using System;

namespace Vortex.Core.MappedParametersSystem.Base
{
    /// <summary>
    /// Интерфейс-маркер для классов использующих MappedParameters
    /// </summary>
    public interface IMappedModel
    {
        /// <summary>
        /// Событие изменение модели
        /// </summary>
        public event Action OnUpdate;

        /// <summary>
        /// Выводит перечень наименований параметров
        /// </summary>
        /// <returns></returns>
        public string[] GetParameters();

        /// <summary>
        /// Возвращает значение указанного параметра
        /// </summary>
        /// <param name="parameterName"></param>
        /// <returns></returns>
        public int GetValue(string parameterName);

        /// <summary>
        /// Инициализация параметрами
        /// </summary>
        /// <param name="value"></param>
        void Init(ParametersMap value);

        /// <summary>
        /// Возвращает линки на родителей параметра
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public IParameterLink[] GetParents(string name);

        /// <summary>
        /// Возвращает класс-контейнер с данными 
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public GenericParameter GetParameterAsContainer(string name);
    }
}