using System;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using UnityEngine;
using Vortex.Core.StateAxisSystem.Abstractions;

namespace Vortex.Unity.StateAxisSystem.Initialization
{
    /// <summary>
    /// Принудительное выполнение static-инициализаторов всех не-абстрактных наследников
    /// <see cref="StateAxis"/>. Без этого <see cref="StateAxis.GetAll{T}"/> возвращает пустой
    /// массив, пока в коде нет явной ссылки на конкретное значение оси.
    ///
    /// Вызывается:
    ///  • в рантайме — <c>[RuntimeInitializeOnLoadMethod(BeforeSceneLoad)]</c>;
    ///  • в редакторе — <c>[InitializeOnLoadMethod]</c> для работы Inspector-дропдаунов и валидатора
    ///    в редакторском режиме (вне Play).
    /// </summary>
    public static class StateAxisInitializer
    {
        private static bool _initialized;

        [RuntimeInitializeOnLoadMethod]
        private static void InitializeRuntime() => Initialize();

#if UNITY_EDITOR
        [UnityEditor.InitializeOnLoadMethod]
        private static void InitializeEditor() => Initialize();
#endif

        /// <summary>
        /// Идемпотентная инициализация. Сканирует AppDomain и принудительно выполняет
        /// type initializer'ы всех не-абстрактных <see cref="StateAxis"/>-наследников.
        /// </summary>
        public static void Initialize()
        {
            if (_initialized) return;
            _initialized = true;

            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type[] types;
                try
                {
                    types = asm.GetTypes();
                }
                catch (ReflectionTypeLoadException ex)
                {
                    types = ex.Types.Where(t => t != null).ToArray();
                }

                foreach (var type in types)
                {
                    if (type == null || type.IsAbstract) continue;
                    if (!typeof(StateAxis).IsAssignableFrom(type)) continue;

                    try
                    {
                        RuntimeHelpers.RunClassConstructor(type.TypeHandle);
                    }
                    catch (Exception e)
                    {
                        Debug.LogError($"[StateAxis] Failed to initialize {type.FullName}: {e.Message}");
                    }
                }
            }
        }
    }
}