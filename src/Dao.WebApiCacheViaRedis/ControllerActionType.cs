using System;
using System.Reflection;

namespace Dao.WebApiCacheViaRedis;

internal class ControllerActionType(TypeInfo controllerType, MethodInfo actionMethod)
{
    readonly Tuple<TypeInfo, MethodInfo> instance = new(controllerType, actionMethod);

    public TypeInfo ControllerType { get; } = controllerType;
    public MethodInfo ActionMethod { get; } = actionMethod;

    public override int GetHashCode() => this.instance.GetHashCode();

    public override bool Equals(object obj) => obj is ControllerActionType type && this.instance.Equals(type.instance);
}