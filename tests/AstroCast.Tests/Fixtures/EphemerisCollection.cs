namespace AstroCast.Tests.Fixtures;

/// <summary>
/// xUnit collection definition that shares a single <see cref="EphemerisFixture"/>
/// across all test classes in the "Ephemeris" collection. This ensures that
/// <c>swe_close()</c> is called exactly once after all tests complete.
/// </summary>
[CollectionDefinition("Ephemeris")]
public sealed class EphemerisCollection : ICollectionFixture<EphemerisFixture>
{
    // This class has no code — it simply associates the collection name
    // with the fixture type via the ICollectionFixture<T> interface.
}
