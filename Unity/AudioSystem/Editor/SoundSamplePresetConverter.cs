#if UNITY_EDITOR && ENABLE_ADDRESSABLES
using System.Reflection;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;
using Vortex.Unity.AudioSystem.Model;
using Vortex.Unity.AudioSystem.Presets;
using Object = UnityEngine.Object;

namespace Vortex.Unity.AudioSystem.Editor
{
    /// <summary>
    /// Конвертер <see cref="SoundSamplePreset"/> → <see cref="SoundSampleAddressablePreset"/>
    /// через контекстное меню Project (ПКМ по выделенным ассетам).
    ///
    /// Ключевое: сохраняется framework-GUID (<see cref="Vortex.Unity.DatabaseSystem.Presets.RecordPreset{T}.GuidPreset"/>),
    /// поэтому ссылки <c>[DbRecord]</c> не рвутся — они хранят <c>GuidPreset</c>, а не Unity-GUID ассета.
    /// Unity-GUID ассета при замене меняется (это не влияет на доменные ссылки фреймворка).
    ///
    /// Прямые <c>AudioClip</c>-ссылки переводятся в <see cref="AssetReferenceAudioClip"/>; если клип
    /// ещё не addressable — он регистрируется в дефолтной группе.
    /// </summary>
    public static class SoundSamplePresetConverter
    {
        private const BindingFlags FieldFlags = BindingFlags.NonPublic | BindingFlags.Instance;

        [MenuItem("Assets/Vortex/Convert to Addressable Sound", false, 1001)]
        private static void Convert()
        {
            var presets = Selection.GetFiltered<SoundSamplePreset>(SelectionMode.Assets);
            if (presets.Length == 0)
                return;

            var settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null)
            {
                Debug.LogError("[SoundSamplePresetConverter] Не найдены Addressable Settings. " +
                               "Создай их: Window → Asset Management → Addressables → Groups.");
                return;
            }

            if (!EditorUtility.DisplayDialog(
                    "Convert to Addressable Sound",
                    $"Преобразовать {presets.Length} пресет(ов) в SoundSampleAddressablePreset?\n\n" +
                    "Исходные ассеты заменяются на месте. Framework-GUID (GuidPreset) сохраняется — " +
                    "ссылки [DbRecord] не порвутся. Unity-GUID ассета сменится.",
                    "Преобразовать", "Отмена"))
                return;

            var converted = 0;
            foreach (var preset in presets)
                if (ConvertOne(preset, settings))
                    converted++;

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[SoundSamplePresetConverter] Преобразовано {converted}/{presets.Length}.");
        }

        [MenuItem("Assets/Vortex/Convert to Addressable Sound", true)]
        private static bool Validate() =>
            Selection.GetFiltered<SoundSamplePreset>(SelectionMode.Assets).Length > 0;

        private static bool ConvertOne(SoundSamplePreset source, AddressableAssetSettings settings)
        {
            var path = AssetDatabase.GetAssetPath(source);
            if (string.IsNullOrEmpty(path))
                return false;

            var target = ScriptableObject.CreateInstance<SoundSampleAddressablePreset>();

            // 1. Копируем все общие сериализованные поля (guid, nameRecord, description, icon, type,
            //    pitchRange, valueRange, channel). Пропускаем m_Script и audioClips — у них разный тип.
            //    CopyFromSerializedProperty переносит свойства по совпадающему propertyPath, поэтому
            //    не зависит от точных имён полей RecordPreset и устойчиво к их переименованию.
            var srcSo = new SerializedObject(source);
            var dstSo = new SerializedObject(target);
            var it = srcSo.GetIterator();
            if (it.NextVisible(true))
            {
                do
                {
                    if (it.propertyPath == "m_Script" || it.propertyPath == "audioClips")
                        continue;
                    dstSo.CopyFromSerializedProperty(it);
                } while (it.NextVisible(false));
            }

            dstSo.ApplyModifiedPropertiesWithoutUndo();

            // 2. AudioClip[] → AssetReferenceAudioClip[] (+ регистрация клипа в Addressables при необходимости).
            var srcClips = (AudioClip[])typeof(SoundSamplePreset)
                .GetField("audioClips", FieldFlags)?.GetValue(source);
            typeof(SoundSampleAddressablePreset)
                .GetField("audioClips", FieldFlags)?
                .SetValue(target, BuildRefs(srcClips, settings));

            // 3. Замена на месте: тот же путь, framework-GUID уже скопирован → [DbRecord]-ссылки живут.
            EditorUtility.SetDirty(target);
            if (!AssetDatabase.DeleteAsset(path))
            {
                Debug.LogError($"[SoundSamplePresetConverter] Не удалось удалить исходный ассет: {path}");
                Object.DestroyImmediate(target);
                return false;
            }

            AssetDatabase.CreateAsset(target, path);
            return true;
        }

        private static AssetReferenceAudioClip[] BuildRefs(AudioClip[] clips, AddressableAssetSettings settings)
        {
            if (clips == null)
                return new AssetReferenceAudioClip[0];

            var refs = new AssetReferenceAudioClip[clips.Length];
            for (var i = 0; i < clips.Length; i++)
            {
                var clip = clips[i];
                if (clip == null)
                    continue;

                var clipGuid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(clip));

                // Клип должен быть addressable, иначе AssetReference не разрезолвится в рантайме.
                if (settings.FindAssetEntry(clipGuid) == null)
                    settings.CreateOrMoveEntry(clipGuid, settings.DefaultGroup);

                refs[i] = new AssetReferenceAudioClip(clipGuid);
            }

            return refs;
        }
    }
}
#endif
