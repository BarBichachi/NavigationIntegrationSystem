using Infrastructure.Enums;
using System.IO;

namespace Infrastructure.Navigation.NavigationSystems.IntegratedInsOutput;

// Builds/decodes the integrated INS output binary frame (sync + payload + checksum)
public sealed class IntegratedInsOutput_CommFrame
{
    #region Constants

    // Frame sync byte (define a real constant and keep it stable once chosen)
    private const byte m_Sync = 0x50;

    #endregion

    #region Private Fields

    private readonly object m_LockObject;
    private IntegratedInsOutputData m_Data;
    private byte m_Checksum;

    #endregion

    #region Properties

    public static int BinLength { get; } = IntegratedInsOutputData.BinLength + 2 * sizeof(byte);

    public IntegratedInsOutputData Data
    {
        get { lock (m_LockObject) { return m_Data.Clone(); } }
    }

    #endregion

    #region Constructors

    // Creates a new integrated output comm frame
    public IntegratedInsOutput_CommFrame()
    {
        m_LockObject = new object();
        m_Data = new IntegratedInsOutputData();
        m_Checksum = 0;
    }

    #endregion

    #region Decode

    // Decodes a full frame from a byte array
    public BinaryDataDecodingStatus DecodeBinaryData(byte[] i_BinaryData, int i_DataSize)
    {
        if (i_BinaryData == null || i_DataSize <= 0) { return BinaryDataDecodingStatus.LengthError; }
        if (i_DataSize < BinLength) { return BinaryDataDecodingStatus.LengthError; }

        using (MemoryStream m = new MemoryStream(i_BinaryData))
        using (BinaryReader reader = new BinaryReader(m))
        {
            return DecodeBinaryData(reader, i_BinaryData, i_DataSize);
        }
    }

    // Decodes a full frame from a reader (and optionally validates checksum using the source buffer)
    public BinaryDataDecodingStatus DecodeBinaryData(BinaryReader i_Reader, byte[] i_SourceBuffer, int i_DataSize)
    {
        if (i_Reader == null) { return BinaryDataDecodingStatus.LengthError; }
        if (i_DataSize < BinLength) { return BinaryDataDecodingStatus.LengthError; }

        byte sync = i_Reader.ReadByte();

        IntegratedInsOutputData tmpData = new IntegratedInsOutputData();
        tmpData.ReadBinary(i_Reader);

        byte checksum = i_Reader.ReadByte();

        if (sync != m_Sync) { return BinaryDataDecodingStatus.SyncError; }

        // Validates checksum (keep enabled/disabled consistently across the whole recording ecosystem)
        if (i_SourceBuffer != null)
        {
            byte expected = CalculateChecksum(i_SourceBuffer, BinLength);
            // If you want strict validation, uncomment this check (and match it in the encoder)
            // if (checksum != expected) { return BinaryDataDecodingStatus.ChecksumError; }
        }

        lock (m_LockObject)
        {
            m_Data = tmpData;
            m_Checksum = checksum;
        }

        return BinaryDataDecodingStatus.Success;
    }

    #endregion

    #region Encode

    // Encodes a full frame into a provided output array
    public void EncodeBinaryData(ref byte[] io_OutArray, ref int io_Size)
    {
        if (io_OutArray == null || io_OutArray.Length < BinLength) { io_Size = 0; return; }

        using (MemoryStream m = new MemoryStream(io_OutArray))
        using (BinaryWriter writer = new BinaryWriter(m))
        {
            writer.Write(m_Sync);

            IntegratedInsOutputData snapshot = Data;
            snapshot.Encode(writer);

            writer.Write((byte)0); // placeholder checksum, overwritten below
        }

        byte checksum = CalculateChecksum(io_OutArray, BinLength);
        io_OutArray[BinLength - 1] = checksum;
        io_Size = BinLength;

        lock (m_LockObject)
        {
            m_Checksum = checksum;
        }
    }

    #endregion

    #region Private Helpers

    // Calculates a simple additive checksum over the frame, excluding the checksum byte itself
    private static byte CalculateChecksum(byte[] i_Buffer, int i_FrameLength)
    {
        byte checksum = 0;

        // Sum bytes [1..FrameLength-2] like StdInsCommFrame (exclude sync at 0 and checksum at last)
        for (int i = 1; i < i_FrameLength - 1; i++)
        {
            checksum += i_Buffer[i];
        }

        return checksum;
    }

    #endregion
}
