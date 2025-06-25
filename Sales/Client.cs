namespace Sales;

// Represents a client that want to buy a ticket.
public class Client
{
    private static int _id = 1;
    private static readonly object IdLock = new();

    // Gets the unique identifier assigned to this client.
    public int Id { get; }

    // Initializes a new instance of the <see cref="Client"/> class
    // and assigns a unique ID in a thread-safe manner.
    public Client()
    {
        lock (IdLock)
        {
            Id = _id++;
        }
    }
}