using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Muno.Models;

namespace Muno.Services;

/// <summary>
/// Exports audio files to supported formats using ffmpeg.
/// </summary>
public sealed class AudioExportService : IAudioExportService
{
    private readonly ILogger<AudioExportService> _logger;

    public AudioExportService(ILogger<AudioExportService> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<Result> ExportAsync(
        string sourcePath,
        string destinationPath,
        OutputFormat format,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (!File.Exists(sourcePath))
            {
                return Result.Failure("The mastered audio file is no longer available.");
            }

            var ffmpegPath = Path.Combine(AppContext.BaseDirectory, "Libs", "ffmpeg.exe");
            if (!File.Exists(ffmpegPath))
            {
                _logger.LogError("ffmpeg executable was not found: {Path}", ffmpegPath);
                return Result.Failure("The audio export engine is not available.");
            }

            var destinationDirectory = Path.GetDirectoryName(destinationPath);
            if (string.IsNullOrWhiteSpace(destinationDirectory))
            {
                return Result.Failure("The export destination is invalid.");
            }

            Directory.CreateDirectory(destinationDirectory);

            var startInfo = new ProcessStartInfo
            {
                FileName = ffmpegPath,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            startInfo.ArgumentList.Add("-y");
            startInfo.ArgumentList.Add("-i");
            startInfo.ArgumentList.Add(sourcePath);
            startInfo.ArgumentList.Add("-vn");
            AddFormatArguments(startInfo.ArgumentList, format);
            startInfo.ArgumentList.Add(destinationPath);

            using var process = new Process { StartInfo = startInfo };
            if (!process.Start())
            {
                _logger.LogError("Failed to start ffmpeg export process.");
                return Result.Failure("Failed to start the audio export.");
            }

            using var cancellationRegistration = cancellationToken.Register(() =>
            {
                try
                {
                    process.Kill(entireProcessTree: true);
                }
                catch (InvalidOperationException)
                {
                }
            });

            var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
            var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken);
            var stderr = await stderrTask;
            await stdoutTask;

            if (process.ExitCode != 0)
            {
                _logger.LogWarning(
                    "ffmpeg export failed with exit code {ExitCode}: {Error}",
                    process.ExitCode,
                    stderr);
                return Result.Failure("Failed to export the mastered audio.");
            }

            return Result.Success();
        }
        catch (OperationCanceledException)
        {
            return Result.Failure("Audio export was cancelled.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error while exporting mastered audio.");
            return Result.Failure("Failed to export the mastered audio.");
        }
    }

    private static void AddFormatArguments(IList<string> arguments, OutputFormat format)
    {
        switch (format)
        {
            case OutputFormat.Wav:
                arguments.Add("-c:a");
                arguments.Add("pcm_s16le");
                break;
            case OutputFormat.Mp3:
                arguments.Add("-c:a");
                arguments.Add("libmp3lame");
                arguments.Add("-q:a");
                arguments.Add("2");
                break;
            case OutputFormat.Aac:
                arguments.Add("-c:a");
                arguments.Add("aac");
                arguments.Add("-b:a");
                arguments.Add("192k");
                arguments.Add("-f");
                arguments.Add("adts");
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(format), format, "Unsupported export format.");
        }
    }
}
