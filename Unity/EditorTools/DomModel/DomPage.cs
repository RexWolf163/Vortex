#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Vortex.Unity.EditorTools.DomModel
{
    /// <summary>
    /// Страница = один SerializedObject (компонент / ScriptableObject).
    /// Содержит все узлы по propertyPath.
    /// </summary>
    internal class DomPage
    {
        public readonly Dictionary<string, DomNode> Nodes = new();

        /// <summary>
        /// Фрейм последнего полного пересчёта высот.
        /// Если текущий frameCount != LastComputedFrame, нужен пересчёт.
        /// </summary>
        public int LastComputedFrame = -1;

        /// <summary>
        /// Страница была обсчитана.
        /// </summary>
        public bool IsCalculated;

        /// <summary>
        /// Страница была обсчитана.
        /// </summary>
        public DateTime CalculatedAtTime;

        /// <summary>
        /// Ширина окна при последнем построении (для инвалидации при resize).
        /// </summary>
        public float ViewWidth;

        /// <summary>
        /// Состояние скроллбара при последнем расчёте ширин.
        /// При смене — ширины пересчитываются.
        /// </summary>
        public bool HasScrollbar;

        /// <summary>
        /// Владелец страницы
        /// </summary>
        public GameObject Owner { get; private set; }

        /// <summary>
        /// Владелец страницы SO ассета
        /// </summary>
        public ScriptableObject SO { get; private set; }

        /// <summary>
        /// SerializedObject'ы компонентов. Живут пока жива страница,
        /// т.к. DomNode.Data.Property ссылается на них.
        /// </summary>
        public readonly List<SerializedObject> OwnedSerializedObjects = new();

        public DomPage(GameObject owner)
        {
            Owner = owner;
            SO = null;
        }

        public DomPage(ScriptableObject so)
        {
            SO = so;
            Owner = null;
        }

        public void Dispose()
        {
            foreach (var so in OwnedSerializedObjects)
                so?.Dispose();
            OwnedSerializedObjects.Clear();
            Nodes.Clear();
        }

        /// <summary>
        /// Очистка структуры нод.
        /// Не трогаем OwnedSerializedObjects если только он не разросся, чтобы не ломать отрисовку отстающих элементов
        /// </summary>
        public void Reset()
        {
            if (OwnedSerializedObjects.Count > 3000)
            {
                for (var i = 0; i < OwnedSerializedObjects.Count - 300; i++)
                    OwnedSerializedObjects[i]?.Dispose();
                OwnedSerializedObjects.RemoveRange(0, OwnedSerializedObjects.Count - 300);
            }

            Nodes.Clear();
        }
    }
}
#endif