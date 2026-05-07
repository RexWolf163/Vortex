using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;
using Vortex.Sdk.EffectSpawnSystem.Components;
#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
#endif

namespace Vortex.Sdk.EffectSpawnSystem.Catalog
{
    /// <summary>
    /// Единый индекс префабов-эффектов проекта.
    /// Регистрируется в шине через <c>EffectSpawn.RegisterCatalog(catalog)</c> из стартового кода.
    ///
    /// Ключ значения — <c>prefab.name</c>. Имя префаба = имя файла без расширения.
    /// Дубликаты имён ловятся в редакторе кнопкой Validate.
    /// </summary>
    [CreateAssetMenu(fileName = "EffectsCatalog", menuName = "Vortex/Effects Catalog")]
    public class EffectsCatalog : ScriptableObject
    {
        [SerializeField, InfoBox("Префабы эффектов с компонентом EffectView. Ключ для спауна = prefab.name.")]
        [LabelText("Effects")]
        private GameObject[] effects = Array.Empty<GameObject>();

        private Dictionary<string, GameObject> _byKey;
        private string[] _keys;

        /// <summary>Все ключи каталога в порядке объявления.</summary>
        public IReadOnlyList<string> Keys
        {
            get
            {
                EnsureBuilt();
                return _keys;
            }
        }

        /// <summary>Префабы каталога (только для редактора и итераций; модификация не поддерживается).</summary>
        public IReadOnlyList<GameObject> Effects => effects;

        /// <summary>Префаб по ключу или <c>null</c>, если ключ не найден.</summary>
        public GameObject GetPrefab(string key)
        {
            if (string.IsNullOrEmpty(key)) return null;
            EnsureBuilt();
            return _byKey.TryGetValue(key, out var go) ? go : null;
        }

        private void OnEnable() => Invalidate();
        private void OnValidate() => Invalidate();

        private void Invalidate()
        {
            _byKey = null;
            _keys = null;
        }

        private void EnsureBuilt()
        {
            if (_byKey != null) return;
            _byKey = new Dictionary<string, GameObject>(effects.Length);
            var keys = new List<string>(effects.Length);
            foreach (var e in effects)
            {
                if (e == null) continue;
                var key = e.name;
                if (string.IsNullOrEmpty(key)) continue;
                _byKey[key] = e; // дубликаты — последний выигрывает; диагностируется кнопкой Validate
                keys.Add(key);
            }

            _keys = keys.ToArray();
        }

#if UNITY_EDITOR
        [Button("Scan Project", ButtonSizes.Medium), PropertyOrder(10), HorizontalGroup("btns")]
        [Tooltip(
            "Сканирует AssetDatabase, добавляет все префабы с EffectView, которые ещё не присутствуют в массиве. Существующие записи не трогает.")]
        private void ScanProject()
        {
            var existing = new HashSet<GameObject>(effects);
            var found = new List<GameObject>();
            var guids = AssetDatabase.FindAssets("t:Prefab");
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (go == null) continue;
                if (go.GetComponent<EffectView>() == null) continue;
                if (existing.Contains(go)) continue;
                found.Add(go);
            }

            if (found.Count == 0)
            {
                Debug.Log($"[EffectsCatalog] '{name}': новых префабов не найдено.", this);
                return;
            }

            var combined = new List<GameObject>(effects);
            combined.AddRange(found);
            effects = combined.ToArray();
            EditorUtility.SetDirty(this);
            AssetDatabase.SaveAssets();
            Invalidate();

            Debug.Log($"[EffectsCatalog] '{name}': добавлено {found.Count} новых префабов.", this);
        }

        [Button("Validate", ButtonSizes.Medium), PropertyOrder(11), HorizontalGroup("btns")]
        private void Validate()
        {
            var issues = new List<string>();
            var seen = new HashSet<string>();

            for (int i = 0; i < effects.Length; i++)
            {
                var e = effects[i];
                if (e == null)
                {
                    issues.Add($"[{i}] null-элемент в массиве.");
                    continue;
                }

                if (e.GetComponent<EffectView>() == null)
                    issues.Add($"[{i}] '{e.name}' не имеет компонента EffectView.");
                if (!seen.Add(e.name))
                    issues.Add($"[{i}] дубликат имени '{e.name}'.");
            }

            if (issues.Count == 0)
                Debug.Log($"[EffectsCatalog] '{name}': проверка пройдена ({effects.Length} префабов).", this);
            else
                Debug.LogError(
                    $"[EffectsCatalog] '{name}': найдено {issues.Count} проблем:\n" + string.Join("\n", issues), this);
        }

        [ShowInInspector, PropertyOrder(20)]
        [LabelText("Index preview")]
        [DictionaryDrawerSettings(KeyLabel = "Key", ValueLabel = "Asset path", IsReadOnly = true)]
        [InfoBox("Только для просмотра — что будет видно потребителю при чтении Keys/GetPrefab.")]
        private Dictionary<string, string> IndexPreview
        {
            get
            {
                var result = new Dictionary<string, string>();
                if (effects == null) return result;
                foreach (var e in effects)
                {
                    if (e == null) continue;
                    var key = e.name;
                    if (result.ContainsKey(key)) continue; // дубликаты — диагностируются Validate
                    result[key] = AssetDatabase.GetAssetPath(e);
                }

                return result;
            }
        }
#endif
    }
}