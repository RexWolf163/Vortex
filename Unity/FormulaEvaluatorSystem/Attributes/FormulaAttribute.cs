using System;
using UnityEngine;

namespace Vortex.Unity.FormulaEvaluatorSystem.Attributes
{
    [AttributeUsage(AttributeTargets.Field)]
    public class FormulaAttribute : PropertyAttribute
    {
        public string SlotsFieldName { get; }

        public FormulaAttribute(string slotsFieldName)
        {
            SlotsFieldName = slotsFieldName;
        }
    }
}
