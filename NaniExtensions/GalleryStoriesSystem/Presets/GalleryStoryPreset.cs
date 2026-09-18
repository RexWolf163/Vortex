#if USING_NANINOVELL
using System;
using Naninovel;
using Sirenix.OdinInspector;
using UnityEngine;
using Vortex.NaniExtensions.GalleryStoriesSystem.Models;
using Vortex.Unity.DatabaseSystem.Presets;

namespace Vortex.NaniExtensions.GalleryStoriesSystem.Presets
{
    /// <summary>
    /// Пресет нарративной галлерейной карточки. Наследует базовые поля
    /// <c>RecordPreset&lt;T&gt;</c>: Guid, Name, Description, Icon (превью).
    ///
    /// Nani-скрипт хранится driver-нейтральной строкой пути (<see cref="scriptPath"/>).
    /// Для удобного заполнения — вспомогательное non-serialized поле <see cref="script"/>:
    /// дизайнер бросает Script-ассет, <see cref="OnScriptChanged"/> копирует его
    /// <c>Path</c> в <see cref="scriptPath"/> и обнуляет ссылку. Так драг-н-дроп работает,
    /// но сама ссылка на ScriptableObject не сохраняется — надёжно против nani-выгрузки
    /// (<c>ScriptPlayer.ResetService()</c> обнуляет загруженный Script-инстанс).
    /// </summary>
    [CreateAssetMenu(fileName = "GalleryStoryPreset",
        menuName = "Vortex/Presets/Gallery/Story")]
    public class GalleryStoryPreset : RecordPreset<GalleryStoryModel>
    {
        [SerializeField, Naninovel.ReadOnly]
        [InfoBox("$" + nameof(ScriptPathValidationMessage),
            "$" + nameof(ScriptPathValidationType))]
        private string scriptPath;

        [NonSerialized, ShowInInspector]
        [Tooltip("Перетащи сюда nani-скрипт — его Path скопируется в scriptPath, а поле очистится. " +
                 "Ссылка на Script не сериализуется (защита от выгрузки инстанса самой naninovel).")]
        [OnValueChanged(nameof(OnScriptChanged))]
        private Script script;

        /// <summary>
        /// Read-only accessor для <see cref="GalleryStoryModel.CopyFrom"/>: модель копирует
        /// строку пути (string immutable — deep-clone не нужен).
        /// </summary>
        public string ScriptPathTemplate => scriptPath;

#if UNITY_EDITOR
        private void OnScriptChanged()
        {
            if (script == null) return;

            scriptPath = script.Path;
            script = null;
            UnityEditor.EditorUtility.SetDirty(this);
        }

        // ================= InfoBox validation =================

        // Сообщение InfoBox — формируется в зависимости от текущего состояния scriptPath.
        // Динамически подставляется через "$-syntax" в атрибуте.
        private string ScriptPathValidationMessage
        {
            get
            {
                if (string.IsNullOrWhiteSpace(scriptPath))
                    return "Путь к скрипту не задан. Перетащите nani-скрипт в поле Script.";

                var found = FindScriptByPath(scriptPath);
                return found != null
                    ? $"OK: '{found.name}' (Path: '{scriptPath}')"
                    : $"Скрипт с Path '{scriptPath}' не найден в проекте. Проверьте, что nani-скрипт существует и его Path совпадает.";
            }
        }

        private InfoMessageType ScriptPathValidationType
        {
            get
            {
                if (string.IsNullOrWhiteSpace(scriptPath)) return InfoMessageType.Warning;
                return FindScriptByPath(scriptPath) != null
                    ? InfoMessageType.Info
                    : InfoMessageType.Error;
            }
        }

        /// <summary>
        /// Найти в проекте <see cref="Script"/>-ассет по <c>Path</c>. Editor-only, используется
        /// InfoBox-валидацией. Итерирует все ассеты типа <c>Script</c>: их обычно единицы-десятки
        /// в проекте, стоимость — миллисекунды при OnValidate.
        /// </summary>
        private static Script FindScriptByPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return null;

            var guids = UnityEditor.AssetDatabase.FindAssets("t:Script");
            foreach (var guid in guids)
            {
                var assetPath = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
                var s = UnityEditor.AssetDatabase.LoadAssetAtPath<Script>(assetPath);
                // .cs-файлы под "t:Script" тоже попадают как MonoScript — LoadAssetAtPath<Script> вернёт null.
                if (s != null && s.Path == path) return s;
            }

            return null;
        }
#endif
    }
}
#endif