#if UNITY_EDITOR
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using Vortex.Unity.AudioSystem.Presets;

namespace Vortex.Unity.AudioSystem.Editor
{
    /// <summary>
    /// Тулза для массовой обертки аудио клипов в ассеты типа Sound
    /// </summary>
    public static class AudioAssetsCombiner
    {
        [MenuItem("Assets/Vortex/Create Audio Assets", false, 1000)]
        private static void CreateAudioAssets(MenuCommand menuCommand)
        {
            var audioClips = Selection.GetFiltered<AudioClip>(SelectionMode.Assets);

            if (audioClips.Length == 0)
            {
                Debug.LogWarning("No audio clips selected");
                return;
            }

            string targetPath = GetTargetPath();

            foreach (var audioClip in audioClips)
            {
                CreateSoundAsset(audioClip, targetPath);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"Created {audioClips.Length} sound asset(s)");
        }

        [MenuItem("Assets/Vortex/Create Audio Assets", true)]
        private static bool ValidateCreateAudioAssets()
        {
            var audioClips = Selection.GetFiltered<AudioClip>(SelectionMode.Assets);
            return audioClips.Length > 0;
        }

        private static void CreateSoundAsset(AudioClip audioClip, string path)
        {
            var soundPreset = ScriptableObject.CreateInstance<SoundSamplePreset>();

            var cleanName = $"_{CleanFileName(audioClip.name)}";

            // Устанавливаем description через отражение, т.к. это protected поле из RecordPreset
            var descriptionField =
                typeof(Vortex.Unity.DatabaseSystem.Presets.RecordPreset<Vortex.Unity.AudioSystem.Model.Sound>)
                    .GetField("description",
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            descriptionField?.SetValue(soundPreset, "Автогенерированный ассет");

            // Устанавливаем audioClips через отражение, т.к. поле private
            var audioClipsField = typeof(SoundSamplePreset)
                .GetField("audioClips",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            audioClipsField?.SetValue(soundPreset, new AudioClip[] { audioClip });

            var assetPath = Path.Combine(path, $"{cleanName}.asset");
            assetPath = AssetDatabase.GenerateUniqueAssetPath(assetPath);

            AssetDatabase.CreateAsset(soundPreset, assetPath);
        }

        private static string CleanFileName(string fileName)
        {
            // Удаляем все знаки препинания и специальные символы
            var cleanName = Regex.Replace(fileName, @"[^\w\s-]", "");
            // Заменяем пробелы на подчеркивания
            cleanName = Regex.Replace(cleanName, @"\s+", "_");
            // Удаляем множественные подчеркивания
            cleanName = Regex.Replace(cleanName, @"_+", "_");
            // Удаляем подчеркивания в начале и конце
            cleanName = cleanName.Trim('_');

            return cleanName;
        }

        private static string GetTargetPath()
        {
            // Пытаемся определить путь из выделенных объектов
            if (Selection.objects.Length > 0)
            {
                string path = AssetDatabase.GetAssetPath(Selection.objects[0]);
                if (!string.IsNullOrEmpty(path))
                {
                    // Если выделен файл, берем его директорию
                    if (File.Exists(path))
                        return Path.GetDirectoryName(path);
                    // Если выделена папка, используем её
                    if (Directory.Exists(path))
                        return path;
                }
            }

            // По умолчанию создаем в Assets/Audio
            string defaultPath = "Assets/Audio";
            if (!Directory.Exists(defaultPath))
                Directory.CreateDirectory(defaultPath);

            return defaultPath;
        }
    }
}
#endif