using System;
using Vortex.Core.MaskSystem;
using Vortex.Unity.EditorTools.Attributes;

namespace Vortex.Unity.MaskSystem
{
    /// <summary>
    /// Прокладка маски Unity-слоя: название вида для заголовка в инспекторе и текст правил вида для подсказки.
    /// </summary>
    [Serializable, ClassLabel("$GetLabel")]
    public abstract partial class UnityMask : Mask
    {
        /// <summary>Название вида для заголовка в инспекторе. По умолчанию — имя типа.</summary>
        protected virtual string GetLabel() => GetType().Name;

        /// <summary>Текст правил вида для подсказки.</summary>
        public abstract string RulesHint { get; }
    }
}
