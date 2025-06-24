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
    const byte departure = 0x5D;
    const byte weAreTooRichNow = 0x7A;
    const byte allFlightsSoldOut = 0x9E;

    static SalesStat salesStat = new(); 
    static bool operationEnded = false;
    static readonly object endLock = new();

    const string ServerAddress = "127.0.0.1:17000";

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

    static void InitializeSharedMemory()
    {
        mmf = MemoryMappedFile.OpenExisting("AirSalesMMF");
        mmfMutex = new Mutex(false, "Global\\AirSalesMMF_Mutex");
        Console.WriteLine("Shared memory and mutex initialized.");
    }

    static void InitializeTcpClient()
    {
        tcpClient = new SimpleTcpClient(ServerAddress);
        tcpClient.Events.DataReceived += OnDataReceived;
        tcpClient.Connect();
        Console.WriteLine($"Connected to Sales TCP server at {ServerAddress}");
    }

    static void InitializeTimers()
    {
        readMmfTimer = new Timer(1000);
        readMmfTimer.Elapsed += ReadMmfEverySecond;
        readMmfTimer.AutoReset = true;
        readMmfTimer.Start();
        Console.WriteLine("Memory read timer started.");

        departureTimer = new Timer(5000);
        departureTimer.Elapsed += DepartureTimerOnElapsed;
        departureTimer.AutoReset = false;
        departureTimer.Start();
        Console.WriteLine("Departure timer started.");
    }

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

    static void EndOfOperation(Reason reason)
    {
        lock (endLock)
        {
            if (operationEnded)
                return;

            operationEnded = true;

            readMmfTimer.Stop();
            departureTimer.Stop();

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


    static void DepartureTimerOnElapsed(object? sender, ElapsedEventArgs e)
    {
        lock (endLock)
        {
            if (operationEnded)
                return;

            Console.WriteLine("[DepartureTimerOnElapsed] [Manager] Departure timer elapsed. Sending departure code to Sales.");
            tcpClient.Send(new byte[] { departure });
            EndOfOperation(Reason.Departure);
        }
    }

    static void ReadMmfEverySecond(object? sender, ElapsedEventArgs e)
    {
        ReadMmf();

        Console.WriteLine($"[ReadMmfEverySecond] [Manager] Reading mmf data : TotalClientsServed={salesStat.TotalClientsServed}, TotalRevenue={salesStat.TotalRevenue}");

        if (salesStat.TotalRevenue >= WeAreTooRichThreshold)
        {
            Console.WriteLine("[ReadMmfEverySecond] [Manager] We are so rich now... Sending WeAreTooRichNow code to Sales");
            tcpClient.Send(new byte[] { weAreTooRichNow });
            EndOfOperation(Reason.TooRich);
        }
    }

    static void ReadMmf()
    {
        try
        {
            mmfMutex.WaitOne();

            using var accessor = mmf.CreateViewAccessor(0, 8);
            int revenue = accessor.ReadInt32(0);
            int clients = accessor.ReadInt32(4);

            salesStat.TotalRevenue = revenue;
            salesStat.TotalClientsServed = clients;
        }
        finally
        {
            mmfMutex.ReleaseMutex();
        }
    }
}


