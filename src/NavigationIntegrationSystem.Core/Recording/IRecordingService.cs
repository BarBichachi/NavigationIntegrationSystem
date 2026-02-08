using System;

namespace NavigationIntegrationSystem.Core.Recording;

public interface IRecordingService
{
    #region Properties
    bool IsRecording { get; }
    #endregion

    #region Events
    event EventHandler<bool>? RecordingStateChanged;
    #endregion

    #region Functions
    // Starts the recording session
    void Start();

    // Stops the recording session
    void Stop();

    // Records the Integrated INS Output record (Type 50)
    void RecordIntegratedOutput(object i_DataSnapshot);
    #endregion
}