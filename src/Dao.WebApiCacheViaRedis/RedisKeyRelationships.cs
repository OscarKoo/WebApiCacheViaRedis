using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace Dao.WebApiCacheViaRedis;

internal class RedisKeyRelationships
{
    static readonly ConcurrentDictionary<Type, HashSet<string>> entityRequests = new();
    static readonly ConcurrentDictionary<string, HashSet<string>> subRouteRequests = new(StringComparer.Ordinal);

    public static Action<string> OnPubRoute { get; set; }
    public static Action<string> OnResetRequest { get; set; }

    public RedisKeyRelationships(string request, string pubRoute, IEnumerable<Type> entityTypes, IEnumerable<string> subRoutes)
    {
        PubRoute = pubRoute;

        if (entityTypes == null)
        {
            EntityTypes = new List<Type>();
        }
        else
        {
            EntityTypes = new List<Type>(entityTypes);
            foreach (var entityType in EntityTypes)
            {
                Add(entityRequests, entityType, request);
            }
        }

        if (subRoutes == null)
        {
            SubRoutes = new List<string>();
        }
        else
        {
            SubRoutes = new List<string>(subRoutes);
            foreach (var subRoute in SubRoutes)
            {
                Add(subRouteRequests, subRoute, request);
            }
        }
    }

    public string PubRoute { get; }
    public ICollection<Type> EntityTypes { get; }
    public ICollection<string> SubRoutes { get; }

    public void Remove(string request)
    {
        foreach (var entityType in EntityTypes)
        {
            Remove(entityRequests, entityType, request);
        }

        foreach (var subRoute in SubRoutes)
        {
            Remove(subRouteRequests, subRoute, request);
        }

        if (!string.IsNullOrWhiteSpace(PubRoute))
        {
            var onPubRoute = OnPubRoute;
            onPubRoute?.Invoke(PubRoute);
        }
    }

    static HashSet<string> GetOrAdd<TKey>(ConcurrentDictionary<TKey, HashSet<string>> source, TKey key) =>
        source.GetOrAdd(key, _ => new HashSet<string>(StringComparer.Ordinal));

    static bool Add<TKey>(ConcurrentDictionary<TKey, HashSet<string>> source, TKey key, string value)
    {
        var items = GetOrAdd(source, key);
        lock (items)
        {
            return items.Add(value);
        }
    }

    static bool Remove<TKey>(ConcurrentDictionary<TKey, HashSet<string>> source, TKey key, string value)
    {
        var items = GetOrAdd(source, key);
        lock (items)
        {
            return items.Remove(value);
        }
    }

    public static void ResetEntityTypes(IEnumerable<Type> entityTypes)
    {
        if (entityTypes == null)
            return;

        var onResetRequest = OnResetRequest;
        var removed = new HashSet<string>(StringComparer.Ordinal);

        foreach (var entityType in entityTypes)
        {
            var requests = GetOrAdd(entityRequests, entityType);
            lock (requests)
            {
                foreach (var request in requests.Where(removed.Add))
                {
                    onResetRequest?.Invoke(request);
                }

                requests.Clear();
            }
        }
    }

    public static void ResetSubRoute(string subRoute)
    {
        if (string.IsNullOrWhiteSpace(subRoute))
            return;

        var onResetRequest = OnResetRequest;
        var requests = GetOrAdd(subRouteRequests, subRoute);
        lock (requests)
        {
            foreach (var request in requests)
            {
                onResetRequest?.Invoke(request);
            }

            requests.Clear();
        }
    }
}