using System;
using System.Collections.Generic;
using UnityEngine;
using Vortex.Core.Extensions.ReactiveValues;
using Vortex.Unity.Extensions.ReactiveValues;

namespace AppScripts.Camera.Model
{
    public class CameraModel : IReactiveData
    {
        public event Action OnUpdateData;

        /// <summary>
        /// Текущая позиция камеры
        /// </summary>
        public Vector2Data Position { get; } = new(Vector2.zero);

        /// <summary>
        /// Целевая позиция камеры
        /// </summary>
        public Vector2Data Target { get; } = new(Vector2.zero);

        internal readonly List<CameraFocusTarget> focusedObjects = new();

        /// <summary>
        /// Объекты в фокусе для центровки камеры по принципу "приоритета последнего"
        /// </summary>
        public IReadOnlyList<CameraFocusTarget> FocusedObjects => focusedObjects;

        /// <summary>
        /// Границы перемещения камеры
        /// </summary>
        public Rect Borders { get; internal set; }

        /// <summary>
        /// Использовать или нет границы перемещения камеры
        /// </summary>
        public bool IsBordered { get; internal set; }

        internal void CallOnUpdate() => OnUpdateData?.Invoke();
    }
}