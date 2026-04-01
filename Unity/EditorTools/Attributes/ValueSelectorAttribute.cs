using UnityEngine;

namespace Vortex.Unity.EditorTools.Attributes
{
    /// <summary>
    /// Заменяет стандартное поле на SearchablePopup с вариантами из указанного метода.
    ///
    /// Поддерживаемые типы возврата метода:
    ///   • string[] / List<string>     — ключ = значение, записывается в string-поле
    ///   • Dictionary<string, TValue>       — ключи отображаются в popup,
    ///                                          при выборе TValue записывается в поле
    ///
    /// Метод может быть instance или static, public или private, без параметров.
    ///
    /// Тип поля должен соответствовать TValue словаря (или string для массивов).
    /// Для несериализуемых типов (например System.Type) используйте string-поле
    /// и Dictionary<string, string> (FullName → FullName).
    ///
    /// Использование:
    ///   // Простой список строк
    ///   [SerializeField, ValueSelector("GetNames")]
    ///   private string selectedName;
    ///
    ///   // Словарь с кастомным placeholder
    ///   [SerializeField, ValueSelector("GetTypes", Placeholder = "— Pick Type —")]
    ///   private string selectedTypeName;
    ///
    ///   private string[] GetNames() => new[] { "Alpha", "Beta", "Gamma" };
    ///
    ///   private Dictionary<string, string> GetTypes() =>
    ///       AppDomain.CurrentDomain.GetAssemblies()
    ///           .SelectMany(a => a.GetTypes())
    ///           .Where(t => typeof(MonoBehaviour).IsAssignableFrom(t))
    ///           .ToDictionary(t => t.FullName, t => t.AssemblyQualifiedName);
    /// </summary>
    public class ValueSelectorAttribute : PropertyAttribute
    {
        public string MethodName { get; private set; }
        public string Placeholder { get; set; }

        public ValueSelectorAttribute(string methodName)
        {
            MethodName = methodName;
        }
    }
}