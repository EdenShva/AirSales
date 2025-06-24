using System;
using System.Collections.Concurrent;
using System.IO.MemoryMappedFiles;
using System.Timers;
using Sales;
using SuperSimpleTcp;
using Timer = System.Timers.Timer;

/* * Air Sales System
 * 
 * This program simulates a ticket sales system for flights.
 * It includes a TCP server to communicate with a manager process,
 * handles multiple clients and workers, and uses memory-mapped files
 * for shared state.
 * 
 */

namespace Sales;
class Program
{
    const byte departure = 0x5D;
    const byte weAreTooRichNow = 0x7A; 
    const byte allFlightsSoldOut = 0x9E;

    const int nWorkers = 5;
    const int nFlights = 3;
    
    private static string managerIpPort = "";
    private static bool endOfSalesCalled = false;

    private static object consoleLock = new();
    private static object salesStatLock = new();
    private static object endOfSalesLock = new();

    private static Random random = new();
    private static Timer clientCreationTimer=new();
    private static CancellationTokenSource cts = new();  
    private static SalesStat salesStat = new(); 

    private static ConcurrentQueue<Client> clientQueue = new();
    private static List<Flight> flights = new();

    private static SimpleTcpServer tcpServer;
    private static MemoryMappedFile mmf;
    private static Mutex mmfMutex;  

    static void Main(string[] args)
    {
        InitializeFlights();
        InitializeSharedMemory();
        StartTcpServer();

        Console.WriteLine("Press any key when manager process ready..");
        Console.ReadKey();

        StartClientFactory();
        StartWorkerThreads();

        Console.ReadLine();
        return;
    }

    static void InitializeFlights()
    {
        for (int i = 0; i < nFlights; i++)
        {
            flights.Add(new Flight());
            PrintWithLock($"Flight #{i + 1} initialized.");
        }
    }

    static void InitializeSharedMemory()
    {
        mmf = MemoryMappedFile.CreateOrOpen("AirSalesMMF", 4096);
        PrintWithLock("Memory-mapped file created (4KB).");

        mmfMutex = new(false, "Global\\AirSalesMMF_Mutex");
    }

    static void StartTcpServer()
    {
        tcpServer = new SimpleTcpServer("127.0.0.1:17000"); 
        tcpServer.Events.ClientConnected += TcpServerOnClientConnected;
        tcpServer.Events.DataReceived += TcpServerOnDataReceived;
        tcpServer.Start();
        PrintWithLock("TCP server started on port 17000.");
    }

    static void TcpServerOnDataReceived(object? sender, DataReceivedEventArgs e)
    {
        if (e.Data.Array == null || e.Data.Count == 0)
            return;

        byte code = e.Data.Array[e.Data.Offset];

        switch (code)
        {
            case weAreTooRichNow:
                PrintWithLock("Received 'We Are Too Rich' signal from manager.");
                EndOfSales(Reason.TooRich);
                break;
            case departure:
                PrintWithLock("Received 'Departure' signal from manager.");
                EndOfSales(Reason.Departure);
                break;
            default:
                PrintWithLock($"Received unknown code {code:X2} from manager.");
                break;
        }
    }

    static void TcpServerOnClientConnected(object? sender, ConnectionEventArgs e)
    {
        managerIpPort = e.IpPort;
        PrintWithLock($"Manager connected from {managerIpPort}");
    }

    static void UpdateSharedMemory()
    {
        try
        {
            mmfMutex.WaitOne();

            using var accessor = mmf.CreateViewAccessor(0, 8);
            byte[] revenueBytes = BitConverter.GetBytes(salesStat.TotalRevenue);
            byte[] clientsBytes = BitConverter.GetBytes(salesStat.TotalClientsServed);

            accessor.WriteArray(0, revenueBytes, 0, 4);
            accessor.WriteArray(4, clientsBytes, 0, 4);
        }
        finally
        {
            mmfMutex.ReleaseMutex();
        }
    }

    static void Sell(object? obj)
    {
        if (obj is not Worker worker)
            return;

        while (!worker.CancellationToken.IsCancellationRequested)
        {
            if (!clientQueue.TryDequeue(out var client))
            {
                Thread.Sleep(10);
                continue;
            }

            //PrintWithLock($"Worker #{worker.Id} started processing Client #{client.Id}");

            bool sold = false;

            foreach (var flight in flights)
            {
                if (flight.TryBookSeat(out int cost, out string seatClass))
                {

                    lock (salesStatLock)
                    {
                        salesStat.TotalRevenue += cost;
                        salesStat.TotalClientsServed++;
                    }

                    UpdateSharedMemory();

                    PrintWithLock($"[Sell] [{worker.Id}]Worker sold {seatClass} class to [{client.Id}]Client");
                    sold = true;
                    break;
                }
            }

            if (!sold && flights.All(f => f.IsSoldOut()))
            {
                PrintWithLock("All flights sold out. Ending sales.");
                EndOfSales(Reason.SoldOut);
                return;
            }
        }
    }

    static void EndOfSales(Reason reason)
    {
        lock (endOfSalesLock)
        {
            if (endOfSalesCalled) return; 
            endOfSalesCalled = true;
        }

        cts.Cancel(); 
        clientCreationTimer?.Stop();

        PrintWithLock($"Sales ended due to: {reason}");

        if (reason == Reason.SoldOut)
        {
            if (!string.IsNullOrEmpty(managerIpPort))
            {
                byte code = allFlightsSoldOut;
                tcpServer.Send(managerIpPort, new byte[] { code });
                PrintWithLock($"Sent sold-out code ({code:X2}) to manager at {managerIpPort}");
            }
        }
    }

    static void StartWorkerThreads()
    {
        for (int i = 0; i < nWorkers; i++)
        {
            var worker = new Worker(cts.Token);
            Thread thread = new Thread(Sell);
            thread.IsBackground = true;
            thread.Start(worker);
        }
    }

    static void CreateClient(object? sender, ElapsedEventArgs e)
    {
        var client = new Client(); 
        clientQueue.Enqueue(client);
        PrintWithLock($"[CreateClient] [{client.Id}]Client created.");
    }

    static void StartClientFactory()
    {
        clientCreationTimer.Interval = random.Next(5, 26);
        clientCreationTimer.Elapsed += CreateClient;
        clientCreationTimer.AutoReset = true;
        clientCreationTimer.Start();
        PrintWithLock("Client factory started.");
    }

    static void PrintWithLock(string message)
    {
        lock (consoleLock)
        {
            Console.WriteLine(message);
        }
    }
}


