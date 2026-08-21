namespace Muno.Services;

/// <summary>
/// Runs the external PhaseLimiter mastering engine.
/// </summary>
public interface IPhaseLimiterEngine
{
    /// <summary>
    /// Runs one mastering operation and reports progress from the engine's stdout.
    /// </summary>
    /// <param name="request">The typed mastering request.</param>
    /// <param name="progress">The progress reporter receiving values from 0 to 1.</param>
    /// <param name="cancellationToken">The token used to cancel the process tree.</param>
    /// <returns>
    /// A successful result only when the process exits with code 0 and the output file exists;
    /// otherwise, a failed result. Cancellation terminates the complete process tree and returns
    /// a failed result instead of throwing.
    /// </returns>
    Task<Result> RunAsync(
        MasteringRequest request,
        IProgress<double> progress,
        CancellationToken cancellationToken = default);
}
