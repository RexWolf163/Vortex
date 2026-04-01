using UnityEngine;
using UnityEngine.Events;

namespace Vortex.Unity.UI.UIComponents
{
    /// <summary>
    /// Структура данных для UI компонента
    /// </summary>
    public struct UIComponentData
    {
        public string[] texts;
        [HideInInspector] public UnityAction[] actions;
        public Sprite[] sprites;
        public int[] enumValues;
    }
}