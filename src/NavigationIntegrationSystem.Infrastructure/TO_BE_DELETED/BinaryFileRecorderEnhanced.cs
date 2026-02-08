using Infrastructure.FileManagement.Settings;

using log4net;

using System;
using System.IO;

namespace Infrastructure.FileManagement.DataRecording
{
    public class BinaryFileRecorderEnhanced
    {
        #region Parameters
        private const long MinimumFreeSpaceRequiredToStartRecording = 10000000; //in bytes = 10mb
        private const double MinimumFreeSpaceRequiredToStartRecordingInMb = MinimumFreeSpaceRequiredToStartRecording / 1048576.0;
        private const string FilenameExtension = ".dat";
        private object m_LockObject;
        private ILog m_logger;
        private string m_Filename;
        private string m_Pathname;
        private string m_FilenameTitle;
        private string m_RecordingDirectory;
        private bool m_isRecording;
        private ulong m_CurrentFileSize;
        private FileStream m_File;
        private MemoryStream m_MemStream;
        private BinaryWriter m_BinWriter;
        private byte[] m_WriteBuffer;
        private const int m_BufferSize = 1024;
        private DataRecordHeader m_Header;
        private DriveInfo recordDriveInfo;
        private RecordStatistics recordStatistics;
        #endregion  // Parameters

        #region Properties
        public RecordingSettings Settings { get; set; }
        public int SelectedScenrarioIndex { get; private set; }
        public RecordStatistics RecordStatistics
        {
            private set => recordStatistics = value;
            get { return recordStatistics.Clone(); }
        }
        public string Filename
        {
            get { return m_Filename; }
        }

        public string Pathname
        {
            get { return m_Pathname; }
        }

        public ulong Filesize
        {
            get { return m_CurrentFileSize; }
        }

        public bool isRecording
        {
            get { return m_isRecording; }
        }


        public ILog Logger
        {
            set
            {
                lock (m_LockObject)
                {
                    m_logger = value;
                }
            }
        }

        public bool IsRecordingDirectoryAUncPath { get; set; }

        /// <summary>
        /// for unc path there is no way to know the drive letter of the recording path.
        /// thus, no free space check can be made.
        /// use this property to force a disk space check in a certain drive like c: or d:
        /// leave this empty and the recorder will try to get the drive letter from the recording directory (it is not unc)
        /// </summary>
        public string ForceDriveLetterFreeSpaceCheck { get; set; }


        #endregion        // Properties

        public event Action<string> ErrorOccuredDuringRecording;

        /// <summary>
        /// updates subscribers once per seconds about record statistics
        /// </summary>
        public event Action<RecordStatistics> RecordStatisticsUpdate;
        public event Action<bool> RecordingStartStop;

        #region Construcrtor
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="logService"></param>
        /// <param name="settings"></param>
        /// <param name="recordingPath"></param>
        public BinaryFileRecorderEnhanced()
        {
            m_LockObject = new object();
            ForceDriveLetterFreeSpaceCheck = string.Empty;
            IsRecordingDirectoryAUncPath = false;
            m_logger = null;
            m_Pathname = string.Empty;
            m_Filename = "";
            m_FilenameTitle = "Scenario";
            m_RecordingDirectory = string.Empty;
            m_isRecording = false;
            m_CurrentFileSize = 0;

            Settings = new RecordingSettings();
            Settings.LoadDefaultSettings();
            m_File = null;

            m_WriteBuffer = new byte[m_BufferSize];
            m_MemStream = new MemoryStream(m_WriteBuffer);
            m_BinWriter = new BinaryWriter(m_MemStream);
            m_BinWriter.Seek(0, SeekOrigin.Begin);

            m_Header = new DataRecordHeader();

            recordStatistics = new RecordStatistics();
            SelectedScenrarioIndex = 0;
        }

        #endregion  // Construcrtor


        #region Methods

        private void CloseCurrentFile()
        {
            if (m_File != null)
            {
                try
                {
                    m_File.Flush();
                    m_File.Close();

                    string str = "DataRecorder: Closed Recording file " + m_Filename;
                    if (m_logger != null)
                        m_logger.Info(str);

                    if (m_CurrentFileSize == 0)
                    {
                        File.Delete(m_Pathname);
                        str = "DataRecorder: deleted Recording file " + m_Filename + " (due to 0 file size)";
                        if (m_logger != null)
                            m_logger.Info(str);
                    }
                }
                catch (Exception e)
                {
                    if (m_logger != null)
                        m_logger.Error($"Error while closing binary file {m_Filename}.", e);
                }
                finally
                {
                    m_File = null;
                }
            }
        }

