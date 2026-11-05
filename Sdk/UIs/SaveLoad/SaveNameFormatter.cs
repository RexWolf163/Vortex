using Vortex.Core.LocalizationSystem;
using Vortex.Core.SaveSystem.Abstraction;

namespace Vortex.Sdk.UIs.SaveLoad
{
    /// <summary>
    /// Отображаемое имя сейва по шаблону <c>{тип}_{индекс}[_{хвост}]</c>, где тип —
    /// <see cref="SavingSystemConstants.AutoName"/> / <see cref="SavingSystemConstants.ManualName"/>.
    /// </summary>
    public static class SaveNameFormatter
    {
        /// <summary>
        /// Имя для показа: тип распознан — локализованный шаблон с индексом (и хвост через «: »), иначе чистое имя
        /// как есть. Тип сравнивается целиком: «autosave_3» — не автосейв.
        /// </summary>
        /// <param name="summary">Сводка сейва</param>
        /// <param name="autoPatternKey">Ключ локализации шаблона автосейва, {0} — индекс</param>
        /// <param name="manualPatternKey">Ключ локализации шаблона ручного сейва, {0} — индекс</param>
        public static string GetDisplayName(this SaveSummary summary, string autoPatternKey, string manualPatternKey)
        {
            var name = summary.GetSavePureName();
            var parts = name.Split('_');
            if (parts.Length < 2)
                return name;

            string patternKey;
            if (SavingSystemConstants.AutoName.Equals(parts[0]))
                patternKey = autoPatternKey;
            else if (SavingSystemConstants.ManualName.Equals(parts[0]))
                patternKey = manualPatternKey;
            else
                return name;

            var label = string.Format(patternKey.Translate(), parts[1]);
            return parts.Length > 2 ? $"{label}: {string.Join('_', parts[2..])}" : label;
        }
    }
}
