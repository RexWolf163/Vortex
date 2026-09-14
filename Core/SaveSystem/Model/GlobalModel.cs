using System;
using System.Collections.Generic;
using Vortex.Core.ComplexModelSystem;
using Vortex.Core.SaveSystem.Abstraction;

namespace Vortex.Core.SaveSystem.Model
{
    /// <summary>
    /// Реестр модулей глобального хранилища: все реализации <see cref="IGlobalData"/>, найденные рефлексией,
    /// по образцу <c>GameModel</c>. В отличие от него сериализуется не одним блоком, а по модулям — этим
    /// занимается <c>GlobalSaveController</c>; модель только держит экземпляры.
    /// </summary>
    public sealed class GlobalModel : ComplexModel<IGlobalData>
    {
        /// <summary>Экземпляры модулей.</summary>
        internal IEnumerable<IGlobalData> Modules => Index.Values;

        /// <summary>Экземпляр модуля по типу. <c>null</c> — модуля нет.</summary>
        internal IGlobalData Find(Type type) => type != null && Index.TryGetValue(type, out var module) ? module : null;

        protected override void BeforeSerialization()
        {
        }

        protected override void BeforeDeserialization()
        {
        }

        protected override void AfterSerialization()
        {
        }

        protected override void AfterDeserialization()
        {
        }
    }
}
