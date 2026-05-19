using NavigationIntegrationSystem.Core.Logging;
using NavigationIntegrationSystem.Core.Playback;
using NavigationIntegrationSystem.Devices.Validation;

using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NavigationIntegrationSystem.Infrastructure.Playback;

public sealed class CsvPlaybackService : IPlaybackService
{
    #region Constants
    private const int c_IndexScanBufferBytes = 64 * 1024;
    private const int c_LineReadBufferBytes = 4 * 1024;
    #endregion

    #region Private Fields
    private readonly ILogService m_LogService;
    private readonly object m_Lock = new();

    private FileStream? m_FileStream;
    private readonly List<long> m_LineOffsets = new();
    private string[] m_Headers = Array.Empty<string>();

    private CancellationTokenSource? m_PlaybackCts;
    private int m_CurrentLineIndex;
    private bool m_IsPlaying;
    private string? m_LoadedFilePath;
    private int m_Frequency = 10;
    private bool m_Loop;
    #endregion

    #region Properties
    public bool IsPlaying { get => m_IsPlaying; private set => m_IsPlaying = value; }
    public string? LoadedFilePath => m_LoadedFilePath;
    public int CurrentLineIndex => m_CurrentLineIndex;
    public int TotalLineCount => m_LineOffsets.Count;
    public int Frequency { get => m_Frequency; set { m_Frequency = Math.Clamp(value, 1, 100); } }
    public bool Loop { get => Volatile.Read(ref m_Loop); set => Volatile.Write(ref m_Loop, value); }
    #endregion

    #region Events
    public event EventHandler<PlaybackPacket>? PacketDispatched;
    public event EventHandler? StateChanged;
    public event EventHandler? PositionChanged;
    #endregion

    #region Constructors
    public CsvPlaybackService(ILogService i_LogService)
    {
        m_LogService = i_LogService;
    }
    #endregion

