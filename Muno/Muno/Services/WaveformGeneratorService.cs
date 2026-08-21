using Microsoft.Extensions.Logging;
using Muno.Models;

namespace Muno.Services;

/// <summary>
/// Generates downsampled min/max peak waveform data from decoded PCM audio on a background thread.
/// </summary>
public sealed class WaveformGeneratorService : IWaveformGeneratorService
{
    private readonly ILogger<WaveformGeneratorService> _logger;

    public WaveformGeneratorService(ILogger<WaveformGeneratorService> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc/>
    public Task<Result<WaveformData>> GenerateWaveformAsync(AudioFile audioFile, int targetPeakCount, CancellationToken cancellationToken = default)
    {
        return Task.Run(() =>
        {
            try
            {
                if (targetPeakCount <= 0)
                {
                    return Result<WaveformData>.Failure("Target peak count must be greater than zero");
                }

                var pcm = audioFile.PcmData;
                var channels = audioFile.Channels <= 0 ? 1 : audioFile.Channels;
                var totalFrames = pcm.Length / channels;

                if (totalFrames == 0)
                {
                    return Result<WaveformData>.Failure("Audio file contains no PCM samples");
                }

                var peakCount = Math.Min(targetPeakCount, totalFrames);
                var minPeaks = new float[peakCount];
                var maxPeaks = new float[peakCount];

                var framesPerPeak = (double)totalFrames / peakCount;

                for (var peakIndex = 0; peakIndex < peakCount; peakIndex++)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var startFrame = (int)(peakIndex * framesPerPeak);
                    var endFrame = (int)Math.Min((peakIndex + 1) * framesPerPeak, totalFrames);
                    if (endFrame <= startFrame)
                    {
                        endFrame = Math.Min(startFrame + 1, totalFrames);
                    }

                    var min = float.MaxValue;
                    var max = float.MinValue;

                    for (var frame = startFrame; frame < endFrame; frame++)
                    {
                        // Average across channels to get a single sample value per frame
                        var sampleSum = 0f;
                        for (var channel = 0; channel < channels; channel++)
                        {
                            var index = (frame * channels) + channel;
                            if (index < pcm.Length)
                            {
                                sampleSum += pcm[index];
                            }
                        }

                        var sample = sampleSum / channels;
                        if (sample < min)
                        {
                            min = sample;
                        }

                        if (sample > max)
                        {
                            max = sample;
                        }
                    }

                    minPeaks[peakIndex] = min;
                    maxPeaks[peakIndex] = max;
                }

                var waveformData = new WaveformData
                {
                    MinPeaks = minPeaks,
                    MaxPeaks = maxPeaks,
                    TotalSampleCount = totalFrames,
                    Channels = channels
                };

                _logger.LogInformation("Generated waveform data with {PeakCount} peaks for {FileName}",
                    peakCount, audioFile.FileName);

                return Result<WaveformData>.Success(waveformData);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating waveform data for {FileName}", audioFile.FileName);
                return Result<WaveformData>.Failure($"Error generating waveform data: {ex.Message}");
            }
        }, cancellationToken);
    }
}
