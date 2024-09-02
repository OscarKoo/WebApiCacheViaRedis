using System;
using System.Collections.Generic;
using System.Net.Http;

namespace Dao.WebApiCacheViaRedis;

public class RedisCacheResetBy
{
    public ICollection<Type> EntityTypes { get; set; }
    public ICollection<MicroServiceRoute> MicroServiceRoutes { get; set; }
}

public class MicroServiceRoute(string serviceName, HttpMethod httpMethod, string route)
{
    public string ServiceName { get; } = serviceName;
    public HttpMethod HttpMethod { get; } = httpMethod;
    public string Route { get; } = route;

    public override string ToString() => RedisCacheKey.CreateRouteKey(ServiceName, HttpMethod.ToString(), Route);
}