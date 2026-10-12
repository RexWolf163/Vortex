using UnityEngine;
using Vortex.Core.ExtensibleEnumSystem.Abstractions;

namespace Vortex.Unity.ExtensibleEnumSystem.Bootstrap
{
    /// <summary>
    /// Unity-запуск eager-инициализации реестра <see cref="ExtensibleEnum"/> (Layer 1 платформенно-независим и сам её
    /// не вызывает):
    /// <list type="bullet">
    /// <item>в рантайме — до загрузки первой сцены (<see cref="RuntimeInitializeLoadType.BeforeSceneLoad"/>): к
    /// <c>Awake</c> объектов сцены реестр и десериализация уже готовы;</item>
    /// <item>в редакторе — при загрузке домена: Inspector-дропдауны (<c>[ExtEnumKey]</c>) видят все значения.</item>
    /// </list>
    /// <see cref="ExtensibleEnum.Initialize"/> идемпотентна — оба вызова в редакторе безопасны.
    /// </summary>
    public static class ExtensibleEnumBootstrap
    {
        /// <summary>Инициализация реестра в рантайме, до загрузки первой сцены.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void InitializeOnRuntime() => ExtensibleEnum.Initialize();

#if UNITY_EDITOR
        /// <summary>Инициализация реестра при загрузке Editor-домена.</summary>
        [UnityEditor.InitializeOnLoadMethod]
        private static void InitializeOnEditor() => ExtensibleEnum.Initialize();
#endif
    }
}
