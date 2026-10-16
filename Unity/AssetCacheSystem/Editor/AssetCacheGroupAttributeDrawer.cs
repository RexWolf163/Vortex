#if UNITY_EDITOR && ODIN_INSPECTOR
using System;
using System.Linq;
using Sirenix.OdinInspector.Editor;
using UnityEditor;
using UnityEngine;
using Vortex.Core.Extensions.LogicExtensions;
using Vortex.Unity.AssetCacheSystem.Attributes;
using Vortex.Unity.AssetCacheSystem.Config;
using Vortex.Unity.EditorTools.Elements;
using Vortex.Unity.Extensions;

namespace Vortex.Unity.AssetCacheSystem.Editor
{
    /// <summary>
    /// Odin-drawer для <see cref="AssetCacheGroupAttribute"/>.
    /// Заменяет string-значение на SearchablePopup с группами из <see cref="AssetCacheSettings"/>.
    /// Визуал — как у <c>ValueSelector</c>: невыбранное значение рисуется красным NULL-полем.
    /// Кнопка «×» сбрасывает выбор — поле возвращается к дефолтной группе.
    /// Чтение и запись — через <c>ValueEntry.SmartValue</c>, без зависимости от SerializedProperty.
    /// </summary>
    public sealed class AssetCacheGroupAttributeDrawer : OdinAttributeDrawer<AssetCacheGroupAttribute, string>
    {
        private const double CacheTtlSeconds = 1.0;
        private const float ClearButtonWidth = 20f;

        // Общий для всех полей кеш ключей: AssetDatabase.FindAssets на каждый OnGUI — дорого,
        // а настройки меняются редко. Сброс по TTL — правка конфига видна через секунду.
        private static string[] _cachedKeys;
        private static double _cacheTimestamp;

        protected override void DrawPropertyLayout(GUIContent label)
        {
            var rect = EditorGUILayout.GetControlRect();
            if (label != null)
                rect = EditorGUI.PrefixLabel(rect, label);

            var keys = GetKeys();
            var current = ValueEntry.SmartValue;
            var isEmpty = current.IsNullOrWhitespace();

            if (!isEmpty)
            {
                var clearRect = new Rect(rect.xMax - ClearButtonWidth, rect.y, ClearButtonWidth, rect.height);
                rect.width -= ClearButtonWidth + 2f;
                if (GUI.Button(clearRect, "×"))
                {
                    ValueEntry.SmartValue = string.Empty;
                    return;
                }
            }

            var currentIndex = isEmpty ? -1 : Array.IndexOf(keys, current);
            // Ключ, которого нет в настройках (опечатка/группа удалена), показываем как есть — красным.
            var placeholder = !isEmpty && currentIndex < 0 ? $"{current} [нет в AssetCacheSettings]" : null;

            DrawingUtility.DrawSelector(
                rect,
                current,
                keys,
                newValue => ValueEntry.SmartValue = newValue,
                currentIndex,
                placeholder);
        }

        private static string[] GetKeys()
        {
            var now = EditorApplication.timeSinceStartup;
            if (_cachedKeys != null && now - _cacheTimestamp <= CacheTtlSeconds)
                return _cachedKeys;

            var settings = AssetDatabaseExt.GetSingletonAsset<AssetCacheSettings>();
            _cachedKeys = settings == null
                ? new string[0]
                : settings.GetGroupKeys().Where(k => !k.IsNullOrWhitespace()).Distinct().ToArray();
            _cacheTimestamp = now;
            return _cachedKeys;
        }
    }
}
#endif
