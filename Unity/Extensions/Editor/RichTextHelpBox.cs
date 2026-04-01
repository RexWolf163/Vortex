using UnityEditor;
using UnityEngine;

namespace Vortex.Unity.Extensions.Editor
{
    public static class RichTextHelpBox
    {
#if UNITY_EDITOR
        /// <summary>
        /// Расширенный HelpBox с поддержкой Rich Text
        /// </summary>
        public static void Create(Rect rect, string richTextMessage, MessageType messageType)
        {
            // Настраиваем GUI-style с поддержкой Rich Text
            GUIStyle style = new GUIStyle(EditorStyles.helpBox);
            style.richText = true;

            // Выводим сообщение с Rich Text
            EditorGUI.LabelField(rect, richTextMessage, style);

            // Обводка для визуализации рамки, аналогичной HelpBox
            if (messageType != MessageType.None)
            {
                Color color = Color.clear;
                switch (messageType)
                {
                    case MessageType.Info:
                        color = new Color(0.5f, 0.5f, 1f, 0.0f); // синий оттенок
                        break;
                    case MessageType.Warning:
                        color = new Color(0.8f, 0.647f, 0f, 0.2f); // оранжевый оттенок
                        break;
                    case MessageType.Error:
                        color = new Color(1f, 0f, 0f, 0.2f); // красный оттенок
                        break;
                }

                // Нарисовать рамку прямоугольника
                EditorGUI.DrawRect(rect, color);
            }
        }

        // Удобная перегруженная версия для использования с EditorGUILayout
        public static void Create(string richTextMessage, MessageType messageType, int height = 50)
        {
            var rect = EditorGUILayout.GetControlRect(false, height);
            Create(rect, richTextMessage, messageType);
        }
#endif
    }
}