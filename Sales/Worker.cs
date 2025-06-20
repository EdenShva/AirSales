namespace Sales;

/// <summary>
/// Represents a salesman who <see href="https://www.youtube.com/watch?v=wn5yyGOZSww">sells</see> tickets.
/// </summary>
public class Worker
{
    private static int _id = 1;
    private static readonly object IdLock = new();

    /// <summary>
    /// Gets the unique identifier assigned to this worker.
    /// </summary>
    public int Id { get; }

    public CancellationToken CancellationToken { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="Worker"/> class,
    /// assigning a unique ID and storing the provided cancellation token.
    /// </summary>
    /// <param name="token">The cancellation token to be associated with the thread.</param>
    public Worker(CancellationToken token)
    {
        lock (IdLock)
        {
            Id = _id++;
            CancellationToken = token;
        }
    }
}