using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Http;

namespace Dao.WebApiCacheViaRedis;

public class RedisCacheKey
{
    public RedisCacheKey(string serviceName, string httpMethod, string route, IDictionary<string, object> args, IQueryCollection query = null)
    {
        ServiceName = serviceName;
        HttpMethod = httpMethod;
        Route = route;
        Arguments = args;
        Query = query;
    }

    public string ServiceName { get; }
    public string HttpMethod { get; }

    public string Route { get; }
    public string RouteKey => string.IsNullOrWhiteSpace(Route) ? Route : CreateRouteKey(ServiceName, HttpMethod, Route);
    internal static string CreateRouteKey(string serviceName, string httpMethod, string route) => $"{serviceName}${httpMethod}${route}".TrimEnd('/').ToLowerInvariant();

    public IDictionary<string, object> Arguments { get; }

    public IQueryCollection Query { get; set; }
    string ParseQuery => Query.IsNullOrEmpty() ? null : "$" + Query.OrderBy(o => o.Key).ToDictionary(k => k.Key, v => v.Value.OrderBy(o => o).ToArray()).ToJson();

    public override string ToString() => $"{CreateRouteKey(ServiceName, HttpMethod, Route)}${Arguments?.ToJson()}{ParseQuery}".ToLowerInvariant();
}