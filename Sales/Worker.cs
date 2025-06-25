namespace Sales;

// Represents a salesman who sells tickets.
public class Worker
{
    private static int _id = 1;
    private static readonly object IdLock = new();

    // Gets the unique identifier assigned to this worker.
    public int Id { get; }

    public CancellationToken CancellationToken { get; }

    // Initializes a new instance of the <see cref="Worker"/> class,
    // assigning a unique ID and storing the provided cancellation token.
    // The cancellation token to be associated with the thread.
    public Worker(CancellationToken token)
    {
        lock (IdLock)
        {
            Id = _id++;
            CancellationToken = token;
        }
    }
}