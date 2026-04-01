#if UNITY_EDITOR
using UnityEngine;
using System.Collections.Generic;
using UnityEditor;
using Vortex.Unity.CoreAssetsSystem;
using Vortex.Unity.EditorTools.Attributes;

namespace Vortex.Unity.EditorTools.EditorSettings
{
    [CreateAssetMenu(menuName = "Vortex/EditorTools Settings", fileName = "ToolsSettings")]
    public class ToolsSettings : ScriptableObject, ICoreAsset
    {
#if ODIN_INSPECTOR
        [Hide]
#endif
        [InfoBubble(
            "При включении параметра, кастомный редактор применяет стили списков Vortex ко всем MonoBehaviour.\nПри отключенном - только к реализаторам интерфейса IUseVortexCollectionRendering")]
        [SerializeField, ToggleButton(isSingleButton: true)]
        private bool globalCollectionRendering = true;

        [SerializeField, ToggleButton("Labels", "Colors"), HideLabel]
        private bool isProSkin;

#if ODIN_INSPECTOR
        private bool ShowNoProSkin() => isProSkin;

        [Hide("ShowNoProSkin"), HideLabel]
#endif
        [SerializeField, ToggleBox("isProSkin", 0)]
        private ThemeColors lightColors = ThemeColors.CreateLight();

#if ODIN_INSPECTOR
        private bool ShowProSkin() => !isProSkin;

        [Hide("ShowProSkin"), HideLabel]
#endif
        [SerializeField, ToggleBox("isProSkin", 1)]
        private ThemeColors proColors = ThemeColors.CreatePro();

        public bool GlobalCollectionRendering => globalCollectionRendering;

        public Color GetColor(DefaultColors key)
        {
            var skin = EditorGUIUtility.isProSkin;
            return skin ? proColors.GetColor(key) : lightColors.GetColor(key);
        }


        private static readonly Color FallbackText = new(0f, 0f, 0f, 1f);
        private static readonly Color FallbackBg = new(0.9f, 0.9f, 0.9f, 1f);

        /// <summary>
        /// Цвет для линии
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public static Color GetLineColor(DefaultColors key)
        {
            return GetInstance() != null ? _instance.GetColor(key) : FallbackText;
        }

        /// <summary>
        /// Цвет для фона
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public static Color GetBgColor(DefaultColors key)
        {
            return GetInstance() != null ? _instance.GetColor(key) : FallbackBg;
        }

        private Dictionary<int, string> Labels() => new()
        {
            { 0, "Light" },
            { 1, "Pro" },
        };

        private Dictionary<int, Color> Colors() => new()
        {
            { 0, Color.white },
            { 1, Color.white },
        };

        private static ToolsSettings _instance;

        public static ToolsSettings GetInstance()
        {
            if (_instance != null) return _instance;

            var guids = AssetDatabase.FindAssets("t:ToolsSettings");
            if (guids.Length == 0) return null;

            var path = AssetDatabase.GUIDToAssetPath(guids[0]);
            _instance = AssetDatabase.LoadAssetAtPath<ToolsSettings>(path);
            return _instance;
        }
    }
}
#endif