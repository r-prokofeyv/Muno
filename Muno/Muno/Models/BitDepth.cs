namespace Muno.Models;

/// <summary>
/// Specifies the output bit depth for WAV files. This setting is relevant only when
/// <see cref="OutputFormat.Wav"/> is selected.
/// </summary>
public enum BitDepth
{
    /// <summary>
    /// 16-bit integer PCM (<c>16</c>).
    /// </summary>
    Bit16 = 16,

    /// <summary>
    /// 24-bit integer PCM (<c>24</c>).
    /// </summary>
    Bit24 = 24,

    /// <summary>
    /// 32-bit floating-point PCM (<c>32</c>).
    /// </summary>
    Bit32Float = 32
}
