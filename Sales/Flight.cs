namespace Sales;


public class Flight
{
    public const int FirstClassCost = 800;
    public const int EconomyClassCost = 300;
    private int _firstClassSeats = 12;
    private int _economyClassSeats = 120;
    private static readonly object BookingLock = new();

    public bool TryBookSeat(out int cost, out string seatClass)
    {
        lock (BookingLock)
        {
            var firstSold = _firstClassSeats <= 0;
            var economySold = _economyClassSeats <= 0;
            var random = Random.Shared.Next() % 2 == 0;
            cost = firstSold && economySold ? 0 :
                random ? firstSold ? EconomyClassCost : FirstClassCost :
                economySold ? FirstClassCost : EconomyClassCost;
            seatClass = cost == FirstClassCost ? "First" :
            cost == EconomyClassCost ? "Economy" : "None";
            _firstClassSeats -= cost == FirstClassCost ? 1 : 0;
            _economyClassSeats -= cost == EconomyClassCost ? 1 : 0;
            return cost != 0;
        }
    }

    public bool IsSoldOut()
    {
        lock (BookingLock)
        {
            return _firstClassSeats == 0 && _economyClassSeats == 0;
        }
    }

}