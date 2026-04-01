#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Vortex.Unity.Extensions.Editor.Templates
{
    /// <summary>
    /// AI-Generation
    /// 
    /// Окно ввода названия системы, которая разворачивается из шаблона
    /// </summary>
    public class VtpGeneratorWindow : EditorWindow
    {
        private string _className = "";
        private static string _templatePath;
        private static string _outputFolder;
        private static Dictionary<string, string> _substitutions = new();

        /// <summary>
        /// Показать окно для ввода названия системы, которая разворачивается из шаблона
        /// </summary>
        /// <param name="templatePath">файл шаблона</param>
        /// <param name="outputFolder">папка из которой идет вызов (в нее будут генерироваться файлы)</param>
        /// <param name="substitutions">
        /// Словарь замен. В окне вводится название класса, которое будет
        /// добавлено в этот словарь как "ClassName" => "input.value"
        /// Можно передать еще ключей с данными для замен в шаблоне при развертывании его в файлы
        /// </param>
        public static void ShowWindow(string templatePath, string outputFolder,
            Dictionary<string, string> substitutions)
        {
            _templatePath = templatePath;
            _outputFolder = outputFolder;
            _substitutions = substitutions;

            var window = GetWindow<VtpGeneratorWindow>(true, "VTP Generator");
            window.minSize = new Vector2(300, 70);
            window.maxSize = new Vector2(500, 70);
        }

        private void OnGUI()
        {
            GUILayout.Label("Генерация из шаблона", EditorStyles.boldLabel);
            _className = EditorGUILayout.TextField("Имя класса", _className);
            GUI.enabled = !string.IsNullOrWhiteSpace(_className);
            if (GUILayout.Button("Сгенерировать"))
            {
                _substitutions["ClassName"] = _className;
                Close();
                VtpTemplateGenerator.Generate(_templatePath, _outputFolder, _substitutions);
            }

            GUI.enabled = true;
        }
    }
}
#endif