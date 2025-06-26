namespace Sales;
// Holds shared sales statistics: total clients served and total revenue
public class SalesStat
{
    // Lock objects for thread-safe access to each property
    private static readonly object TotalClientsServedLock = new();
    private static readonly object TotalRevenueLock = new();

    // field for total number of clients served
    private int _totalClientsServed;


    // thread-safe getter and setter
    public int TotalClientsServed
    {
        get
        {
            lock (TotalClientsServedLock)   // Ensure safe access across threads
            {
                return _totalClientsServed;
            }
        }
        set
        {
            lock (TotalClientsServedLock)   // Ensure safe update across threads
            {
                _totalClientsServed = value;
            }
        }
    }

    // field for total revenue
    private int _totalRevenue;

    public int TotalRevenue
    {
        get
        {
            lock (TotalRevenueLock)
            {
                return _totalRevenue;
            }
        }
        set
        {
            lock (TotalRevenueLock)
            {
                _totalRevenue = value;
            }
        }
    }


    public override string ToString()
    {
        return $"{nameof(TotalClientsServed)}={TotalClientsServed}, {nameof(TotalRevenue)}={TotalRevenue}";
    }
}