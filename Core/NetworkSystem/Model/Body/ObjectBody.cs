namespace Vortex.Core.NetworkSystem.Model.Body
{
    /// <summary>Тело-объект: объект, который провайдер тела сериализует при запуске запроса.</summary>
    public sealed class ObjectBody : QueryBody
    {
        /// <summary>Объект для сериализации.</summary>
        public object Value { get; }

        /// <summary>Создать тело-объект.</summary>
        /// <param name="value">Объект для сериализации.</param>
        public ObjectBody(object value)
        {
            Value = value;
        }
    }
}
