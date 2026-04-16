using System.Collections.Generic;
using UnityEngine;

namespace Vortex.Unity.Camera.Model
{
    /// <summary>
    /// Класс фокусной группы
    /// </summary>
    public class CameraFocusTarget
    {
        internal readonly List<Transform> focusTargets = new();

        public IReadOnlyList<Transform> FocusTargets => focusTargets;
    }
}