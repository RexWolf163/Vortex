using UnityEngine;
using Vortex.Unity.EditorTools.Attributes;

namespace Vortex.Unity.DebugSystem
{
    public partial class DebugSettings
    {
        [SerializeField] [ToggleButton(isSingleButton: true)]
        [Tooltip("Ошибка чтения глобального хранилища или дубль ключа модуля останавливают загрузку (только редактор). " +
                 "Выключено — как в билде: значения по умолчанию и лог.")]
        private bool globalSaveFailFast = true;

        /// <summary>
        /// Fail-fast для ошибок глобального сохранения. Не подчинён <see cref="DebugMode"/> — осознанное
        /// отклонение от соглашения: это не флаг логов, и выключение логов не должно молча выключать fail-fast.
        /// В билде всегда <c>false</c>.
        /// </summary>
        public bool GlobalSaveFailFast => Application.isEditor && globalSaveFailFast;
    }
}
