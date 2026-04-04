using System;
using Vortex.Core.Extensions.LogicExtensions.Actions;
using Vortex.Core.Extensions.LogicExtensions.SerializationSystem;

namespace Vortex.Core.MappedParametersSystem.Base
{
    /// <summary>
    /// Динамически определяемый при настройке параметр, связанный с другим параметром некой логикой
    /// по принципу однонаправленного списка.
    /// Родитель параметра определяется при помощи управляющего контроллера
    /// Логика определяется управляющим контроллером
    /// </summary>
    [Serializable, POCO]
    public class GenericParameter
    {
        public event Action OnUpdate;

        /// <summary>
        /// Название параметра
        /// </summary>
        public string Name { get; internal set; }

        /// <summary>
        /// Значение параметра
        /// </summary>
        public int Value { get; internal set; }

        public GenericParameter() => Name = "";

        public GenericParameter(string name) => Name = name;

        /// <summary>
        /// Установить новое значение
        /// </summary>
        /// <param name="value"></param>
        public void SetValue(int value)
        {
            if (Value == value)
                return;
            Value = value;
            OnUpdate.Fire();
        }
    }
}