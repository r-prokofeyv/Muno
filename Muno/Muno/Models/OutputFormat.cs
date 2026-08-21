namespace Muno.Models;

/// <summary>
/// Specifies the output audio format used by the mastering engine.
/// </summary>
public enum OutputFormat
{
    /// <summary>
    /// Waveform Audio File Format (<c>wav</c>).
    /// </summary>
    Wav,

    /// <summary>
    /// MPEG Audio Layer III (<c>mp3</c>).
    /// </summary>
    Mp3,

    /// <summary>
    /// Advanced Audio Coding (<c>aac</c>).
    /// </summary>
    Aac
}