        /// <summary>
        /// replace the recording directory to a new one
        /// </summary>
        /// <param name="newDir"></param>
        public void SetRecordingDirectory(string newDir)
        {
            if (!m_isRecording)
            {
                m_RecordingDirectory = newDir;
            }
        }

        public void SelectScenario(byte scenarioID)
        {
            if ((scenarioID >= 0) && (Settings.ScenariosList.Count > scenarioID))
            {
                SelectedScenrarioIndex = scenarioID;
            }
        }

        public string ToggleRecordingCommand()
        {
            if (m_isRecording)
            {
                StopRecording();
                return "";
            }
            else
            {
                return StartRecording();
            }
        }

        /// <returns>empty string if success, otherwise returns error message</returns>
        public string StartRecording()
        {
            try
            {
                if (m_isRecording)
                {
                    m_logger?.Info("Start recording requested while recording is in progress");
                    return "Recording is already in progress!";
                }

                if (m_RecordingDirectory == string.Empty)
                {
                    string error = "Recording Directory is an empty string.";
                    m_logger.Error("Cannot Start recording. " + error);
                    return error;
                }
                Directory.CreateDirectory(m_RecordingDirectory);

                recordDriveInfo = null;

                if (IsRecordingDirectoryAUncPath)
                {
                    if (ForceDriveLetterFreeSpaceCheck != string.Empty)
                    {
                        recordDriveInfo = new DriveInfo(ForceDriveLetterFreeSpaceCheck);
                        if (recordDriveInfo == null)
                        {
                            string error = "Error retrieving recording drive info, cannot check for free space.";
                            m_logger?.Error("Cannot Start recording. " + error);
                            return error;
                        }
                    }
                }
                else
                {
                    recordDriveInfo = new DriveInfo(Path.GetPathRoot(m_RecordingDirectory));
                    if (recordDriveInfo == null)
                    {
                        string error = "Error retrieving recording drive info, cannot check for free space.";
                        m_logger?.Error("Cannot Start recording. " + error);
                        return error;
                    }
                }

                if (recordDriveInfo != null)
                {
                    if (recordDriveInfo.AvailableFreeSpace < MinimumFreeSpaceRequiredToStartRecording)
                    {
                        string error = $"Not enough free space is available for recording in {recordDriveInfo.Name} drive. Minimum required : {MinimumFreeSpaceRequiredToStartRecordingInMb.ToString("F2")} Mb.";
                        m_logger.Error("Cannot Start recording. " + error);
                        return error;
                    }
                }


                DateTime now;
                string str = "Scenario";

                //build new filename:

                if (Settings.ScenariosList.Count > 0)
                {
                    m_FilenameTitle = Settings.ScenariosList[SelectedScenrarioIndex];
                }

                now = DateTime.UtcNow;
                str = now.Year.ToString("0000");
                str += now.Month.ToString("00");
                str += now.Day.ToString("00");
                str += "_";
                str += now.Hour.ToString("00");
                str += now.Minute.ToString("00");
                str += now.Second.ToString("00");
                m_Filename = m_FilenameTitle + "_" + str + FilenameExtension;
                m_Pathname = m_RecordingDirectory + "\\" + m_Filename;

                recordStatistics.StartTime = now;

                m_File = new FileStream(
                   m_Pathname,
                   System.IO.FileMode.CreateNew,
                   System.IO.FileAccess.Write,
                   System.IO.FileShare.None,
                   65536,
                   System.IO.FileOptions.Asynchronous);
                m_CurrentFileSize = 0;

                lock (m_LockObject)
                {
                    m_isRecording = true;
                }

                m_logger?.Info("Recording started. Opened file " + m_Filename);
                RecordingStartStop?.Invoke(true);
                return string.Empty;
            }
            catch (Exception e)
            {
                string errorMsg = "Error while trying to initiate recording session.";
                m_logger?.Error(errorMsg, e);
                errorMsg += Environment.NewLine + e.Message;
                return errorMsg;
            }
        }

