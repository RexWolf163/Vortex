#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using Vortex.Sdk.EffectSpawnSystem.Catalog;

namespace Vortex.Sdk.EffectSpawnSystem.Editor
{
    /// <summary>
    /// Custom-инспектор для <see cref="EffectsCatalog"/>:
    ///  • стандартный массив <c>effects</c>;
    ///  • кнопка <b>Scan Project</b> — добавляет все префабы с <c>EffectView</c>, ещё отсутствующие в каталоге;
    ///  • кнопка <b>Validate</b> — проверка коллизий имён, null-элементов, отсутствия EffectView;
    ///  • live-таблица «key → asset path» под массивом.
    /// </summary>
    [CustomEditor(typeof(EffectsCatalog))]
    public class EffectsCatalogEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            var catalog = (EffectsCatalog)target;

            DrawDefaultInspector();

            EditorGUILayout.Space();
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Scan Project", GUILayout.Height(26)))
                {
                    catalog.ScanProject();
                    AssetDatabase.SaveAssets();
                }
                if (GUILayout.Button("Validate", GUILayout.Height(26)))
                {
                    var issues = catalog.Validate();
                    if (issues.Count == 0)
                        Debug.Log($"[EffectsCatalog] '{catalog.name}': проверка пройдена ({catalog.Effects.Count} префабов).", catalog);
                    else
                        Debug.LogError($"[EffectsCatalog] '{catalog.name}': найдено {issues.Count} проблем:\n" + string.Join("\n", issues), catalog);
                }
            }

            EditorGUILayout.Space();
            DrawKeyList(catalog);
        }

        private static void DrawKeyList(EffectsCatalog catalog)
        {
            var keys = catalog.Keys;
            if (keys.Count == 0)
            {
                EditorGUILayout.HelpBox("Каталог пуст. Перетащите префабы в массив или нажмите Scan Project.", MessageType.Info);
                return;
            }

            EditorGUILayout.LabelField($"Keys ({keys.Count}):", EditorStyles.boldLabel);
            using (new EditorGUI.IndentLevelScope())
            {
                foreach (var key in keys)
                {
                    var prefab = catalog.GetPrefab(key);
                    var path = prefab != null ? AssetDatabase.GetAssetPath(prefab) : "(null)";
                    EditorGUILayout.LabelField(key, path);
                }
            }
        }
    }
}
#endif
