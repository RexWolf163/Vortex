using System;
using UnityEngine;

namespace Vortex.Unity.AssetSwapSystem
{
    /// <summary>
    /// POCO группы swap'а. Имя группы вычисляется из <see cref="index"/> как
    /// <c>$"AssetGroup{index}"</c> — не хранится строкой, чтобы избежать drift'а
    /// между сохранённым текстом и логикой (см. инвариант I3).
    /// </summary>
    [Serializable]
    internal class AssetSwapGroup
    {
        [SerializeField] public int index;
        [SerializeField] public int variantsCount;
        [SerializeField, TextArea] public string comment;
    }
}
