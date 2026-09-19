using System;
using System.Collections.Generic;
using UnityEngine;

namespace Vortex.Unity.AssetSwapSystem
{
    /// <summary>
    /// POCO группы swap'а. Имя группы вычисляется из <see cref="index"/> как
    /// <c>$"AssetGroup{index}"</c> — не хранится строкой, чтобы избежать drift'а
    /// между сохранённым текстом и логикой (см. инвариант I3).
    ///
    /// <see cref="variantComments"/> — параллельный <see cref="variantsCount"/> список
    /// поясняющих комментариев к вариантам (индекс K-1 → комментарий Variant K).
    /// Длина выравнивается в <c>AssetSwapSettings.Sanitize</c>: если короче — добивается
    /// пустыми строками; если длиннее (variantsCount уменьшили) — обрезается. Хранить
    /// как <c>List&lt;AssetSwapVariant&gt;</c> избыточно на текущем наборе полей
    /// (единственный опциональный текст); при расширении атрибутов варианта — переехать.
    /// </summary>
    [Serializable]
    internal class AssetSwapGroup
    {
        [SerializeField] public int index;
        [SerializeField] public int variantsCount;
        [SerializeField, TextArea] public string comment;
        [SerializeField] public List<string> variantComments = new();
    }
}
