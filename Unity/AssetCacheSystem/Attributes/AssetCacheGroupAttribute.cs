using System;
using UnityEngine;

namespace Vortex.Unity.AssetCacheSystem.Attributes
{
    /// <summary>
    /// Помечает string-поле как ключ группы кэша. В инспекторе поле заменяется выпадашкой
    /// с группами из <c>AssetCacheSettings</c>. Пустое значение — дефолтная группа.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public class AssetCacheGroupAttribute : PropertyAttribute
    {
    }
}
