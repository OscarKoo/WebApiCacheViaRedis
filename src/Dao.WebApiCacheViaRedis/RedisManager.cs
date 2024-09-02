using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dao.IndividualLock;
using StackExchange.Redis;

namespace Dao.WebApiCacheViaRedis;

public class RedisManager
{
    const string Prefix = $"{nameof(Dao)}.{nameof(WebApiCacheViaRedis)}.{nameof(RedisManager)}";
    const string ChannelName = $"${Prefix}_reset_route";

    static RedisManager instance;
    static readonly ConcurrentDictionary<MutableItem<string>, RedisKeyRelationships> requests = new(new MutableItemComparer<string>(StringComparer.Ordinal));
    static readonly IndividualLocks<string> cacheLock = new(StringComparer.Ordinal);

    #region Redis

    IRedisLogger logger;
    readonly ConnectionMultiplexer redis;
    readonly ISubscriber subscriber;
    readonly Timer timer;

    RedisManager(string redisConnectionString, IRedisLogger logger)
    {
        this.logger = logger;

        this.redis = ConnectionMultiplexer.Connect(redisConnectionString);
        this.subscriber = this.redis.GetSubscriber();
        this.subscriber.Subscribe(ChannelName, OnSubscribed);

        RedisKeyRelationships.OnPubRoute = OnPubRoute;
        RedisKeyRelationships.OnResetRequest = OnResetRequest;

        if (GlobalVars.RedisCacheSettings.AutoCleanupInterval != null)
        {
            var interval = GlobalVars.RedisCacheSettings.AutoCleanupInterval.Value;
            this.timer = new Timer(TimerCallback, null, interval, interval);
        }
    }

    static volatile bool isCleaning;

    static void TimerCallback(object state)
    {
        if (isCleaning)
            return;
        isCleaning = true;

        var now = DateTime.UtcNow;
        var keys = requests.Where(w => w.Key.UpdateTime < now).Select(s => s.Key).ToList();

        foreach (var key in keys)
        {
            OnResetRequest(key);
        }

        isCleaning = false;
    }

    static void OnSubscribed(RedisChannel channel, RedisValue message)
    {
        var route = message.ToString();
        if (RedisKeyRelationships.ResetSubRoute(route))
            instance.logger?.Info($"[RedisManager.OnSubscribed] Reset by route: {route}");
    }

    static void OnPubRoute(string route)
    {
        try
        {
            instance.subscriber.Publish(ChannelName, route);
            instance.logger?.Info($"[RedisManager.OnPubRoute] Pub route: {route}");
        }
        catch (Exception ex)
        {
            Console.WriteLine(string.Join(Environment.NewLine, $"[RedisManager.OnPubRoute, {route}]", ex));
        }
    }

    static void OnResetRequest(string request)
    {
        try
        {
            instance.GetDatabase().KeyDelete(request);
            Remove(request);

            instance.logger?.Info($"[RedisManager.OnResetRequest] Reset request: {request}");
        }
        catch (Exception ex)
        {
            Console.WriteLine(string.Join(Environment.NewLine, $"[RedisManager.OnResetRequest, {request}]", ex));
        }
    }

    static void Remove(string request)
    {
        if (requests.TryRemove(request, out var value))
            value.Remove(request);
    }

    public static RedisManager GetOrCreate(string redisConnectionString, IRedisLogger logger = null)
    {
        if (instance != null)
        {
            instance.logger ??= logger;
            return instance;
        }

        lock (cacheLock)
        {
            if (instance != null)
            {
                instance.logger ??= logger;
                return instance;
            }

            instance = new RedisManager(redisConnectionString, logger);
        }

        return instance;
    }

    public IDatabase GetDatabase() => this.redis.GetDatabase();

    static async Task<bool> TryGetCache(IDatabase db, string key, RefOut<RedisValue> value)
    {
        value.Value = await db.StringGetAsync(key).ConfigureAwait(false);
        return value.Value.HasValue;
    }

    public async Task<string> GetOrAddAsync(RedisCacheKey redisKey, RedisCacheResetBy resetBy, Func<Task<string>> valueFactory, RedisConfiguration config = null)
    {
        var key = redisKey.ToString();
        var routeKey = redisKey.RouteKey;
        return await GetOrAddAsync(key, routeKey, resetBy, valueFactory, config).ConfigureAwait(false);
    }

    public async Task<string> GetOrAddAsync(string key, string routeKey, RedisCacheResetBy resetBy, Func<Task<string>> valueFactory, RedisConfiguration config = null)
    {
        var db = GetDatabase();

        var requestKey = new MutableItem<string>(key);
        requestKey.UpdateTime = config?.Expiry != null
            ? requestKey.CreateTime.Add(config.Expiry.Value)
            : DateTime.MaxValue;

        var relationship = new RedisKeyRelationships(key, routeKey, resetBy?.EntityTypes, resetBy?.MicroServiceRoutes?.Select(s => s.ToString()).ToList());

        if (GlobalVars.RedisCacheSettings.AutoCleanupInterval != null)
            requests.TryAdd(requestKey, relationship);

        var outValue = RefOut<RedisValue>.Create();
        if (await TryGetCache(db, key, outValue).ConfigureAwait(false))
            return outValue.Value;

        Remove(key);

        using (await cacheLock.LockAsync(key).ConfigureAwait(false))
        {
            if (await TryGetCache(db, key, outValue).ConfigureAwait(false))
                return outValue.Value;

            var sw = new Stopwatch();
            sw.Start();

            var value = (await valueFactory().ConfigureAwait(false)).ToRedisValue();
            await db.StringSetAsync(key, value, config?.Expiry, When.Always, config?.CommandFlags ?? CommandFlags.None).ConfigureAwait(false);

            requests.TryAdd(requestKey, relationship);

            sw.Stop();
            this.logger?.Info(string.Join(Environment.NewLine,
                $"Cache ({key}) dose not exist in the Redis, Get data from database.",
                $"Elapsed: {(sw.ElapsedMilliseconds > 1000 ? "[ATTENTION] " : "")}Cost {sw.ElapsedMilliseconds} ms"));

            return value.StringValue();
        }
    }

    #endregion

    #region SaveChanges

    public static void AfterSaveChanges(IEnumerable<Type> entityTypes)
    {
        if (instance == null)
            return;

        RedisKeyRelationships.ResetEntityTypes(entityTypes);
    }

    #endregion
}