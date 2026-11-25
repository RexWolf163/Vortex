using System;
using UnityEngine;
using Vortex.Core.MaskSystem;

namespace Vortex.Unity.MaskSystem
{
    /// <summary>
    /// Маска «шаблон со *» для инспектора. Семантика сопоставления — без изменений от <see cref="WildcardMask"/>.
    /// В редакторе (в том числе в Play Mode) правка параметров перевалидирует маску на лету; в сборке изменение
    /// после валидации — ошибка программиста. Выбирается полем с <see cref="MaskSelectorAttribute"/>.
    /// </summary>
    [Serializable]
    public class UnityWildcardMask : WildcardMask
    {
        protected override bool RebuildOnChange => Application.isEditor;
    }
}
