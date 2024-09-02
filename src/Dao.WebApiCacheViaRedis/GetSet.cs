namespace Dao.WebApiCacheViaRedis;

public class GetSet<TGet, TSet>
{
    public TGet Get { get; set; }
    public TSet Set { get; set; }
}