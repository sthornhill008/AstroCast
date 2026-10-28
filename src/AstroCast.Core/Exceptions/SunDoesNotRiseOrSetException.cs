namespace AstroCast.Core.Exceptions;

/// <summary>
/// Exception thrown when the Sun does not rise or does not set at the requested place
/// within the Swiss Ephemeris search after the requested instant: the polar day or the
/// polar night.
/// </summary>
/// <remarks>
/// <para>
/// <c>swe_rise_trans()</c> reports this case by returning <c>-2</c> ("function return
/// value -2 means that the body does not rise or set", <c>swecl.c</c>), not as an error.
/// It is a condition of the place and date, not a failure of the calculation: where it
/// occurs the planetary hour is undefined, and the chart engine omits it.
/// </para>
/// <para>
/// It derives from <see cref="EphemerisCalculationException"/>, so every existing catch
/// of that type still catches it.
/// </para>
/// </remarks>
public sealed class SunDoesNotRiseOrSetException : EphemerisCalculationException
{
    /// <summary>
    /// Initialises a new instance of <see cref="SunDoesNotRiseOrSetException"/>
    /// with the specified message.
    /// </summary>
    /// <param name="message">The message describing the place and instant.</param>
    public SunDoesNotRiseOrSetException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initialises a new instance of <see cref="SunDoesNotRiseOrSetException"/>
    /// with the specified message and inner exception.
    /// </summary>
    /// <param name="message">The message describing the place and instant.</param>
    /// <param name="innerException">The exception that caused this one.</param>
    public SunDoesNotRiseOrSetException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
