using UnityEngine;

namespace Vortex.Core.DatabaseSystem.Model
{
    public abstract partial class Record
    {
        /// <summary>
        /// Иконка элемента
        /// </summary>
        public Sprite Icon { get; protected set; }
    }
}