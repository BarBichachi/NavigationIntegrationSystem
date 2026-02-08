namespace Infrastructure.Enums
{
    public enum BinaryDataDecodingStatus
    {
        LengthError = 0,
        ChecksumError,
        SyncError,
        Success,
        OtherError,
        Irrelevant
    }
}
