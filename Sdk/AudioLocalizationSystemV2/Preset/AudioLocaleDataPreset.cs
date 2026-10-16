using System;
using System.Collections.Generic;
using System.Linq;
using Sirenix.OdinInspector;
using Sirenix.Utilities;
using UnityEngine;
using Vortex.Core.AssetHandleSystem;
using Vortex.Core.DatabaseSystem.Model.Enums;
using Vortex.Core.LocalizationSystem.Bus;
using Vortex.Sdk.AudioLocalizationSystemV2.Model;
using Vortex.Unity.AudioSystem.Attributes;
using Vortex.Unity.DatabaseSystem.Presets;
using Vortex.Unity.EditorTools.Attributes;
using Vortex.Unity.LocalizationSystem;
#if UNITY_EDITOR
using System.IO;
using UnityEditor;
#endif

namespace Vortex.Sdk.AudioLocalizationSystemV2.Preset
{
    [CreateAssetMenu(fileName = "AudioLocaleData", menuName = "Database/AudioLocaleDataV2")]
    public class AudioLocaleDataPreset : RecordPreset<AudioLocaleData>
    {
        [Serializable, ClassLabel("$Label")]
        private class LangGroup
        {
            [Language] public string language;

            /// <summary>
            /// Клип реплики. Реализацию (прямая ссылка / addressable с группой кэша) выбирает дизайнер.
            /// </summary>
            [SerializeReference] public AssetHandle<AudioClip> audio;

#if UNITY_EDITOR
            private string Label()
            {
                if (audio == null)
                    return $"Language: {language} => [NULL]";
                return $"Language: {language} => {audio.GetType().Name}";
            }
#endif
        }

        [SerializeField, LocalizationKey] private string textGuid;

        /// <summary>
        /// Ключ локализации строки которую нужно озвучить
        /// </summary>
        public string TextGuid => textGuid;

        [SerializeField, AudioChannelName] private string channel;

        /// <summary>
        /// Аудиоканал воспроизведения реплики
        /// </summary>
        public string Channel => channel;

        [SerializeField, InfoBox("$ValidationMessage", InfoMessageType.Error, "$HasValidationError")]
        private List<LangGroup> voices;

        /// <summary>
        /// Индекс Язык=> хэндл звука
        /// </summary>
        public Dictionary<string, AssetHandle<AudioClip>> Voices =>
            voices.ToDictionary(d => d.language, d => d.audio);

#if UNITY_EDITOR
        private void OnValidate() => type = RecordTypes.Singleton;

        #region Валидация

        private const double ValidationInterval = 1.0;

        private double _validationTime;
        private string _validationMessage;

        private bool HasValidationError => !string.IsNullOrEmpty(ValidationMessage);

        /// <summary>
        /// Сообщение валидации для инспектора. Odin опрашивает условие на каждой перерисовке,
        /// а проверка клипов идёт через SerializedObject — поэтому результат кешируется на секунду.
        /// </summary>
        private string ValidationMessage
        {
            get
            {
                var now = EditorApplication.timeSinceStartup;
                if (_validationMessage != null && now - _validationTime < ValidationInterval)
                    return _validationMessage;

                _validationTime = now;
                _validationMessage = Validate();
                return _validationMessage;
            }
        }

        /// <summary>
        /// Нарушения, которые иначе всплывут только в рантайме: повтор языка роняет построение записи
        /// (ToDictionary), пустой ключ делает реплику недостижимой, пустой хэндл/клип или канал — немой.
        /// Клип проверяется только у известных реализаций хэндла (поле asset / reference).
        /// </summary>
        private string Validate()
        {
            var errors = new List<string>();
            if (textGuid.IsNullOrWhitespace())
                errors.Add("ключ локализации не задан — реплика недостижима");
            if (channel.IsNullOrWhitespace())
                errors.Add("аудиоканал не задан");

            if (voices == null || voices.Count == 0)
                return string.Join("\n", errors);

            using var so = new SerializedObject(this);
            var languages = new HashSet<string>();
            for (var i = 0; i < voices.Count; i++)
            {
                var voice = voices[i];
                if (voice.language.IsNullOrWhitespace())
                    errors.Add($"[{i}] язык не задан");
                else if (!languages.Add(voice.language))
                    errors.Add($"[{i}] язык «{voice.language}» уже есть — запись не построится");

                if (voice.audio == null)
                {
                    errors.Add($"[{i}] хэндл клипа не задан");
                    continue;
                }

                var handle = GetHandleProperty(so, i);
                var known = handle.FindPropertyRelative("asset") != null ||
                            handle.FindPropertyRelative("reference") != null;
                if (known && GetHandleAsset(handle) == null)
                    errors.Add($"[{i}] клип не назначен");
            }

            return string.Join("\n", errors);
        }

