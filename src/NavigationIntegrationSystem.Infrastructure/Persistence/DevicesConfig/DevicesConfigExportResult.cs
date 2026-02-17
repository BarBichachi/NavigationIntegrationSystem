namespace NavigationIntegrationSystem.Infrastructure.Persistence.DevicesConfig;

// Represents the result of exporting devices configuration
public sealed class DevicesConfigExportResult
{
    #region Properties
    public bool IsSuccess { get; }
    public string Message { get; }
    #endregion

    #region Constructors
    // Creates a new export result
    private DevicesConfigExportResult(bool i_IsSuccess, string i_Message)
    {
        IsSuccess = i_IsSuccess;
        Message = i_Message;
    }
    #endregion

    #region Functions
    // Creates a successful export result
    public static DevicesConfigExportResult Success()
    {
        return new DevicesConfigExportResult(true, "Export succeeded.");
    }

    // Creates a failed export result with a message
    public static DevicesConfigExportResult Failure(string i_Message)
    {
        return new DevicesConfigExportResult(false, i_Message);
    }
    #endregion
}