        public void StopRecording()
        {
            if (m_isRecording)
            {
                lock (m_LockObject)
                {
                    m_isRecording = false;
                    CloseCurrentFile();
                    m_Filename = "";
                    RecordingStartStop?.Invoke(false);
                }

                m_logger?.Info("Data Recorder: Stopped Data Recording");
            }
        }

        /// <summary>
        /// Records an input data buffer to file.
        /// using Asynchronous IO Operation,
        /// </summary>
        /// <param name="id"></param>
        /// <param name="inData"></param>
        /// <param name="dataSize"></param>
        /// <param name="now"></param>
        /// <returns></returns>
        public bool Record(int id, int type, byte[] inData, long dataSize, DateTime now)
        {
            bool success = false;

            if (dataSize > m_BufferSize)
                return success;

            lock (m_LockObject)
            {
                if (m_isRecording)
                {
                    try
                    {
                        m_Header.ID = (ushort)id;
                        m_Header.DataType = (ushort)type;
                        m_Header.DataLength = (ushort)dataSize;
                        m_Header.Time = now.ToBinary();
                        m_BinWriter.Seek(0, SeekOrigin.Begin);
                        m_BinWriter.Write(m_Header.SyncWord);
                        m_BinWriter.Write(m_Header.ID);
                        m_BinWriter.Write(m_Header.DataLength);
                        m_BinWriter.Write(m_Header.Time);
                        m_BinWriter.Write(m_Header.DataType);
                        m_BinWriter.Write(inData, 0, (int)dataSize);
                        m_File.WriteAsync(m_MemStream.ToArray(), 0, m_Header.HeaderLength + (int)dataSize);
                        m_CurrentFileSize += (ulong)(m_Header.HeaderLength + dataSize);
                        success = true;
                    }
                    catch (Exception e)
                    {
                        m_logger?.Error("Error occured during recording. stopping recording...", e);
                        StopRecording();
                    }
                }
            }

            return success;
        }

        public void OnPeriodicOnePps(object sender, EventArgs e)
        {
            lock (m_LockObject)
            {
                if (m_isRecording)
                {
                    // check for maximum file size:
                    if (m_CurrentFileSize >= Settings.MaximumFileSize)
                    {
                        m_logger?.Info("DataRecorder: Creating New File, Max size Exceeded.");
                        StopRecording();
                        StartRecording();
                        return;
                    }
                    else
                    {
                        try
                        {
                            m_File.Flush();
                        }
                        catch (Exception ex)
                        {
                            StopRecording();
                            m_logger?.Error("Error occured during flush record operation. stopping recording...", ex);
                            ErrorOccuredDuringRecording?.Invoke(ex.Message);

                            return;
                        }
                    }


                    if (recordDriveInfo != null)
                    {
                        if (recordDriveInfo.AvailableFreeSpace < MinimumFreeSpaceRequiredToStartRecording)
                        {
                            StopRecording();
                            m_logger?.Error("Error occured during recording. Not enough free space is available for recording. stopping recording...");
                            ErrorOccuredDuringRecording?.Invoke($"Not enough free space is available for recording in {recordDriveInfo.Name} drive.\nMinimum required : {MinimumFreeSpaceRequiredToStartRecordingInMb.ToString("F2")} Mb.");
                            return;
                        }

                        recordStatistics.RecordingDiskTotalSize = recordDriveInfo.TotalSize;
                        recordStatistics.RecordingDiskFreeSpace = recordDriveInfo.AvailableFreeSpace;
                    }


                    try
                    {
                        FileInfo fi = new FileInfo(m_Pathname);

                        recordStatistics.Elapsed = DateTime.UtcNow - recordStatistics.StartTime;
                        recordStatistics.FileSizeBytes = fi.Length;
                    }
                    catch (Exception er)
                    {
                        StopRecording();
                        m_logger.Error($"Binary file recorder OnPeriodicOnePps FileInfo exception", er);
                        ErrorOccuredDuringRecording?.Invoke("Error retrieving file info :" + Environment.NewLine + er.Message);
                        return;
                    }

                    RecordStatisticsUpdate?.Invoke(recordStatistics.Clone());
                }
            }
        }

        #endregion   // Methods
    }
}
