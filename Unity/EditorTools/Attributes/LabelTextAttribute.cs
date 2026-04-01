using UnityEngine;

namespace Vortex.Unity.EditorTools.Attributes
{
    public class LabelTextAttribute : PropertyAttribute
    {
        public string TextOrMethod { get; private set; }

        public LabelTextAttribute(string textOrMethod)
        {
            TextOrMethod = textOrMethod;
        }
    }
}
