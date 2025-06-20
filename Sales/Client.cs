namespace Sales;

/// <summary>
/// Represents a client that want to buy a ticket.
/// </summary>
public class Client
{
    private static int _id = 1;
    private static readonly object IdLock = new();

    /// <summary>
    /// Gets the unique identifier assigned to this client.
    /// </summary>
    public int Id { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="Client"/> class
    /// and assigns a unique ID in a thread-safe manner.
    /// </summary>
    public Client()
    {
        lock (IdLock)
        {
            Id = _id++;
        }
    }
}