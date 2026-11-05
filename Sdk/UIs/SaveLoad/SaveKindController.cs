using Cysharp.Threading.Tasks;
using UnityEngine;
using Vortex.Core.SaveSystem.Abstraction;
using Vortex.Core.SaveSystem.Bus;
using Vortex.Unity.SaveSystem.Presets;

namespace Vortex.Sdk.UIs.SaveLoad
{
    /// <summary>
    /// Тип сейва (<see cref="SavingSystemConstants.AutoName"/> / <see cref="SavingSystemConstants.ManualName"/>) —
    /// метаблок <see cref="MetaKey"/> сводки.
    ///
    /// Тип знает только точка сохранения, поэтому она сохраняет через <see cref="Save"/>: тип держится на время
    /// синхронной постановки запроса и пишется в метаданные запроса (<c>SaveController.OnSaveRequest</c>) — едет
    /// вместе с ним через очередь. Сейв в обход (<c>SaveController.Save</c>) пишется без типа.
    ///
    /// Для сейвов, записанных до перехода на метаданные, тип читается из префикса имени (<c>{тип}_{индекс}</c>).
    /// </summary>
    public static class SaveKindController
    {
        /// <summary>
        /// Ключ метаблока типа сейва
        /// </summary>
        public const string MetaKey = SaveSettings.KindMetaKey;

        /// <summary>
        /// Тип ставящегося запроса; null — вне <see cref="Save"/>, запрос без типа
        /// </summary>
        private static string _kind;

        [RuntimeInitializeOnLoadMethod]
        private static void Init()
        {
            SaveController.OnSaveRequest -= AddKind;
            SaveController.OnSaveRequest += AddKind;
        }

        /// <summary>
        /// Сохранение с типом. Возвращает guid сейва; null — запрос отклонён, отменён удалением сейва или не удался.
        /// </summary>
        /// <param name="kind">Тип сейва</param>
        /// <param name="name">Название для сейва</param>
        /// <param name="guid">guid сейва; null — новый</param>
        public static UniTask<string> Save(string kind, string name, string guid = null)
        {
            _kind = kind;
            try
            {
                // OnSaveRequest поднимается синхронно внутри Save — тип уходит в метаданные запроса до возврата
                return SaveController.Save(name, guid);
            }
            finally
            {
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
