using System;

namespace Dao.WebApiCacheViaRedis;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public class RedisCacheAttribute : Attribute
{
    public RedisCacheAttribute()
    {
    }

    public RedisCacheAttribute(bool enabled) => Enabled = enabled;

    public bool Enabled { get; set; } = true;
    public RedisConfiguration RedisConfiguration { get; set; }

    RedisCacheResetBy redisCacheResetBy;
    public RedisCacheResetBy RedisCacheResetBy
    {
        get => this.redisCacheResetBy ?? new RedisCacheResetBy();
        set => this.redisCacheResetBy = value;
    }
}