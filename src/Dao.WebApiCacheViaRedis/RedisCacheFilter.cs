using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Dao.WebApiCacheViaRedis;

internal class RedisCacheFilter(IRedisLogger logger) : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        if (context.ActionDescriptor is ControllerActionDescriptor descriptor)
        {
            var returnType = descriptor.MethodInfo.ReturnType;
            if (!returnType.IsVoidOrTask() && !returnType.IsClassOf(typeof(Stream)))
            {
                var request = context.HttpContext.Request;
                var config = new RedisCacheAttribute(false);
                var attr = descriptor.GetAttributes<RedisCacheAttribute>().FirstOrDefault();
                if (attr is { Enabled: true } || (attr == null && GlobalVars.RedisCacheSettings.ApplyToAllGetRequests && request.Method.Equals("GET", StringComparison.OrdinalIgnoreCase)))
                {
                    InitializeConfig(config, attr);

                    var key = new RedisCacheKey(GlobalVars.ServiceName, request.Method, descriptor.AttributeRouteInfo?.Template, context.ActionArguments);
                    if (config.RedisConfiguration.UseExtraQueryStringAsRedisKey)
                        key.Query = request.Query;

                    var getSetResult = GetSetResult(returnType);

                    try
                    {
                        var manager = RedisManager.GetOrCreate(GlobalVars.RedisConnectionString, logger);

                        var sw = new Stopwatch();
                        sw.Start();

                        var value = await manager.GetOrAddAsync(key, config.RedisCacheResetBy, async () =>
                        {
                            var resultContext = await next().ConfigureAwait(false);
                            return getSetResult.Get(resultContext.Result);
                        }, config.RedisConfiguration).ConfigureAwait(false);

                        sw.Stop();
                        logger?.Debug(string.Join(Environment.NewLine,
                            $"Get cache ({key}) from Redis. ({value?.Length ?? 0})",
                            $"Elapsed: {(sw.ElapsedMilliseconds > 1000 ? "[ATTENTION] " : "")}Cost {sw.ElapsedMilliseconds} ms"));

                        context.Result = getSetResult.Set(value);
                        return;
                    }
                    catch (Exception ex)
                    {
                        logger?.Error($"[{nameof(RedisCacheFilter)}, {request.Method}: {request.FullPath()}]", ex);
                    }
                }
            }
        }

        await next().ConfigureAwait(false);
    }

    static void InitializeConfig(RedisCacheAttribute config, RedisCacheAttribute attr)
    {
        config.Enabled = true;
        config.RedisConfiguration = attr?.RedisConfiguration ?? GlobalVars.RedisCacheSettings.RedisConfiguration;
        config.RedisCacheResetBy.EntityTypes = attr?.RedisCacheResetBy.EntityTypes;
        config.RedisCacheResetBy.MicroServiceRoutes = attr?.RedisCacheResetBy.MicroServiceRoutes;
    }

    #region GetSet

    static readonly Func<IActionResult, string> getResultValue = ar => ar switch
    {
        ObjectResult or => or.Value.ToJson(),
        ContentResult cr => cr.Content,
        _ => null
    };

    static readonly Dictionary<ActionResultType, GetSet<Func<IActionResult, string>, Func<string, IActionResult>>> getSetResults = new()
    {
        {
            ActionResultType.ContentResult, new GetSet<Func<IActionResult, string>, Func<string, IActionResult>>
            {
                Get = getResultValue,
                Set = v => new ContentResult
                {
                    Content = v,
                    ContentType = "text/plain; charset=utf-8"
                }
            }
        },
        {
            ActionResultType.ObjectResult, new GetSet<Func<IActionResult, string>, Func<string, IActionResult>>
            {
                Get = getResultValue,
                Set = v => new ContentResult
                {
                    Content = v,
                    ContentType = "application/json; charset=utf-8"
                }
            }
        }
    };

    static GetSet<Func<IActionResult, string>, Func<string, IActionResult>> GetSetResult(Type returnType)
    {
        var actionResultType = returnType.GetTypeOrTaskGenericType() == typeof(string)
            ? ActionResultType.ContentResult
            : ActionResultType.ObjectResult;
        getSetResults.TryGetValue(actionResultType, out var getSet);
        return getSet;
    }

    #endregion
}