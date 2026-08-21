namespace Muno.Models;

/// <summary>
/// Specifies the optimization algorithm used by the mastering engine.
/// </summary>
public enum OptimizationAlgorithm
{
    /// <summary>
    /// Differential evolution (<c>de</c>).
    /// </summary>
    De,

    /// <summary>
    /// Nelder-Mead (<c>nm</c>).
    /// </summary>
    Nm,

    /// <summary>
    /// Particle swarm optimization (<c>pso</c>).
    /// </summary>
    Pso,

    /// <summary>
    /// Differential evolution with PRMM (<c>de_prmm</c>).
    /// </summary>
    DePrmm,

    /// <summary>
    /// Particle swarm optimization with dynamic velocity modulation (<c>pso_dv</c>).
    /// </summary>
    PsoDv
}
