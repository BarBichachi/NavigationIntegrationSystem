using NavigationIntegrationSystem.Core.Logging;
using NavigationIntegrationSystem.Core.Playback;

using System;
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
    #region Private Fields
    private readonly ILogService m_LogService;
    private readonly List<string> m_FileLines = new();
    private string[] m_Headers = Array.Empty<string>();

    private CancellationTokenSource? m_PlaybackCts;
    private readonly object m_Lock = new();

    private int m_CurrentLineIndex;
    private bool m_IsPlaying;
    private string? m_LoadedFilePath;
    private int m_Frequency = 10;

    private static readonly string[] c_CsvSchema =
    {
        "PositionLatValue", "PositionLonValue", "PositionAltValue",
        "EulerRollValue", "EulerPitchValue", "EulerAzimuthValue",
        "EulerRollRateValue", "EulerPitchRateValue", "EulerAzimuthRateValue",
        "VelocityNorthValue", "VelocityEastValue", "VelocityDownValue", "VelocityTotalValue",
        "CourseValue", "StatusValue"
    };
    #endregion

    #region Properties
    public bool IsPlaying { get => m_IsPlaying; private set => m_IsPlaying = value; }
    public string? LoadedFilePath => m_LoadedFilePath;
    public int CurrentLineIndex => m_CurrentLineIndex;
    public int TotalLineCount => m_FileLines.Count;
    public int Frequency { get => m_Frequency; set { m_Frequency = Math.Clamp(value, 1, 100); } }
    #endregion

    #region Events
    public event EventHandler<PlaybackPacket>? PacketDispatched;
    public event EventHandler? StateChanged;
    #endregion

    #region Constructors
    public CsvPlaybackService(ILogService i_LogService)
    {
        m_LogService = i_LogService;
    }
    #endregion

    #region Functions
    // Loads the CSV file and prepares for playback.
    public async Task LoadFileAsync(string i_FilePath)
    {
        if (string.IsNullOrWhiteSpace(i_FilePath) || !File.Exists(i_FilePath))
        {
            throw new FileNotFoundException("Playback file not found", i_FilePath);
        }

        Stop();

        lock (m_Lock)
        {
            m_FileLines.Clear();
            m_Headers = Array.Empty<string>();
        }

        string[] lines = await File.ReadAllLinesAsync(i_FilePath).ConfigureAwait(false);

        if (lines.Length < 2)
        {
            throw new InvalidDataException("CSV file must contain a header row and at least one data row.");
        }

        lock (m_Lock)
        {
            m_Headers = ParseCsvLine(lines[0]);
            m_FileLines.AddRange(lines.Skip(1).Where(l => !string.IsNullOrWhiteSpace(l)));
            m_LoadedFilePath = i_FilePath;
            m_CurrentLineIndex = 0;
        }

        m_LogService.Info(nameof(CsvPlaybackService), $"Loaded playback file: {Path.GetFileName(i_FilePath)} ({TotalLineCount} frames)");
        StateChanged?.Invoke(this, EventArgs.Empty);
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
    }

    // Seeks to a specific line index, clamping to valid range.
    public void Seek(int i_LineIndex)
    {
        lock (m_Lock)
        {
            m_CurrentLineIndex = Math.Clamp(i_LineIndex, 0, TotalLineCount - 1);
        }
    }

    // Creates a new CSV file with the appropriate header for recording playback data.
    public async Task CreateTemplateAsync(string i_FilePath)
    {
        if (string.IsNullOrWhiteSpace(i_FilePath)) throw new ArgumentNullException(nameof(i_FilePath));
        string header = string.Join(",", c_CsvSchema);
        await File.WriteAllTextAsync(i_FilePath, header, Encoding.UTF8).ConfigureAwait(false);
    }

    // The main orchestrator for the playback thread
    private async Task PlaybackLoopAsync(CancellationToken i_Token)
    {
        try
        {
            while (!i_Token.IsCancellationRequested)
            {
                // Calculate delay based on frequency (e.g., 10Hz = 100ms)
                int delayMs = 1000 / Frequency;

                if (!TryGetNextLine(out string line))
                {
                    SetPlayingState(false);
                    break;
                }

                PlaybackPacket? packet = ParsePacket(line);

                if (packet != null)
                {
                    PacketDispatched?.Invoke(this, packet);
                }

                IncrementIndex();

                await Task.Delay(delayMs, i_Token).ConfigureAwait(false);
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

    // Safely retrieves the next line or indicates EOF
    private bool TryGetNextLine(out string o_Line)
    {
        lock (m_Lock)
        {
            if (m_CurrentLineIndex >= TotalLineCount)
            {
                o_Line = string.Empty;
                return false;
            }

            o_Line = m_FileLines[m_CurrentLineIndex];
            return true;
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
        string[] cells = ParseCsvLine(i_Line);
        // We allow partial rows, but matching headers is safer
        if (cells.Length > m_Headers.Length) return null;

        var values = new Dictionary<string, double>();

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

    // Simple CSV line parser that trims whitespace and handles empty lines
    private static string[] ParseCsvLine(string i_Line)
    {
        if (string.IsNullOrEmpty(i_Line)) return Array.Empty<string>();
        return i_Line.Split(',').Select(s => s.Trim()).ToArray();
    }

    // Clean up resources when the service is disposed.
    public void Dispose()
    {
        m_PlaybackCts?.Cancel();
        m_PlaybackCts?.Dispose();
    }
    #endregion
}