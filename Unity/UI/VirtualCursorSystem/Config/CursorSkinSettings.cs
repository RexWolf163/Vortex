using System.Collections.Generic;
using UnityEngine;
using Vortex.Unity.EditorTools.Attributes;

namespace Vortex.Unity.UI.VirtualCursorSystem
{
    /// <summary>
    /// SO-каталог тем курсора + ГЛОБАЛЬНЫЕ тиры разрешения. Тиры — единый источник брейкпоинтов
    /// (по возрастанию), каждая тема даёт по одному паку на тир. Выбор темы в рантайме —
    /// <see cref="CursorSkinSelector"/>. Загружается бутстрапом пакета и передаётся в
    /// <see cref="VirtualCursorController.Init"/>.
    /// </summary>
    [CreateAssetMenu(fileName = "CursorSkinSettings", menuName = "Vortex/Cursor/Cursor Skin Settings")]
    public class CursorSkinSettings : ScriptableObject
    {
        [SerializeField, ValueSelector("GetSkinsPacks"),
         Tooltip("Ключ темы по умолчанию (если выбранная не задана/не найдена).")]
        private string defaultSetKey;

        [SerializeField, Tooltip("Каталог тем курсора.")]
        private CursorSkinPack[] sets = new CursorSkinPack[0];

        public string DefaultSetKey => defaultSetKey;
        public CursorSkinPack[] Sets => sets;

        /// <summary>Тема по ключу; дефолтная, если ключ пуст/не найден; первая — если и дефолт не найден.</summary>
        public CursorSkinPack GetPack(string key)
        {
            CursorSkinPack first = null;
            CursorSkinPack def = null;
            foreach (var s in sets)
            {
                if (s == null) continue;
                first ??= s;
                if (!string.IsNullOrEmpty(key) && s.Key == key)
                    return s;
                if (s.Key == defaultSetKey)
                    def = s;
            }

            return def ?? first;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
        }

        private List<string> GetSkinsPacks()
        {
            var res = new List<string>();
            foreach (var cursorSkinPack in sets)
            {
                res.Add(cursorSkinPack.Key);
            }

            return res;
        }
#endif
    }
}