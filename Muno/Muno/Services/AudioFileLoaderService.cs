using Microsoft.Extensions.Logging;
using Muno.Models;
using System.IO;

namespace Muno.Services;

/// <summary>
/// Orchestrates loading an audio file: validating its extension, decoding it, and generating waveform data.
/// </summary>
public sealed class AudioFileLoaderService : IAudioFileLoaderService
{
    /// <summary>
    /// Default number of peak pairs to generate for waveform visualization.
    /// </summary>
    private const int DefaultTargetPeakCount = 2000;

    private static readonly string[] SupportedExtensions = [".wav", ".mp3", ".aac"];

    private readonly IAudioDecodingService _audioDecodingService;
    private readonly IWaveformGeneratorService _waveformGeneratorService;
    private readonly ILogger<AudioFileLoaderService> _logger;

    public AudioFileLoaderService(
        IAudioDecodingService audioDecodingService,
        IWaveformGeneratorService waveformGeneratorService,
        ILogger<AudioFileLoaderService> logger)
    {
        _audioDecodingService = audioDecodingService;
        _waveformGeneratorService = waveformGeneratorService;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<Result<LoadedAudio>> LoadAudioFileAsync(string filePath, CancellationToken cancellationToken = default)
    {
        var extension = Path.GetExtension(filePath);
        if (string.IsNullOrEmpty(extension) || !SupportedExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
        {
            _logger.LogWarning("Unsupported file extension: {Extension} for file {FilePath}", extension, filePath);
            return Result<LoadedAudio>.Failure($"Unsupported file type '{extension}'. Supported types are: WAV, MP3, AAC.");
        }

        var decodeResult = await _audioDecodingService.DecodeAudioFileAsync(filePath, cancellationToken);
        if (!decodeResult.IsSuccess || decodeResult.Value == null)
        {
            return Result<LoadedAudio>.Failure(decodeResult.ErrorMessage ?? "Failed to decode audio file");
        }

        var audioFile = decodeResult.Value;

        var waveformResult = await _waveformGeneratorService.GenerateWaveformAsync(audioFile, DefaultTargetPeakCount, cancellationToken);
        if (!waveformResult.IsSuccess || waveformResult.Value == null)
        {
            return Result<LoadedAudio>.Failure(waveformResult.ErrorMessage ?? "Failed to generate waveform data");
        }

        var loadedAudio = new LoadedAudio
        {
            AudioFile = audioFile,
            WaveformData = waveformResult.Value
        };

        return Result<LoadedAudio>.Success(loadedAudio);
    }
}
