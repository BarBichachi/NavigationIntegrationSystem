using System;

namespace Infrastructure.FileManagement.DataRecording
{
    public class RecordStatistics
    {
        public DateTime StartTime { get; set; }
        public TimeSpan Elapsed { get; set; }
        public long FileSizeBytes { get; set; }
        public long RecordingDiskTotalSize { get; set; }
        public long RecordingDiskFreeSpace { get; set; }

        public RecordStatistics Clone()
        {
            return (RecordStatistics)this.MemberwiseClone();
        }
    }
}