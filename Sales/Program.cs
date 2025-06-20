using System.Collections.Concurrent;
using System.IO.MemoryMappedFiles;
using System.Timers;
using Sales;
using SuperSimpleTcp;
using Timer = System.Timers.Timer;


namespace Sales;

class Program
{
    // Define GUID string

    const byte departure = 0x5D;
    const byte weAreTooRichNow = 0x7A;
    const byte allFlightsSoldOut = 0x9E;

    const int nWorkers = 5;
    const int nFlights = 3;

    const string MmfName = "AirSalesMMF";
    const string MutexName = "Global\\AirSalesMMF_Mutex";




    Timer clientCreationTimer;      // Create clientCreationTimer,
    CancellationTokenSource cts = new();    // CancellationTokenSource()
    ConcurrentQueue<Client> clientQueue = new();   // ConcurrentQueue<Client>()
    List<Flight> flights = new();   // Create Flights i.e.: List<Flight>
    MemoryMappedFile mmf;   // Create mmf
    SimpleTcpServer tcpServer;   // Create tcpServer
    Mutex mmfMutex = new(false, MutexName);    // Create Mutex for accessing mmf
    SalesStat salesStat = new();  // Create salesStat object to store sales statistics
    // locks and other variables
    string managerIpPort = "";
    object consoleLock = new();
    object salesStatLock = new();
    bool endOfSalesCalled = false;
    object endOfSalesLock = new();


    static void Main(string[] args)
    {
        var program = new Program();
        program.Run();
    }

    void Run()
    {
        InitializeFlights();
        InitializeSharedMemory();
        StartTcpServer();
        // Wait for manager to start and connect to tcpServer
        Console.WriteLine("Press any key when manager process ready..");
        Console.ReadKey();

        StartClientFactory();
        StartWorkerThreads();

        // This statement prevents the program from ending prematurely
        Console.ReadLine();
        return;
    }

    void InitializeFlights()
    {
        for (int i = 0; i < nFlights; i++)
        {
            flights.Add(new Flight());
            PrintWithLock($"Flight #{i + 1} initialized.");
        }
    }

    void InitializeSharedMemory()
    {
        mmf = MemoryMappedFile.CreateOrOpen(MmfName, 4096);
        PrintWithLock("Memory-mapped file created (4KB).");
    }

    void StartTcpServer()
    {
        // Start tcpServer and define Events

        tcpServer = new SimpleTcpServer("0.0.0.0:9000"); 
        tcpServer.Events.ClientConnected += TcpServerOnClientConnected;
        tcpServer.Events.DataReceived += TcpServerOnDataReceived;
        tcpServer.Start();
        PrintWithLock("TCP server started on port 9000.");
    }

    void TcpServerOnDataReceived(object? sender, DataReceivedEventArgs e)
    {
        // When data received 
        // 1. If it is weAreTooRichNow then  EndOfSales(Reason.TooRich);
        // 2. If it is departure then  EndOfSales(Reason.Departure);

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

    void TcpServerOnClientConnected(object? sender, ConnectionEventArgs e)
    {
        // Save `e.IpPort` into a variable 
        // Print appropriate message

        managerIpPort = e.IpPort;
        PrintWithLock($"Manager connected from {managerIpPort}");
    }


    void UpdateSharedMemory()
    {
        // 1. Acquire Mutex
        // 2. Update MMF data
        // don't forget to use try/finally

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

    void Sell(object? obj)
    {
        // 0. Get Worker object from obj
        // 1. Worker thread seats in a loop until it being cancelled or until all flights are sold out
        // 2. In a loop:
        //  2.1 Try to take Client from queue (if none available just continue)
        //  2.2 Go over all the flights and try to book a seat for a client
        //  2.3 If succeeds 
        //      2.3.1 Print a message
        //      2.3.2 Update workers own stats and shared memory
        //  3. If All tickets sold out : EndOfSales(Reason.SoldOut)

        if (obj is not Worker worker)
            return;

        while (!worker.CancellationToken.IsCancellationRequested)
        {
            if (!clientQueue.TryDequeue(out var client))
            {
                Thread.Sleep(10);
                continue;
            }

            PrintWithLock($"Worker #{worker.Id} started processing Client #{client.Id}");

            bool sold = false;

            foreach (var flight in flights)
            {
                if (flight.TryBookSeat(out int cost))
                {
                    // עדכון הסטטיסטיקות בתוך lock למניעת תחרות
                    lock (salesStatLock)
                    {
                        salesStat.TotalRevenue += cost;
                        salesStat.TotalClientsServed++;
                    }

                    UpdateSharedMemory();

                    PrintWithLock($"Worker #{worker.Id} sold seat for {cost}$ to Client #{client.Id}");
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



    void EndOfSales(Reason reason)
    {
        // Ensure only 1 thread will enter this function only once
        // Cancel the CancellationTokenSource
        // Stop the clientCreationTimer
        // Print the reason - why program stopped
        // If the reason is ALL FLIGHTS SOLD OUT
        // Then don't forget You have to send the code via TCP to Manager

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

    void StartWorkerThreads()
    {
        // Create threads and workers
        // Start worker threads and bind worker objects to threads

        for (int i = 0; i < nWorkers; i++)
        {
            var worker = new Worker(cts.Token);
            Thread thread = new Thread(Sell);
            thread.IsBackground = true;
            thread.Start(worker);
        }
    }

    void CreateClient(object? sender, ElapsedEventArgs e)
    {
        // Rearm clientCreationTimer
        // Create new client
        // Print message
        // Client should enter the queue

        var client = new Client(); // Create a new client instance

        clientQueue.Enqueue(client); // Add the client to the queue

        PrintWithLock($"Client #{client.Id} created and added to queue.");
    }

    void StartClientFactory()
    {
        // Define clientCreationTimer and bind CreateClient to Elapsed event

        clientCreationTimer = new Timer(1000); // 1 second interval
        clientCreationTimer.Elapsed += CreateClient;
        clientCreationTimer.AutoReset = true;
        clientCreationTimer.Start();
        PrintWithLock("Client factory started.");
    }

    void PrintWithLock(string message)
    {
        lock (consoleLock)
        {
            Console.WriteLine(message);
        }
    }

}


