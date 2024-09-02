using System;
using System.Collections.Generic;

namespace Dao.WebApiCacheViaRedis;

public class MutableItem<T>
{
    public MutableItem(T value, DateTime? now = null)
    {
        Value = value;
        CreateTime = now ?? DateTime.UtcNow;
        UpdateTime = CreateTime;
    }

    public T Value { get; }

    public DateTime CreateTime { get; }
    public DateTime UpdateTime { get; set; }


    public override bool Equals(object obj) => obj is MutableItem<T> key && Value.Equals(key.Value);
    public override int GetHashCode() => Value.GetHashCode();

    public static implicit operator T(MutableItem<T> item) => item.Value;
    public static implicit operator MutableItem<T>(T item) => new(item);
}

public class MutableItemComparer<T>(IEqualityComparer<T> compare = null) : IEqualityComparer<MutableItem<T>>
{
    readonly IEqualityComparer<T> compare = compare ?? EqualityComparer<T>.Default;

    public bool Equals(MutableItem<T> x, MutableItem<T> y) => x != null && y != null && this.compare.Equals(x.Value, y.Value);

    public int GetHashCode(MutableItem<T> obj) => this.compare.GetHashCode(obj.Value);
}