        #endregion

        /// <summary>
        /// Подразумевается что ключ локали будет начинатсья с ключа персонажа произносящего фразу
        /// с точкой, отделяющей от остального ключа
        /// </summary>
        [FoldoutGroup("Грубый инструментарий массовой замены")]
        [Button("Update Voices Array", ButtonSizes.Medium)]
        private void UpdateVoices()
        {
            if (voices != null && voices.Count > 0)
            {
                var so = new SerializedObject(this);
                for (int i = 0; i < voices.Count; i++)
                {
                    var voice = voices[i];
                    if (voice.audio == null) continue;

                    var handle = GetHandleProperty(so, i);
                    var oldClip = GetHandleAsset(handle);
                    if (oldClip == null || string.IsNullOrEmpty(oldClip.name)) continue;

                    var dotIndex = oldClip.name.IndexOf('.');
                    if (dotIndex < 0) continue;

                    var newName = $"{TextGuid}.{voice.language}";

                    var newClip = FindClip(newName);
                    if (newClip == null) continue;

                    SetHandleAsset(handle, newClip);
                }

                so.ApplyModifiedProperties();
            }

            EditorUtility.SetDirty(this);
        }

        [ShowInInspector, HorizontalGroup("Грубый инструментарий массовой замены/NewChar")]
        private string charName;

        /// <summary>
        /// Подразумевается что ключ локали будет начинатсья с ключа персонажа произносящего фразу
        /// с точкой, отделяющей от остального ключа
        /// </summary>
        [Button("Замена первой части ключа", ButtonSizes.Medium),
         HorizontalGroup("Грубый инструментарий массовой замены/NewChar")]
        private void ChangeChar()
        {
            if (string.IsNullOrEmpty(charName)) return;

            var dotIndex = textGuid.IndexOf('.');
            if (dotIndex < 0) return;
            var oldSuffix = textGuid.Substring(dotIndex + 1);
            var candidateTextKey = $"{charName}.{oldSuffix}";
            var matchedTextKey = Localization.GetLocalizationKeys().FirstOrDefault(k =>
                string.Equals(k, candidateTextKey, StringComparison.OrdinalIgnoreCase));
            if (string.IsNullOrEmpty(matchedTextKey)) return;

            textGuid = matchedTextKey;

            // Та же логика для аудио: первая часть имени клипа (до точки) меняется на charName,
            // потом ищем клип с таким именем и перенацеливаем хэндл.
            if (voices != null && voices.Count > 0)
            {
                var so = new SerializedObject(this);
                for (int i = 0; i < voices.Count; i++)
                {
                    var voice = voices[i];
                    if (voice.audio == null) continue;

                    var handle = GetHandleProperty(so, i);
                    var oldClip = GetHandleAsset(handle);
                    if (oldClip == null || string.IsNullOrEmpty(oldClip.name)) continue;

                    var audioDot = oldClip.name.IndexOf('.');
                    if (audioDot < 0) continue;

                    var audioSuffix = oldClip.name.Substring(audioDot + 1);
                    var newAudioName = $"{charName}.{audioSuffix}";

                    var newClip = FindClip(newAudioName);
                    if (newClip == null) continue;

                    SetHandleAsset(handle, newClip);
                }

                so.ApplyModifiedProperties();
            }

            EditorUtility.SetDirty(this);
        }

        [Language, ShowInInspector, HorizontalGroup("Грубый инструментарий массовой замены/NewLang")]
        private string _newLanguage;

