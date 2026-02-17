namespace NavigationIntegrationSystem.Infrastructure.Persistence.DevicesConfig;

// Represents the result of importing devices configuration
public sealed class DevicesConfigImportResult
{
    #region Properties
    public bool IsSuccess { get; }
    public string Message { get; }
    public DevicesConfigFile? Config { get; }
    #endregion

    #region Constructors
    // Creates a new import result
    private DevicesConfigImportResult(bool i_IsSuccess, string i_Message, DevicesConfigFile? i_Config)
    {
        IsSuccess = i_IsSuccess;
        Message = i_Message;
        Config = i_Config;
    }
    #endregion

    #region Functions
    // Creates a successful import result
    public static DevicesConfigImportResult Success(DevicesConfigFile i_Config)
    {
        return new DevicesConfigImportResult(true, "Import succeeded.", i_Config);
    }

    // Creates a failed import result with a message
    public static DevicesConfigImportResult Failure(string i_Message)
    {
        return new DevicesConfigImportResult(false, i_Message, null);
    }
    #endregion
}
