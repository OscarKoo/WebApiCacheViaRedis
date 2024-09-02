using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Controllers;
using Newtonsoft.Json;
using StackExchange.Redis;

namespace Dao.WebApiCacheViaRedis;

internal static class Extensions
{
    #region GetAttributes

    static readonly ConcurrentDictionary<Tuple<ControllerActionType, Type>, Lazy<object[]>> actionControllerAttributes = new();

    public static IEnumerable<T> GetAttributes<T>(this ControllerActionDescriptor descriptor, Func<KeyValuePair<Type, T[]>, bool> attributeFilter = null)
    {
        if (descriptor == null)
            return Array.Empty<T>();

        var key = new Tuple<ControllerActionType, Type>(new ControllerActionType(descriptor.ControllerTypeInfo, descriptor.MethodInfo), typeof(T));
        return actionControllerAttributes.GetOrAdd(key, k => new Lazy<object[]>(() =>
        {
            var actionAttributes = GetAttributes<T>(k.Item1.ActionMethod);
            var controllerAttributes = GetAttributes<T>(k.Item1.ControllerType);

            foreach (var kv in controllerAttributes.Where(kv => !actionAttributes.ContainsKey(kv.Key)))
            {
                actionAttributes.Add(kv.Key, kv.Value);
            }

            return actionAttributes.Where(w => attributeFilter == null || attributeFilter(w)).SelectMany(sm => sm.Value.Select(s => (object)s)).ToArray();
        })).Value.Cast<T>();
    }

    static Dictionary<Type, T[]> GetAttributes<T>(MemberInfo memberInfo) => memberInfo.GetCustomAttributes(true).OfType<T>().Select(s => new
    {
        Type = s.GetType(),
        Filter = s
    }).GroupBy(g => g.Type).ToDictionary(k => k.Key, v => v.Select(s => s.Filter).ToArray());

    #endregion

    #region json

    static readonly JsonSerializerSettings jsonSerializerSettings = new()
    {
        NullValueHandling = NullValueHandling.Ignore,
        ReferenceLoopHandling = ReferenceLoopHandling.Ignore
    };

    public static string ToJson(this object source) => source == null ? null : JsonConvert.SerializeObject(source, jsonSerializerSettings);

    #endregion

    #region Task

    public static bool IsVoidOrTask(this Type type) => type.In(typeof(void), typeof(Task), typeof(ValueTask));

    public static Type GetTypeOrTaskGenericType(this Type type) =>
        type.IsGenericType && type.GetGenericTypeDefinition().In(typeof(Task<>), typeof(ValueTask<>)) ? type.GetGenericArguments()[0] : type;

    public static bool IsClassOf(this Type type, Type targetType) => type == targetType || type.IsSubclassOf(targetType);

    #endregion

    public static string FullPath(this HttpRequest request) =>
        request == null ? null : $"{request.Scheme}://{request.Host}{request.Path}{request.QueryString.Value}";

    public static bool IsNullOrEmpty<T>(this IEnumerable<T> source) => source == null || !source.Any();

    public static bool In<T>(this T source, IEqualityComparer<T> comparer, params T[] args) => !args.IsNullOrEmpty() && args.Contains(source, comparer);

    public static bool In<T>(this T source, params T[] args) => source.In(null, args);

    #region RedisValue

    const string RedisValueStringNull = nameof(RedisValueStringNull);
    const string RedisValueStringEmpty = nameof(RedisValueStringEmpty);

    public static string StringValue(this RedisValue source)
    {
        if (!source.HasValue)
            return null;

        var value = source.ToString();
        return value.Equals(RedisValueStringNull, StringComparison.OrdinalIgnoreCase)
            ? null
            : value.Equals(RedisValueStringEmpty, StringComparison.OrdinalIgnoreCase)
                ? string.Empty
                : value;
    }

    public static RedisValue ToRedisValue(this string source) =>
        source == null
            ? new RedisValue(RedisValueStringNull)
            : string.IsNullOrWhiteSpace(source)
                ? new RedisValue(RedisValueStringEmpty)
                : new RedisValue(source);

    #endregion
}