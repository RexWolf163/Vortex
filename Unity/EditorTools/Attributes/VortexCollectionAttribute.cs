using System;
using UnityEngine;

namespace Vortex.Unity.EditorTools.Attributes
{
    /// <summary>
    /// Маркер для коллекций (List/array): IMultiDrawerAttribute атрибуты
    /// обрабатываются на уровне списка, а не каскадируются на элементы.
    ///
    /// Без параметров — все IMultiDrawerAttribute атрибуты применяются к списку.
    /// С параметрами — только указанные типы атрибутов применяются к списку,
    /// остальные каскадируются на элементы как обычно.
    /// </summary>
    /// <example>
    /// // Все атрибуты на уровне списка:
    /// [VortexCollection]
    /// [InfoBox("Описание")]
    /// [OnValueChanged("OnChanged")]
    /// public List&lt;string&gt; items;
    ///
    /// // Только InfoBox на уровне списка, LabelText каскадируется:
    /// [VortexCollection(typeof(InfoBoxAttribute))]
    /// [InfoBox("Описание")]
    /// [LabelText("$GetLabel")]
    /// public List&lt;MyItem&gt; items;
    /// </example>
    [AttributeUsage(AttributeTargets.Field)]
    public class VortexCollectionAttribute : PropertyAttribute
    {
        /// <summary>
        /// Типы атрибутов для обработки на уровне списка.
        /// null — все IMultiDrawerAttribute атрибуты.
        /// </summary>
        public Type[] ListLevelAttributes { get; }

        public VortexCollectionAttribute(params Type[] listLevelAttributes)
        {
            ListLevelAttributes = listLevelAttributes is { Length: > 0 }
                ? listLevelAttributes
                : null;
        }
    }
}
