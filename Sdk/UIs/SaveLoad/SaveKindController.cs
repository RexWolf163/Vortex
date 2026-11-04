using Cysharp.Threading.Tasks;
using UnityEngine;
using Vortex.Core.SaveSystem.Abstraction;
using Vortex.Core.SaveSystem.Bus;

namespace Vortex.Sdk.UIs.SaveLoad
{
    /// <summary>
    /// Тип сейва (<see cref="SavingSystemConstants.AutoName"/> / <see cref="SavingSystemConstants.ManualName"/>) —
    /// метаблок <see cref="MetaKey"/> сводки.
    ///
    /// Тип знает только точка сохранения, поэтому она сохраняет через <see cref="Save"/>: тип держится на время
    /// цикла и пишется в метаданные на начале сохранения. Сейв в обход (<c>SaveController.Save</c>) пишется без
    /// типа.
    ///
    /// Для сейвов, записанных до перехода на метаданные, тип читается из префикса имени (<c>{тип}_{индекс}</c>).
    /// </summary>
    public static class SaveKindController
    {
        /// <summary>
        /// Ключ метаблока типа сейва
        /// </summary>
        public const string MetaKey = "kind";

        /// <summary>
        /// Тип сейва текущего цикла; null — сохранение идёт без типа
        /// </summary>
        private static string _kind;

        [RuntimeInitializeOnLoadMethod]
        private static void Init()
        {
            SaveController.OnSaveStart -= AddKind;
            SaveController.OnSaveStart += AddKind;
        }

        /// <summary>
        /// Сохранение с типом. Возвращает guid сейва; null — сохранение отклонено (идёт другое) или не удалось.
        /// </summary>
        /// <param name="kind">Тип сейва</param>
        /// <param name="name">Название для сейва</param>
        /// <param name="guid">guid сейва; null — новый</param>
        public static async UniTask<string> Save(string kind, string name, string guid = null)
        {
            _kind = kind;
            try
            {
                return await SaveController.Save(name, guid);
            }
            finally
            {
                // Тип живёт только в своём цикле: отклонённое замком сохранение не оставит его следующему
                _kind = null;
            }
        }

        /// <summary>
        /// Тип сейва: из метаданных, для старых сейвов — из префикса имени; null — тип не определён.
        /// </summary>
        public static string GetKind(SaveSummary summary)
        {
            var kind = summary.GetMeta(MetaKey);
            if (kind != null)
                return kind;

            if (string.IsNullOrEmpty(summary.Name))
                return null;

            // Equals у string — ordinal; StartsWith не годится: засчитал бы «automatic_1» за автосейв
            var prefix = summary.GetSavePureName().Split('_')[0];
            return SavingSystemConstants.AutoName.Equals(prefix) || SavingSystemConstants.ManualName.Equals(prefix)
                ? prefix
                : null;
        }

        private static void AddKind()
        {
            if (_kind != null)
                SaveController.AddMeta(MetaKey, _kind);
        }
    }
}
