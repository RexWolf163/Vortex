using System;
using System.Collections.Generic;
using Vortex.Core.Extensions.LogicExtensions;
using Vortex.Core.MappedParametersSystem.Base;
using Vortex.Core.System.Abstractions;

namespace Vortex.Core.MappedParametersSystem.Bus
{
    /// <summary>
    /// Система доступа к схемам связанных параметров
    /// </summary>
    public class ParameterMaps : SystemController<ParameterMaps, IDriverMappedParameters>
    {
        private static Dictionary<string, ParametersMap> _parametersMaps = new();

        protected override void OnDriverConnect()
        {
            Driver.SetIndex(_parametersMaps);
        }

        protected override void OnDriverDisconnect()
        {
        }

        /// <summary>
        /// Получить перечень параметров для карты c указанным Guid
        /// В качестве guid используется FullName
        /// ВНИМАНИЕ! Соблюдайте осторожность! Нельзя допускать совпадающих идентификаторов
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns>NULL если преданный тип не приводится к IMappedModel</returns>
        public static GenericParameter[] GetParameters<T>() where T : IMappedModel => GetParameters(typeof(T));

        /// <summary>
        /// Получить перечень параметров для карты c указанным Guid
        /// В качестве guid используется FullName
        /// ВНИМАНИЕ! Соблюдайте осторожность! Нельзя допускать совпадающих идентификаторов
        /// </summary>
        /// <param name="type"></param>
        /// <returns>NULL если преданный тип не приводится к IMappedModel</returns>
        public static GenericParameter[] GetParameters(Type type)
        {
            if (!typeof(IMappedModel).IsAssignableFrom(type))
                return null;
            var name = type.FullName;
            return GetParameters(name);
        }

        /// <summary>
        /// Получить перечень параметров для карты c указанным Guid
        /// В качестве guid используется FullName
        /// ВНИМАНИЕ! Соблюдайте осторожность! Нельзя допускать совпадающих идентификаторов
        /// </summary>
        /// <param name="typeFullName"></param>
        /// <returns>NULL если преданный тип не приводится к IMappedModel</returns>
        public static GenericParameter[] GetParameters(string typeFullName)
        {
            return !typeFullName.IsNullOrWhitespace() && _parametersMaps.TryGetValue(typeFullName, out var result)
                ? result.GetParameters()
                : null;
        }

        /// <summary>
        /// Возвращает модель данных построенную по карте с guid совпадающим с FullName указанного типа
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public static IMappedModel GetModel<T>() => GetModel(typeof(T));

        /// <summary>
        /// Возвращает модель данных построенную по карте с guid совпадающим с FullName указанного типа
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        public static IMappedModel GetModel(Type type)
        {
            if (!typeof(IMappedModel).IsAssignableFrom(type))
                return null;
            var name = type.FullName;

            if (name == null)
                return null;
            _parametersMaps.TryGetValue(name, out var map);
            if (map == null)
                return null;

            if (Activator.CreateInstance(type) is not IMappedModel result)
                return null;
            result.Init(map);
            return result;
        }

        public static void InitMap(IMappedModel model)
        {
            var type = model.GetType();
            var name = type.FullName;

            if (name == null)
                return;
            _parametersMaps.TryGetValue(name, out var map);
            if (map == null)
                return;
            model.Init(map);
        }
    }
}