using System.Collections.Generic;
using System.Linq;
using Vortex.Core.Extensions.LogicExtensions;

namespace Vortex.Core.MappedParametersSystem.Base
{
    /// <summary>
    /// Карта параметров
    /// Хранит список динамических параметров с их связями
    /// (Не хранит их значения! Это карта, а не модель)
    /// </summary>
    public class ParametersMap
    {
        public string Guid { get; protected set; }
        public IParameterMap[] Parameters { get; protected set; }

        private Dictionary<string, IParameterMap> _index;

        public ParametersMap(string guid, IParameterMap[] parameters)
        {
            Guid = guid;
            Parameters = parameters;
            _index = new();
            foreach (var parameter in parameters)
                _index.AddNew(parameter.Name, parameter);
        }

        /// <summary>
        /// Возвращает схему связей указанного параметра
        /// </summary>
        /// <param name="parameterName"></param>
        /// <returns></returns>
        public IParameterMap GetParameterMap(string parameterName) => _index.GetValueOrDefault(parameterName);

        /// <summary>
        /// Возвращает массив параметров готовый для дальнейшего использования
        /// Внимание! Задачи сохранения данных и выдачи GUID лежат на управляющем контроллере
        /// </summary>
        /// <returns></returns>
        public GenericParameter[] GetParameters() => Parameters.Select(p => new GenericParameter(p.Name)).ToArray();
    }
}