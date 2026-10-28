namespace AstroCast.Core.Exceptions;

/// <summary>
/// Exception thrown when the Swiss Ephemeris library returns an error
/// during a planetary or house calculation.
/// </summary>
/// <remarks>
/// The <see cref="Exception.Message"/> contains the error string returned
/// by the native <c>swe_calc_ut()</c> or related function. It is not sealed:
/// <see cref="SunDoesNotRiseOrSetException"/> derives from it, so a catch of
/// this type also catches that one.
/// </remarks>
public class EphemerisCalculationException : Exception
{
    /// <summary>
    /// Initialises a new instance of <see cref="EphemerisCalculationException"/>
    /// with the specified error message.
    /// </summary>
    /// <param name="message">The error message from the Swiss Ephemeris library.</param>
    public EphemerisCalculationException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initialises a new instance of <see cref="EphemerisCalculationException"/>
    /// with the specified error message and inner exception.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The exception that caused this error.</param>
    public EphemerisCalculationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
