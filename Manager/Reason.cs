namespace Manager;

// The reason to stop the program execution
public enum Reason
{
    // Departure timer elapsed. It's time for departure.
    Departure,
    // WeAreTooRichThreshold defined by Manager is reached.
    TooRich,
    // All flights are sold out.
    SoldOut
}