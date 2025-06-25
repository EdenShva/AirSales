namespace Manager;

// Represents sales statistics, including the total number of clients served
// and the total revenue. Access to these properties is thread-safe.
public class SalesStat
{
    // Lock object for synchronizing access to <see cref="_totalClientsServed"/>.
    private static readonly object TotalClientsServedLock = new();

    // Lock object for synchronizing access to <see cref="_totalRevenue"/>.
    private static readonly object TotalRevenueLock = new();

    private int _totalClientsServed;

    // Gets or sets the total number of clients served. Access is synchronized to ensure thread safety.
    public int TotalClientsServed
    {
        get
        {
            lock (TotalClientsServedLock)
            {
                return _totalClientsServed;
            }
        }
        set
        {
            lock (TotalClientsServedLock)
            {
                _totalClientsServed = value;
            }
        }
    }

    private int _totalRevenue;

    // Gets or sets the total revenue. Access is synchronized to ensure thread safety.
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

    // Returns a string representation of the sales statistics.
    public override string ToString()
    {
        return $"{nameof(TotalClientsServed)}={TotalClientsServed}, {nameof(TotalRevenue)}={TotalRevenue}";
    }
}