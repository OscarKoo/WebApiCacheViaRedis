using System;
using Microsoft.Extensions.DependencyInjection;

namespace Dao.WebApiCacheViaRedis;

public static class ServicesExtensions
{
    public static IServiceCollection AddRedisLogger<T>(this IServiceCollection services)
        where T : class, IRedisLogger
    {
        services.AddSingleton<IRedisLogger, T>();
        return services;
    }

    public static IMvcBuilder AddWebApiCacheViaRedis(this IServiceCollection services, string serviceName, string redisConnectionString = null, RedisCacheSettings settings = null)
    {
        if (string.IsNullOrWhiteSpace(serviceName))
            throw new ArgumentNullException(nameof(serviceName));

        if (string.IsNullOrWhiteSpace(redisConnectionString))
            redisConnectionString = "127.0.0.1:6379";

        GlobalVars.ServiceName = serviceName;
        GlobalVars.RedisConnectionString = redisConnectionString;
        GlobalVars.RedisCacheSettings = settings;

        return services.AddControllers(opt => { opt.Filters.Add<RedisCacheFilter>(); });
    }
}