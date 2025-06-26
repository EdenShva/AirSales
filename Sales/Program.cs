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
    // Signal codes for communication with manager process
    const byte departure = 0x5D;
    const byte weAreTooRichNow = 0x7A; 
    const byte allFlightsSoldOut = 0x9E;

    // Constants for the number of workers and flights
    const int nWorkers = 5;
    const int nFlights = 3;

    // State variables
    private static string managerIpPort = "";
    private static bool endOfSalesCalled = false;

    // Locks for thread-safe access to console and shared state
    private static object consoleLock = new();
    private static object salesStatLock = new();
    private static object endOfSalesLock = new();

    // Timer to generate new clients
    private static Timer clientCreationTimer=new();

    // Cancellation token for stopping workers
    private static CancellationTokenSource cts = new();

    // Sales statistics tracker (money and clients served)
    private static SalesStat salesStat = new();

    // Queue for incoming clients
    private static ConcurrentQueue<Client> clientQueue = new();

    // List of available flights
    private static List<Flight> flights = new();

    // TCP server for manager communication
    private static SimpleTcpServer tcpServer;

    // Memory-mapped file and mutex for inter-process communication
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

        Console.ReadLine(); // Wait to keep main thread alive
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
        mmf = MemoryMappedFile.CreateOrOpen("AirSalesMMF_d19d8f1a-9fc5-425f-ba56-60f9815998ac", Environment.SystemPageSize);
        PrintWithLock($"Memory-mapped file created ({Environment.SystemPageSize}).");

        mmfMutex = new(false, "Global\\AirSalesMutex_d19d8f1a-9fc5-425f-ba56-60f9815998ac");
    }

    static void StartTcpServer()
    {
        tcpServer = new SimpleTcpServer("127.0.0.1:17000"); 
        tcpServer.Events.ClientConnected += TcpServerOnClientConnected;
        tcpServer.Events.DataReceived += TcpServerOnDataReceived;
        tcpServer.Start();
        PrintWithLock("TCP server started on port 17000.");
    }

    // Handle data received from the manager
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

    // Handle connection from manager process
    static void TcpServerOnClientConnected(object? sender, ConnectionEventArgs e)
    {
        managerIpPort = e.IpPort;
        PrintWithLock($"Manager connected from {managerIpPort}");
    }

    // Update shared memory with current sales statistics
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

    // Main selling logic for each worker
    static void Sell(object? obj)
    {
        if (obj is not Worker worker)
            return;

        while (!worker.CancellationToken.IsCancellationRequested)
        {
            if (!clientQueue.TryDequeue(out var client))
            {
                Thread.Sleep(10);   // Wait if no clients available
                continue;
            }

            //PrintWithLock($"Worker #{worker.Id} started processing Client #{client.Id}");

            bool sold = false;

            // Try to sell a seat to the client
            foreach (var flight in flights)
            {
                if (flight.TryBookSeat(out int cost, out string seatClass))
                {
                    // Update sales stats
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

            // If all flights are sold out, end sales
            if (!sold && flights.All(f => f.IsSoldOut()))
            {
                PrintWithLock("All flights sold out. Ending sales.");
                EndOfSales(Reason.SoldOut);
                return;
            }
        }
    }

    // End the ticket sales process
    static void EndOfSales(Reason reason)
    {
        lock (endOfSalesLock)
        {
            if (endOfSalesCalled) return; 
            endOfSalesCalled = true;
        }

        cts.Cancel();   // Stop worker threads
        clientCreationTimer?.Stop();    // Stop client creation

        PrintWithLock($"Sales ended due to: {reason}");

        // Notify manager if sales ended due to sold-out condition
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

    // Start background worker threads
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

    // Timer callback: creates a new client and adds to queue
    static void CreateClient(object? sender, ElapsedEventArgs e)
    {
        var client = new Client(); 
        clientQueue.Enqueue(client);
        PrintWithLock($"[CreateClient] [{client.Id}]Client created.");
    }

    static void StartClientFactory()
    {
        // Start client generation with a timer
        clientCreationTimer.Interval = Random.Shared.Next(5, 26);
        clientCreationTimer.Elapsed += CreateClient;
        clientCreationTimer.AutoReset = true;
        clientCreationTimer.Start();
        PrintWithLock("Client factory started.");
    }

    // Thread-safe printing to the console
    static void PrintWithLock(string message)
    {
        lock (consoleLock)
        {
            Console.WriteLine(message);
        }
    }
}


