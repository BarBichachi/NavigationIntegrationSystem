namespace NavigationIntegrationSystem.Infrastructure.Recording
{
    public class DataRecordHeader
    {
        private const ushort m_Sync = 0x7E55;

        public ushort ID { get; set; }
        public ushort DataType { get; set; }
        public ushort DataLength { get; set; }
        public long Time { get; set; }
        public int HeaderLength
        {
            get { return (4 * sizeof(ushort) + sizeof(long)); }
        }
        public ushort SyncWord
        {
            get { return m_Sync; }
        }

        public DataRecordHeader()
        {

        }
    }
}
