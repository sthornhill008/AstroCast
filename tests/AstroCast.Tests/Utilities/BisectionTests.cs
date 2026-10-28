using AstroCast.Core.Utilities;

using FluentAssertions;

namespace AstroCast.Tests.Utilities;

/// <summary>
/// <see cref="Bisection"/> (branch Primary, 1.5.0), the bisection the solar arc hits and the solar-arc key share.
/// </summary>
[Trait("Category", "Unit")]
public sealed class BisectionTests
{
    /// <summary>The square root of 2 from x² on [0, 4], to a billionth.</summary>
    [Fact]
    public void Solve_FindsTheRootToTheTolerance()
    {
        double root = Bisection.Solve(x => x * x, 2.0, 0.0, 4.0, 1e-9);

        root.Should().BeApproximately(Math.Sqrt(2.0), 1e-9);
    }
}
