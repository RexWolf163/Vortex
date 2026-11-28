using System.Collections.Generic;

namespace Vortex.Unity.AssetSwapSystem
{
    /// <summary>
    /// Описание группы для внешних инструментов (выбор группы и варианта в настройках сборки): номер, комментарий
    /// группы и комментарии вариантов (индекс K-1 → Variant K). Снимок на момент вызова
    /// <see cref="AssetSwapController.GetGroups"/>; настройки им не меняются
    /// </summary>
    public readonly struct AssetSwapGroupInfo
    {
        /// <summary>N из <c>AssetGroup{N}</c></summary>
        public readonly int Index;

        /// <summary>Comment группы; пусто — не задан</summary>
        public readonly string Comment;

        /// <summary>Comment вариантов: индекс K-1 → Variant K; пусто — не задан</summary>
        public readonly IReadOnlyList<string> VariantComments;

        public AssetSwapGroupInfo(int index, string comment, IReadOnlyList<string> variantComments)
        {
            Index = index;
            Comment = comment;
            VariantComments = variantComments;
        }
    }
}
