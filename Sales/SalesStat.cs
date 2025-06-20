namespace Sales;

public class SalesStat
{
    private static readonly object TotalClientsServedLock = new();
    private static readonly object TotalRevenueLock = new();
    private int _totalClientsServed;

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