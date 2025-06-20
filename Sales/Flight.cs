namespace Sales;

/// <summary>
/// Represents a flight with bookable seats.
/// </summary>
public class Flight
{
    /// <summary>
    /// The cost of a first-class ticket.
    /// </summary>
    public const int FirstClassCost = 800;

    /// <summary>
    /// The cost of an economy-class ticket.
    /// </summary>
    public const int EconomyClassCost = 300;

    private int _firstClassSeats = 6;
    private int _economyClassSeats = 12;
    private static readonly object BookingLock = new();

    /// <summary>
    /// Attempts to book a seat on the flight.
    /// </summary>
    /// <param name="cost">Outputs the cost of the booked seat. If no seats are available, the cost is set to 0.</param>
    /// <returns><c>true</c> if a seat was successfully booked; otherwise, <c>false</c>.</returns>
    public bool TryBookSeat(out int cost)
    {
        lock (BookingLock)
        {
            var firstSold = _firstClassSeats <= 0;
            var economySold = _economyClassSeats <= 0;
            var random = Random.Shared.Next() % 2 == 0;
            cost = firstSold && economySold ? 0 :
                random ? firstSold ? EconomyClassCost : FirstClassCost :
                economySold ? FirstClassCost : EconomyClassCost;
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