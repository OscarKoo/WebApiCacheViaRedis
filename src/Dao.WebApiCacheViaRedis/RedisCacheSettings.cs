using System;
using StackExchange.Redis;

namespace Dao.WebApiCacheViaRedis;

public class RedisCacheSettings
{
    public bool ApplyToAllGetRequests { get; set; }
    public TimeSpan? AutoCleanupInterval { get; set; }

    RedisConfiguration redisConfiguration;
    public RedisConfiguration RedisConfiguration
    {
        get => this.redisConfiguration ?? new RedisConfiguration();
        set => this.redisConfiguration = value;
    }
}

public class RedisConfiguration
{
    public bool UseExtraQueryStringAsRedisKey { get; set; }
    public TimeSpan? Expiry { get; set; }
    public CommandFlags? CommandFlags { get; set; }
}