namespace Nocturne.API.Services.Profiles.Resolvers;

/// <summary>
/// <see cref="Caching.ITenantCache"/> keys for the version of a profile's schedule or settings
/// active at a moment. The moment is part of the key, to the minute, because a profile's rows are
/// versioned: a key without it would answer every moment with whichever version was read first.
/// </summary>
internal static class ScheduleCacheKey
{
    public static string For(string family, string profileName, DateTime timestamp) =>
        $"{family}:{profileName}:{timestamp.Ticks / TimeSpan.TicksPerMinute}";
}
