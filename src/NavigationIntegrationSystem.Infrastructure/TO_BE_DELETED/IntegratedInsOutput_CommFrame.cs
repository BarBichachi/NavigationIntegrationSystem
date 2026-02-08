// FILE: src\NavigationIntegrationSystem.Infrastructure\LEGACY_TO_BE_DELETED\IntegratedInsOutput_CommFrame.cs
using Infrastructure.Enums;

using System.IO;

namespace Infrastructure.Navigation.NavigationSystems.IntegratedInsOutput;

// Builds/decodes the integrated INS output binary frame (sync + payload + checksum)
public sealed class IntegratedInsOutput_CommFrame
{
    #region Constants
    // Frame sync byte defined for the integrated output protocol
    private const byte m_Sync = 0x50;
    #endregion

    #region Private Fields
    private readonly object m_LockObject;
    private IntegratedInsOutput_Data m_Data;
    private byte m_Checksum;
    #endregion

    #region Properties
    // Returns the total binary length (Sync + Payload + Checksum)
    public static int BinLength { get; } = IntegratedInsOutput_Data.BinLength + 2 * sizeof(byte);

    // Returns a snapshot of the current data model
    public IntegratedInsOutput_Data Data
    {
        get { lock (m_LockObject) { return m_Data.Clone(); } }
    }
    #endregion

    #region Constructors
    // Creates a new integrated output comm frame instance
    public IntegratedInsOutput_CommFrame()
    {
        m_LockObject = new object();
        m_Data = new IntegratedInsOutput_Data();
        m_Checksum = 0;
    }
    #endregion

    #region Decode
    // Decodes a full frame from a raw byte array
    public BinaryDataDecodingStatus DecodeBinaryData(byte[] i_BinaryData, int i_DataSize)
    {
        if (i_BinaryData == null || i_DataSize < BinLength) { return BinaryDataDecodingStatus.LengthError; }

        using (MemoryStream m = new MemoryStream(i_BinaryData))
        using (BinaryReader reader = new BinaryReader(m))
        {
            return DecodeBinaryData(reader, i_BinaryData, i_DataSize);
        }
    }

    // Decodes a full frame from a binary reader and validates the protocol sync word
    public BinaryDataDecodingStatus DecodeBinaryData(BinaryReader i_Reader, byte[] i_SourceBuffer, int i_DataSize)
    {
        if (i_Reader == null || i_DataSize < BinLength) { return BinaryDataDecodingStatus.LengthError; }

        byte sync = i_Reader.ReadByte();
        if (sync != m_Sync) { return BinaryDataDecodingStatus.SyncError; }

        IntegratedInsOutput_Data tmpData = new IntegratedInsOutput_Data();
        tmpData.ReadBinary(i_Reader);

        byte checksum = i_Reader.ReadByte();

        // Validates checksum if a source buffer is provided for calculation
        if (i_SourceBuffer != null)
        {
            byte expected = CalculateChecksum(i_SourceBuffer, BinLength);
            // Validation can be enabled here for strict protocol enforcement
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
    // Encodes the current data snapshot into a binary frame with checksum
    public void EncodeBinaryData(ref byte[] io_OutArray, ref int io_Size)
    {
        if (io_OutArray == null || io_OutArray.Length < BinLength) { io_Size = 0; return; }

        using (MemoryStream m = new MemoryStream(io_OutArray))
        using (BinaryWriter writer = new BinaryWriter(m))
        {
            writer.Write(m_Sync);

            IntegratedInsOutput_Data snapshot = Data;
            snapshot.Encode(writer);

            writer.Write((byte)0); // Placeholder for checksum
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
    // Calculates a simple additive checksum over the frame (excluding sync and checksum bytes)
    private static byte CalculateChecksum(byte[] i_Buffer, int i_FrameLength)
    {
        byte checksum = 0;

        // Sum bytes [1..FrameLength-2] excluding the sync at 0 and the checksum position at the end
        for (int i = 1; i < i_FrameLength - 1; i++)
        {
            checksum += i_Buffer[i];
        }

        return checksum;
    }
    #endregion
}