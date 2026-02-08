namespace Infrastructure.FileManagement.DataRecording
{
    public class DataRecordHeader
    {
        private const ushort m_Sync = 0x7E55;

        public ushort SyncWord => m_Sync;
        public ushort ID { get; set; }
        public ushort DataType { get; set; }
        public ushort DataLength { get; set; }
        public long Time { get; set; }

        public int HeaderLength => (4 * sizeof(ushort) + sizeof(long));

        public DataRecordHeader() { }
    }
}