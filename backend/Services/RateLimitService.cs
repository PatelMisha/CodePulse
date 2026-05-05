using StackExchange.Redis;

namespace backend.Services;

// Checks if a user has exceeded 10 runs per minute
// Uses Redis so limits work even if we run multiple backend instances
public class RateLimitService
{
    private readonly IDatabase _redis;
    private const int MaxRunsPerMinute = 10;

    public RateLimitService(IConnectionMultiplexer redis)
    {
        _redis = redis.GetDatabase();
    }

    public async Task<bool> IsAllowedAsync(string clientIp)
    {
        var key = $"ratelimit:{clientIp}:{DateTime.UtcNow:yyyyMMddHHmm}";
        var count = await _redis.StringIncrementAsync(key);
        if (count == 1)
            await _redis.KeyExpireAsync(key, TimeSpan.FromMinutes(1));
        return count <= MaxRunsPerMinute;
    }
}
