namespace AstroCast.Core.Utilities;

/// <summary>
/// Bisection on an increasing function (branch Primary, 1.5.0): the solar arc hits and the solar-arc key of the
/// primary directions each invert a monotonic function of time with it.
/// </summary>
internal static class Bisection
{
    /// <summary>
    /// The argument at which an increasing function reaches <paramref name="target"/>: the bracket is halved, keeping
    /// the half the target lies in, until it is no wider than <paramref name="tolerance"/>, and its middle is returned.
    /// </summary>
    /// <param name="function">The function, increasing across the bracket.</param>
    /// <param name="target">The value sought, between the function's values at the bracket's two ends.</param>
    /// <param name="low">The bracket's start.</param>
    /// <param name="high">The bracket's end, after <paramref name="low"/>.</param>
    /// <param name="tolerance">The bracket's width at which the search stops.</param>
    /// <returns>The middle of the final bracket.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="function"/> is null.</exception>
    internal static double Solve(Func<double, double> function, double target, double low, double high, double tolerance)
    {
        ArgumentNullException.ThrowIfNull(function);

        while (high - low > tolerance)
        {
            double mid = (low + high) / 2.0;
            if (function(mid) < target)
                low = mid;
            else
                high = mid;
        }

        return (low + high) / 2.0;
    }
}