    #region Functions
    // Validates the file, opens it for streaming, parses the header, and lazily indexes byte offsets of each data line.
    // The FileStream stays open for the lifetime of the loaded file; data lines are read on demand during playback.
    public async Task LoadFileAsync(string i_FilePath)
    {
        if (!CsvPlaybackFileValidator.ValidateFile(i_FilePath, out string errorMessage))
        {
            throw new InvalidDataException(errorMessage);
        }

        Stop();
        CloseFileStream();

        FileStream fs = new FileStream(i_FilePath,
                                       FileMode.Open,
                                       FileAccess.Read,
                                       FileShare.Read,
                                       c_IndexScanBufferBytes,
                                       FileOptions.SequentialScan | FileOptions.Asynchronous);
        try
        {
            string? headerLine = ReadFirstLineSync(fs);
            string[] headers = CsvPlaybackSchema.ParseCsvLine(headerLine ?? string.Empty);
            List<long> offsets = await IndexLineOffsetsAsync(fs, fs.Position).ConfigureAwait(false);

            lock (m_Lock)
            {
                m_FileStream = fs;
                m_Headers = headers;
                m_LineOffsets.Clear();
                m_LineOffsets.AddRange(offsets);
                m_LoadedFilePath = i_FilePath;
                m_CurrentLineIndex = 0;
            }
        }
        catch
        {
            fs.Dispose();
            throw;
        }

        m_LogService.Info(nameof(CsvPlaybackService), $"Loaded playback file: {Path.GetFileName(i_FilePath)} ({TotalLineCount} frames)");
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    // Reads the first line of the file, including optional UTF-8 BOM handling. Leaves the stream positioned right after the line terminator.
    private static string? ReadFirstLineSync(FileStream i_Stream)
    {
        i_Stream.Seek(0, SeekOrigin.Begin);

        // Skip UTF-8 BOM (EF BB BF) if present
        Span<byte> bomBuffer = stackalloc byte[3];
        int bomRead = i_Stream.Read(bomBuffer);
        if (!(bomRead == 3 && bomBuffer[0] == 0xEF && bomBuffer[1] == 0xBB && bomBuffer[2] == 0xBF))
        {
            i_Stream.Seek(0, SeekOrigin.Begin);
        }

        long startPos = i_Stream.Position;
        string line = ReadLineSync(i_Stream);
        return (line.Length == 0 && i_Stream.Position == startPos) ? null : line;
    }

    // Single scan over the file from i_StartOffset, recording the byte offset of each non-empty line.
    // Empty lines (\r\n with nothing between) are skipped.
    private static async Task<List<long>> IndexLineOffsetsAsync(FileStream i_Stream, long i_StartOffset)
    {
        List<long> offsets = new List<long>(1024);
        i_Stream.Seek(i_StartOffset, SeekOrigin.Begin);

        byte[] buffer = ArrayPool<byte>.Shared.Rent(c_IndexScanBufferBytes);
        try
        {
            long currentOffset = i_StartOffset;
            bool atLineStart = true;
            int bytesRead;

            while ((bytesRead = await i_Stream.ReadAsync(buffer.AsMemory(0, buffer.Length)).ConfigureAwait(false)) > 0)
            {
                for (int i = 0; i < bytesRead; i++)
                {
                    byte b = buffer[i];
                    if (atLineStart && b != (byte)'\n' && b != (byte)'\r')
                    {
                        offsets.Add(currentOffset + i);
                        atLineStart = false;
                    }
                    if (b == (byte)'\n')
                    {
                        atLineStart = true;
                    }
                }
                currentOffset += bytesRead;
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }

        return offsets;
    }

    // Starts or resumes playback from the current line index.
    public void Play()
    {
        lock (m_Lock)
        {
            if (m_IsPlaying || TotalLineCount == 0) return;

            m_IsPlaying = true;
            m_PlaybackCts?.Cancel();
            m_PlaybackCts = new CancellationTokenSource();

            _ = PlaybackLoopAsync(m_PlaybackCts.Token);
        }
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    // Pauses playback, allowing it to be resumed later from the same index.
    public void Pause()
    {
        lock (m_Lock)
        {
            if (!m_IsPlaying) return;
            m_IsPlaying = false;
            m_PlaybackCts?.Cancel();
        }
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    // Stops playback and resets the current line index to the beginning of the file.
    public void Stop()
    {
        Pause();
        lock (m_Lock)
        {
            m_CurrentLineIndex = 0;
        }
        StateChanged?.Invoke(this, EventArgs.Empty);
        PositionChanged?.Invoke(this, EventArgs.Empty);
    }

    // Unloads the current playback file, closing the underlying FileStream.
    public void Unload()
    {
        Pause();
        CloseFileStream();
        lock (m_Lock)
        {
            m_LineOffsets.Clear();
            m_Headers = Array.Empty<string>();
            m_LoadedFilePath = null;
            m_CurrentLineIndex = 0;
        }
        StateChanged?.Invoke(this, EventArgs.Empty);
        PositionChanged?.Invoke(this, EventArgs.Empty);
    }

    // Seeks to a specific line index, clamping to valid range.
    public void Seek(int i_LineIndex)
    {
        lock (m_Lock)
        {
            m_CurrentLineIndex = Math.Clamp(i_LineIndex, 0, Math.Max(0, TotalLineCount - 1));
        }
        PositionChanged?.Invoke(this, EventArgs.Empty);
    }

    // Creates a new CSV file with the appropriate header plus one example data row (so the file passes CsvPlaybackFileValidator, which requires at least one data row).
    public async Task CreateTemplateAsync(string i_FilePath)
    {
        if (string.IsNullOrWhiteSpace(i_FilePath)) throw new ArgumentNullException(nameof(i_FilePath));

        string header = string.Join(",", CsvPlaybackSchema.Columns);
        string exampleRow = string.Join(",", CsvPlaybackSchema.Columns.Select(_ => "0"));
        string content = $"{header}{Environment.NewLine}{exampleRow}{Environment.NewLine}";

        await File.WriteAllTextAsync(i_FilePath, content, Encoding.UTF8).ConfigureAwait(false);
    }

    // Background playback loop. Uses Stopwatch-based deadline accounting so jitter does not accumulate at frame rates that don't divide evenly into 1000ms (e.g. 60Hz).
    private async Task PlaybackLoopAsync(CancellationToken i_Token)
    {
        Stopwatch sw = Stopwatch.StartNew();
        long nextDeadlineTicks = 0;

        try
        {
            while (!i_Token.IsCancellationRequested)
            {
                if (!TryReadCurrentLine(out string line))
                {
                    // EOF: if Loop is set and the file has any data, rewind and continue; otherwise stop.
                    if (Loop && TotalLineCount > 0)
                    {
                        lock (m_Lock) { m_CurrentLineIndex = 0; }
                        PositionChanged?.Invoke(this, EventArgs.Empty);
                        continue;
                    }
                    SetPlayingState(false);
                    break;
                }

                PlaybackPacket? packet = ParsePacket(line);
                if (packet != null) { PacketDispatched?.Invoke(this, packet); }
                IncrementIndex();
                PositionChanged?.Invoke(this, EventArgs.Empty);

                // Recompute per-frame ticks each iteration so a mid-playback Frequency change takes effect immediately
                long ticksPerFrame = Stopwatch.Frequency / Math.Max(1, Frequency);
                nextDeadlineTicks += ticksPerFrame;

                long remainingTicks = nextDeadlineTicks - sw.ElapsedTicks;

                // If we have fallen behind by more than ~100ms (e.g. UI thread stall, GC), reset baseline so we resume the target rate instead of trying to catch up infinitely
                if (remainingTicks < -Stopwatch.Frequency / 10)
                {
                    nextDeadlineTicks = sw.ElapsedTicks;
                    continue;
                }

                if (remainingTicks > 0)
                {
                    int sleepMs = (int)(remainingTicks * 1000L / Stopwatch.Frequency);
                    if (sleepMs > 0) { await Task.Delay(sleepMs, i_Token).ConfigureAwait(false); }
                }
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            m_LogService.Error(nameof(CsvPlaybackService), "Playback loop error", ex);
            SetPlayingState(false);
        }
        finally
        {
            StateChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    // Reads the current line by seeking to its indexed byte offset. Returns false if EOF or no file loaded.
    private bool TryReadCurrentLine(out string o_Line)
    {
        lock (m_Lock)
        {
            if (m_FileStream == null || m_CurrentLineIndex >= m_LineOffsets.Count)
            {
                o_Line = string.Empty;
                return false;
            }

            long offset = m_LineOffsets[m_CurrentLineIndex];
            m_FileStream.Seek(offset, SeekOrigin.Begin);
            o_Line = ReadLineSync(m_FileStream);
            return true;
        }
    }

    // Synchronous line read used inside the playback loop (under m_Lock). Reads until \n or EOF, excluding \r.
    private static string ReadLineSync(FileStream i_Stream)
    {
        byte[] rent = ArrayPool<byte>.Shared.Rent(c_LineReadBufferBytes);
        try
        {
            int written = 0;
            while (true)
            {
                int b = i_Stream.ReadByte();
                if (b == -1) { break; }
                if (b == '\n') { break; }
                if (b == '\r') { continue; }

                if (written == rent.Length)
                {
                    byte[] grown = ArrayPool<byte>.Shared.Rent(rent.Length * 2);
                    Buffer.BlockCopy(rent, 0, grown, 0, written);
                    ArrayPool<byte>.Shared.Return(rent);
                    rent = grown;
                }
                rent[written++] = (byte)b;
            }

            return written == 0 ? string.Empty : Encoding.UTF8.GetString(rent, 0, written);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(rent);
        }
    }

    // Thread-safe index increment
    private void IncrementIndex()
    {
        lock (m_Lock)
        {
            m_CurrentLineIndex++;
        }
    }

    // Thread-safe state setter
    private void SetPlayingState(bool i_IsPlaying)
    {
        lock (m_Lock)
        {
            m_IsPlaying = i_IsPlaying;
        }
    }

    // Parses a CSV line into a PlaybackPacket, matching headers to values
    private PlaybackPacket? ParsePacket(string i_Line)
    {
        string[] cells = CsvPlaybackSchema.ParseCsvLine(i_Line);
        if (cells.Length > m_Headers.Length) return null;

        Dictionary<string, double> values = new Dictionary<string, double>(cells.Length);

        for (int i = 0; i < cells.Length && i < m_Headers.Length; i++)
        {
            string header = m_Headers[i];
            if (double.TryParse(cells[i], NumberStyles.Any, CultureInfo.InvariantCulture, out double val))
            {
                values[header] = val;
            }
        }

        return new PlaybackPacket(values);
    }

    // Closes and clears the underlying file stream. Safe to call when already closed.
    private void CloseFileStream()
    {
        FileStream? fs;
        lock (m_Lock)
        {
            fs = m_FileStream;
            m_FileStream = null;
        }
        fs?.Dispose();
    }

    // Clean up resources when the service is disposed.
    public void Dispose()
    {
        m_PlaybackCts?.Cancel();
        m_PlaybackCts?.Dispose();
        CloseFileStream();
    }
    #endregion
}