        /// <summary>
        /// Подразумевается что в ключе есть участок с указанием языка через его id в виде ".{lang}."
        /// Например Hero.Text1.ru.2
        /// </summary>
        [Button("Скопировать первую запись под другой язык", ButtonSizes.Medium),
         HorizontalGroup("Грубый инструментарий массовой замены/NewLang")]
        private void AddLanguageEntry()
        {
            if (string.IsNullOrEmpty(_newLanguage)) return;

            voices ??= new List<LangGroup>();

            // Запись для этого языка уже есть — ничего не делаем
            if (voices.Any(v => v.language == _newLanguage)) return;

            // Если voices пустой — первая запись без шаблона
            if (voices.Count == 0)
            {
                voices.Add(new LangGroup { language = _newLanguage, audio = null });
                EditorUtility.SetDirty(this);
                return;
            }

            // Берём первый элемент как образец
            var template = voices[0];
            if (template.audio == null)
                return;

            var oldClip = GetHandleAsset(GetHandleProperty(new SerializedObject(this), 0));
            if (oldClip == null)
                return;
            var newAudioName = oldClip.name;

            // Меняем .{templateLang}. на .{newLanguage}. в имени клипа образца
            if (!string.IsNullOrEmpty(template.language))
            {
                newAudioName = newAudioName.Replace($".{template.language}", $".{_newLanguage}");
            }

            var newClip = FindClip(newAudioName) ??
                          throw new InvalidOperationException($"AudioClip «{newAudioName}» not found");

            // Хэндл той же реализации, что у образца (прямая ссылка / addressable), перенацеленный на новый клип
            voices.Add(new LangGroup
            {
                language = _newLanguage,
                audio = (AssetHandle<AudioClip>)Activator.CreateInstance(template.audio.GetType())
            });

            var so = new SerializedObject(this);
            SetHandleAsset(GetHandleProperty(so, voices.Count - 1), newClip);
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(this);
        }

        private static SerializedProperty GetHandleProperty(SerializedObject so, int index) =>
            so.FindProperty($"{nameof(voices)}.Array.data[{index}].{nameof(LangGroup.audio)}");

        /// <summary>
        /// Ассет, на который указывает хэндл. Реализации различаются полем-носителем:
        /// прямая ссылка — <c>asset</c>, addressable — <c>reference</c> (AssetReference с GUID ассета).
        /// </summary>
        private static AudioClip GetHandleAsset(SerializedProperty handle)
        {
            var direct = handle.FindPropertyRelative("asset");
            if (direct != null)
                return direct.objectReferenceValue as AudioClip;

            var guid = handle.FindPropertyRelative("reference.m_AssetGUID");
            if (guid == null || guid.stringValue.IsNullOrWhitespace())
                return null;
            return AssetDatabase.LoadAssetAtPath<AudioClip>(AssetDatabase.GUIDToAssetPath(guid.stringValue));
        }

        /// <summary>
        /// Перенацелить хэндл на клип. См. <see cref="GetHandleAsset"/> о поле-носителе.
        /// </summary>
        private static void SetHandleAsset(SerializedProperty handle, AudioClip clip)
        {
            var direct = handle.FindPropertyRelative("asset");
            if (direct != null)
            {
                direct.objectReferenceValue = clip;
                return;
            }

            var guid = handle.FindPropertyRelative("reference.m_AssetGUID");
            if (guid == null)
                throw new InvalidOperationException($"Unsupported asset handle «{handle.managedReferenceFullTypename}»");
            guid.stringValue = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(clip));
            var subObject = handle.FindPropertyRelative("reference.m_SubObjectName");
            if (subObject != null)
                subObject.stringValue = string.Empty;
        }

        /// <summary>
        /// Клип по точному имени файла (без учёта регистра).
        /// </summary>
        private static AudioClip FindClip(string clipName) =>
            AssetDatabase.FindAssets($"t:AudioClip {clipName}")
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(p => string.Equals(Path.GetFileNameWithoutExtension(p), clipName,
                    StringComparison.OrdinalIgnoreCase))
                .Select(AssetDatabase.LoadAssetAtPath<AudioClip>)
                .FirstOrDefault();
#endif
    }
}
