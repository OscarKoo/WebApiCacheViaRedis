using System;

namespace Dao.WebApiCacheViaRedis;

public interface IRedisLogger
{
    void Debug(string message, Exception exception = null);
    void Info(string message, Exception exception = null);
    void Warn(string message, Exception exception = null);
    void Error(string message, Exception exception = null);
}