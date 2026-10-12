using UnityEngine;

namespace Vortex.Core.Extensions.LogicExtensions
{
    public static partial class CopyExclusionMarker
    {
        /// <summary>
        /// Свойства <see cref="ScriptableObject"/> и его предков (<c>UnityEngine.Object</c>: <c>name</c>, <c>hideFlags</c>)
        /// не копируются в <c>CopyFrom</c>: пресет-SO отдаёт только доменные данные. Без исключения <c>name</c>
        /// (имя файла ассета) совпал бы без учёта регистра с <c>Record.Name</c> и затёр бы его.
        /// </summary>
        public static ScriptableObject so;
    }
}
