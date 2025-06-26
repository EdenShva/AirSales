namespace Sales;
// Represents a single flight with limited first class and economy seats

public class Flight
{
    // Constant prices for each class
    public const int FirstClassCost = 800;
    public const int EconomyClassCost = 300;

    // Number of available seats in each class
    private int _firstClassSeats = 12;
    private int _economyClassSeats = 120;

    // Shared lock object to ensure thread-safe seat booking
    private static readonly object BookingLock = new();

    // Tries to book a seat (randomly picks between classes if available)
    public bool TryBookSeat(out int cost, out string seatClass)
    {
        lock (BookingLock)  // Ensures only one thread can modify seat counts at a time
        {
            // Check if each class is already sold out
            var firstSold = _firstClassSeats <= 0;
            var economySold = _economyClassSeats <= 0;

            // Randomly decide whether to prefer First or Economy (if both are available)
            var random = Random.Shared.Next() % 2 == 0;

            // Determine seat cost based on availability and random choice
            cost = firstSold && economySold ? 0 :   // No seats left at all
                random ? firstSold ? EconomyClassCost : FirstClassCost :    // Random prefers First
                economySold ? FirstClassCost : EconomyClassCost;    // Random prefers Economy

            // Assign seat class based on the cost
            seatClass = cost == FirstClassCost ? "First" :
            cost == EconomyClassCost ? "Economy" : "None";

            // Reduce the available seats count if a seat was sold
            _firstClassSeats -= cost == FirstClassCost ? 1 : 0;
            _economyClassSeats -= cost == EconomyClassCost ? 1 : 0;

            // Return true if a seat was successfully booked
            return cost != 0;
        }
    }

    // Checks if all seats (both classes) are sold out
    public bool IsSoldOut()
    {
        lock (BookingLock)
        {
            return _firstClassSeats == 0 && _economyClassSeats == 0;
        }
    }

}