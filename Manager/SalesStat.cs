namespace Manager;

/// <summary>
/// Represents sales statistics, including the total number of clients served
/// and the total revenue. Access to these properties is thread-safe.
/// </summary>
public class SalesStat
{
    /// <summary>
    /// Lock object for synchronizing access to <see cref="_totalClientsServed"/>.
    /// </summary>
    private static readonly object TotalClientsServedLock = new();

    /// <summary>
    /// Lock object for synchronizing access to <see cref="_totalRevenue"/>.
    /// </summary>
    private static readonly object TotalRevenueLock = new();

    private int _totalClientsServed;

    /// <summary>
    /// Gets or sets the total number of clients served. Access is synchronized to ensure thread safety.
    /// </summary>
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

    /// <summary>
    /// Gets or sets the total revenue. Access is synchronized to ensure thread safety.
    /// </summary>
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

    /// <summary>
    /// Returns a string representation of the sales statistics.
    /// </summary>
    /// <returns>A formatted string displaying total clients served and total revenue.</returns>
    public override string ToString()
    {
        return $"{nameof(TotalClientsServed)}={TotalClientsServed}, {nameof(TotalRevenue)}={TotalRevenue}";
    }
}