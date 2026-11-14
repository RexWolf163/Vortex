using System;
using UnityEngine;

namespace Vortex.Sdk.CutsceneSystem.Attributes
{
    /// <summary>
    /// Inspector-атрибут для строкового поля с ключом катсцены из <c>CutsceneCatalog</c>. Drawer рисует popup
    /// со всеми доступными ключами.
    ///
    /// В рантайме поле — обычная <c>string</c>; открытие — через <c>CutsceneController.Open(key)</c>
    /// </summary>
    /// <example>
    /// <code>
    /// [CutsceneKey] [SerializeField] private string cutscene;
    ///
    /// private void Launch() => CutsceneController.Open(cutscene);
    /// </code>
    /// </example>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public class CutsceneKeyAttribute : PropertyAttribute
    {
    }
}
