using System.Diagnostics;
using System.Globalization;
using System.Text;
using Microsoft.Extensions.Logging;
using Muno.Models;

namespace Muno.Services;

/// <summary>
/// Executes the external PhaseLimiter mastering engine.
/// </summary>
public sealed class PhaseLimiterEngine : IPhaseLimiterEngine
{
    private readonly ILogger<PhaseLimiterEngine> _logger;

    public PhaseLimiterEngine(ILogger<PhaseLimiterEngine> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<Result> RunAsync(
        MasteringRequest request,
        IProgress<double> progress,
        CancellationToken cancellationToken = default)
    {
        string? stagingDirectory = null;

        try
        {
            var phaseLimiterExePath = Path.GetFullPath(request.PhaseLimiterExePath);
            if (!File.Exists(phaseLimiterExePath))
            {
                _logger.LogError("PhaseLimiter engine executable was not found: {Path}", phaseLimiterExePath);
                return Result.Failure("PhaseLimiter engine executable was not found.");
            }

            var inputPath = Path.GetFullPath(request.InputPath);
            var outputPath = Path.GetFullPath(request.OutputPath);
            var ffmpegPath = Path.GetFullPath(request.FfmpegPath);
            var soundQualityCachePath = Path.GetFullPath(request.SoundQualityCachePath);
            var tempDirectory = Path.GetFullPath(request.TempDirectory);

            Directory.CreateDirectory(tempDirectory);
            var outputDirectory = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(outputDirectory))
            {
                Directory.CreateDirectory(outputDirectory);
            }

            stagingDirectory = CreateStagingDirectory();
            var processInputPath = Path.Combine(stagingDirectory, $"input{Path.GetExtension(inputPath)}");
            var processOutputPath = Path.Combine(stagingDirectory, $"output{Path.GetExtension(outputPath)}");
            var processFfmpegPath = Path.Combine(stagingDirectory, "ffmpeg.exe");
            var processSoundQualityCachePath = Path.Combine(stagingDirectory, "sound_quality2_cache");
            var processTempDirectory = Path.Combine(stagingDirectory, "tmp");

            File.Copy(inputPath, processInputPath, overwrite: true);
            File.Copy(ffmpegPath, processFfmpegPath, overwrite: true);
            File.Copy(soundQualityCachePath, processSoundQualityCachePath, overwrite: true);
            Directory.CreateDirectory(processTempDirectory);

            var argumentList = new List<string>
            {
                "--mastering=true",
                "--mastering_mode=mastering5",
                "--mastering5_optimization_max_eval_count=40000",
                "--pre_compression=true",
                "--pre_compression_threshold=6",
                "--pre_compression_mean_sec=0.2",
                "--low_cut_freq=20",
                "--high_cut_freq=20000",
                "--limiting_mode=phase",
                "--ceiling_mode=true_peak",
                "--true_peak_oversample=4",
                $"--reference_mode=loudness",
                $"--reference={FormatInvariant(request.Settings.TargetLoudness)}",
                $"--mastering5_mastering_level={FormatInvariant(request.Settings.MasteringIntensity)}",
                $"--erb_eval_func_weighting={request.Settings.PreserveBass.ToString().ToLowerInvariant()}",
                $"--mastering5_optimization_algorithm={GetOptimizationAlgorithm(request.Settings.OptimizationAlgorithm)}",
                $"--ceiling={FormatInvariant(request.Settings.TruePeakCeiling)}",
                $"--output_format={GetOutputFormat(request.Settings.OutputFormat)}",
                $"--sample_rate={request.Settings.SampleRate.ToString(CultureInfo.InvariantCulture)}",
                $"--input={processInputPath}",
                $"--output={processOutputPath}",
                $"--ffmpeg={processFfmpegPath}",
                $"--sound_quality2_cache={processSoundQualityCachePath}",
                $"--tmp={processTempDirectory}"
            };

            if (request.Settings.OutputFormat == OutputFormat.Wav)
            {
                argumentList.Insert(
                    argumentList.FindIndex(argument => argument.StartsWith("--sample_rate=", StringComparison.Ordinal)),
                    $"--bit_depth={((int)request.Settings.BitDepth).ToString(CultureInfo.InvariantCulture)}");
            }

            _logger.LogDebug(
                "Starting PhaseLimiter with arguments: {Arguments}",
                string.Join(" ", argumentList));

            var startInfo = new ProcessStartInfo
            {
                FileName = phaseLimiterExePath,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            foreach (var argument in argumentList)
            {
                startInfo.ArgumentList.Add(argument);
            }

            using var process = new Process { StartInfo = startInfo };
            if (!process.Start())
            {
                _logger.LogError("Failed to start PhaseLimiter process.");
                return Result.Failure("Failed to start the mastering engine.");
            }

            using var cancellationRegistration = cancellationToken.Register(() =>
            {
                try
                {
                    process.Kill(entireProcessTree: true);
                }
                catch
                {
                }
            });

            var stderr = new StringBuilder();
            var stdoutTask = ReadStandardOutputAsync(process, progress);
            var stderrTask = ReadStandardErrorAsync(process, stderr);
            var wasCancelled = false;

            try
            {
                await process.WaitForExitAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                wasCancelled = true;
            }

            await Task.WhenAll(stdoutTask, stderrTask);

            if (wasCancelled)
            {
                _logger.LogInformation("PhaseLimiter mastering was cancelled.");
                return Result.Failure("Mastering was cancelled.");
            }

            var exitCode = process.ExitCode;
            var stderrText = stderr.ToString().Trim();
            if (exitCode == 0 && File.Exists(processOutputPath))
            {
                File.Copy(processOutputPath, outputPath, overwrite: true);
                _logger.LogInformation("PhaseLimiter mastering completed successfully: {OutputPath}", outputPath);
                return Result.Success();
            }

            _logger.LogError(
                "PhaseLimiter mastering failed. Exit code: {ExitCode}. Stderr: {StandardError}",
                exitCode,
                stderrText);
            return Result.Failure($"Mastering failed (exit code {exitCode}).");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error while running PhaseLimiter mastering.");
            return Result.Failure("An unexpected error occurred while mastering the audio.");
        }
        finally
        {
            if (stagingDirectory != null)
            {
                try
                {
                    Directory.Delete(stagingDirectory, recursive: true);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to clean up PhaseLimiter staging directory: {Directory}", stagingDirectory);
                }
            }
        }
    }

    private static async Task ReadStandardOutputAsync(Process process, IProgress<double> progress)
    {
        while (await process.StandardOutput.ReadLineAsync() is { } line)
        {
            const string prefix = "progression:";
            if (!line.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var valueText = line[prefix.Length..].Trim();
            if (double.TryParse(valueText, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
            {
                progress.Report(Math.Clamp(value, 0d, 1d));
            }
        }
    }

    private static async Task ReadStandardErrorAsync(Process process, StringBuilder stderr)
    {
        while (await process.StandardError.ReadLineAsync() is { } line)
        {
            stderr.AppendLine(line);
        }
    }

    private static string FormatInvariant(double value) => value.ToString(CultureInfo.InvariantCulture);

    private static string CreateStagingDirectory()
    {
        var stagingRoot = Path.Combine(Path.GetTempPath(), "MunoPhaseLimiter");
        Directory.CreateDirectory(stagingRoot);
        if (!IsShellSafePath(stagingRoot))
        {
            stagingRoot = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.Windows),
                "Temp",
                "MunoPhaseLimiter");
            Directory.CreateDirectory(stagingRoot);
        }

        var stagingDirectory = Path.Combine(stagingRoot, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(stagingDirectory);
        if (!IsShellSafePath(stagingDirectory))
        {
            throw new InvalidOperationException("Unable to create a shell-safe PhaseLimiter staging directory.");
        }

        return stagingDirectory;
    }

    private static bool IsShellSafePath(string path) =>
        path.All(character => char.IsLetterOrDigit(character) || character is ':' or '\\' or '.' or '_' or '-');

    private static string GetOptimizationAlgorithm(OptimizationAlgorithm algorithm) => algorithm switch
    {
        OptimizationAlgorithm.De => "de",
        OptimizationAlgorithm.Nm => "nm",
        OptimizationAlgorithm.Pso => "pso",
        OptimizationAlgorithm.DePrmm => "de_prmm",
        OptimizationAlgorithm.PsoDv => "pso_dv",
        _ => throw new ArgumentOutOfRangeException(nameof(algorithm), algorithm, "Unknown optimization algorithm.")
    };

    private static string GetOutputFormat(OutputFormat outputFormat) => outputFormat switch
    {
        OutputFormat.Wav => "wav",
        OutputFormat.Mp3 => "mp3",
        OutputFormat.Aac => "aac",
        _ => throw new ArgumentOutOfRangeException(nameof(outputFormat), outputFormat, "Unknown output format.")
    };
}
