namespace Dao.WebApiCacheViaRedis;

internal static class GlobalVars
{
    public static string ServiceName { get; set; }
    public static string RedisConnectionString { get; set; }

    static RedisCacheSettings redisCacheSettings;
    public static RedisCacheSettings RedisCacheSettings
    {
        get => redisCacheSettings ?? new RedisCacheSettings();
        set => redisCacheSettings = value;
    }
}