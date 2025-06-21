namespace Manager;

/// <summary>
/// The reason to stop the program execution
/// </summary>
public enum Reason
{
    /// <summary>
    /// Departure timer elapsed. It's time for departure.
    /// </summary>
    Departure,
    /// <summary>
    /// WeAreTooRichThreshold defined by Manager is reached.
    /// </summary>
    TooRich,
    /// <summary>
    /// All flights are sold out.
    /// </summary>
    SoldOut
}