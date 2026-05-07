using UnityEngine;

namespace Vortex.Sdk.EffectSpawnSystem.Components
{
    /// <summary>
    /// Маркер «парковать эффекты сюда».
    /// Размещается на любом GameObject в сцене; <see cref="EffectSpawn"/> ищет ближайший
    /// <see cref="EffectsLayer"/> вверх по родительской цепочке от target-параметра спауна.
    ///
    /// Если поле <see cref="Target"/> задано — эффекты паркуются туда; если null —
    /// в transform этого компонента.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EffectsLayer : MonoBehaviour
    {
        [SerializeField, Tooltip("Куда фактически паркуются эффекты. Если null — в transform этого компонента.")]
        private Transform target;

        /// <summary>
        /// Точка парковки эффектов. Может быть null — тогда используется <see cref="Component.transform"/>.
        /// </summary>
        public Transform Target => target;
    }
}
