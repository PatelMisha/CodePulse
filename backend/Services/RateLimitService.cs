using StackExchange.Redis;

namespace backend.Services;

// Prevents abuse — max 10 code runs per IP per minute.
// Redis key expires automatically after 60 seconds.
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

    public async Task<int> GetRemainingRunsAsync(string clientIp)
    {
        var key = $"ratelimit:{clientIp}:{DateTime.UtcNow:yyyyMMddHHmm}";
        var count = (int?)await _redis.StringGetAsync(key) ?? 0;
        return Math.Max(0, MaxRunsPerMinute - count);
    }
}
