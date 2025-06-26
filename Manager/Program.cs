using System.Diagnostics;
using System.IO.MemoryMappedFiles;
using System.Text;
using System.Text.Json;
using System.Timers;
using Manager;
using SuperSimpleTcp;
using Timer = System.Timers.Timer;

namespace Manager;

class Program
{
    // Codes for inter-process communication
    const byte departure = 0x5D;
    const byte weAreTooRichNow = 0x7A;
    const byte allFlightsSoldOut = 0x9E;

    // Sales statistics object (money and clients served)
    static SalesStat salesStat = new();

    // Indicates whether the operation has already ended
    static bool operationEnded = false;
    static readonly object endLock = new();

    // TCP server address for connecting to the Sales process
    const string ServerAddress = "127.0.0.1:17000";

    // Revenue threshold to determine "We are too rich"
    const int WeAreTooRichThreshold = 700000;

    static SimpleTcpClient tcpClient;
    static MemoryMappedFile mmf;
    static Mutex mmfMutex;
    static Timer readMmfTimer;
    static Timer departureTimer;

    static void Main(string[] args)
    {
        Console.WriteLine("Manager is starting...");

        InitializeSharedMemory();
        InitializeTcpClient();
        InitializeTimers();

        Console.WriteLine("Manager is running. Press Enter to exit...");
        Console.ReadLine();
    }

    // Access the shared memory and named mutex created by Sales process
    static void InitializeSharedMemory()
    {
        mmf = MemoryMappedFile.OpenExisting("AirSalesMMF_d19d8f1a-9fc5-425f-ba56-60f9815998ac");
        mmfMutex = new Mutex(false, "Global\\AirSalesMutex_d19d8f1a-9fc5-425f-ba56-60f9815998ac");
        Console.WriteLine("Shared memory and mutex initialized.");
    }

    // Connect to the Sales TCP server
    static void InitializeTcpClient()
    {
        tcpClient = new SimpleTcpClient(ServerAddress);
        tcpClient.Events.DataReceived += OnDataReceived;
        tcpClient.Connect();
        Console.WriteLine($"Connected to Sales TCP server at {ServerAddress}");
    }


    static void InitializeTimers()
    {
        // Timer to read shared memory every second
        readMmfTimer = new Timer(1000);
        readMmfTimer.Elapsed += ReadMmfEverySecond;
        readMmfTimer.AutoReset = true;
        readMmfTimer.Start();
        Console.WriteLine("Memory read timer started.");

        // Timer to send departure signal after 5 seconds
        departureTimer = new Timer(5000);
        departureTimer.Elapsed += DepartureTimerOnElapsed;
        departureTimer.AutoReset = false;
        departureTimer.Start();
        Console.WriteLine("Departure timer started.");
    }

    // Handle data received from the Sales process
    static void OnDataReceived(object? sender, SuperSimpleTcp.DataReceivedEventArgs e)
    {
        byte receivedCode = e.Data[0];

        if (receivedCode == allFlightsSoldOut)
        {
            Console.WriteLine("[OnDataReceived] [Manager] Received code from Sales: allFlightsSoldOut.");
            EndOfOperation(Reason.SoldOut);
        }
        else
        {
            Console.WriteLine($"[OnDataReceived] [Manager] Unknown code received: 0x{receivedCode:X2}");
        }
    }

    // End manager operation safely, only once
    static void EndOfOperation(Reason reason)
    {
        lock (endLock)
        {
            if (operationEnded)
                return;

            operationEnded = true;

            readMmfTimer.Stop();    // Stop reading memory
            departureTimer.Stop();  // Stop departure timer if still running

            // Print reason for ending
            switch (reason)
            {
                case Reason.Departure:
                    Console.WriteLine("END OF PROGRAM: DEPARTURE");
                    break;
                case Reason.TooRich:
                    Console.WriteLine("END OF PROGRAM: WE ARE TOO RICH NOW");
                    break;
                case Reason.SoldOut:
                    Console.WriteLine("END OF PROGRAM: ALL FLIGHTS SOLD OUT");
                    break;
            }
        }
    }

    // Triggered when the departure timer elapses
    static void DepartureTimerOnElapsed(object? sender, ElapsedEventArgs e)
    {
        lock (endLock)
        {
            if (operationEnded)
                return;

            Console.WriteLine("[DepartureTimerOnElapsed] [Manager] Departure timer elapsed. Sending departure code to Sales.");
            tcpClient.Send(new byte[] { departure });   // Notify Sales process
            EndOfOperation(Reason.Departure);
        }
    }

    // Triggered every second by timer to read data from shared memory
    static void ReadMmfEverySecond(object? sender, ElapsedEventArgs e)
    {
        ReadMmf();

        Console.WriteLine($"[ReadMmfEverySecond] [Manager] Reading mmf data : TotalClientsServed={salesStat.TotalClientsServed}, TotalRevenue={salesStat.TotalRevenue}");

        // If revenue is too high, send "WeAreTooRich" code and shut down
        if (salesStat.TotalRevenue >= WeAreTooRichThreshold)
        {
            Console.WriteLine("[ReadMmfEverySecond] [Manager] We are so rich now... Sending WeAreTooRichNow code to Sales");
            tcpClient.Send(new byte[] { weAreTooRichNow });
            EndOfOperation(Reason.TooRich);
        }
    }

    // Read 8 bytes from shared memory: 4 for revenue, 4 for clients served
    // Update the local salesStat object with the values read from shared memory
    static void ReadMmf()
    {
        try
        {
            // Lock the mutex to safely access shared memory (prevents race conditions)
            mmfMutex.WaitOne();

            // Create a memory accessor for the first 8 bytes of the memory-mapped file
            using var accessor = mmf.CreateViewAccessor(0, 8);

            // Read an integer (4 bytes) starting at byte 0 – this is the total revenue
            int revenue = accessor.ReadInt32(0);
            // Read another integer (4 bytes) starting at byte 4 – this is the total number of clients served
            int clients = accessor.ReadInt32(4);

            // Update the local salesStat object with the values read from shared memory
            salesStat.TotalRevenue = revenue;
            salesStat.TotalClientsServed = clients;
        }
        finally
        {
            // Release the mutex so other processes/threads can access the shared memory
            mmfMutex.ReleaseMutex();    // Always release the mutex
        }
    }
}


