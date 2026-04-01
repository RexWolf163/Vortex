using System;
using UnityEngine;
using Vortex.Core.MappedParametersSystem.Base;
using Vortex.Core.System.Abstractions;

namespace Vortex.Unity.MappedParametersSystem.Handlers
{
    /// <summary>
    /// Абстракция для хранилища линка на модель данных типа IMappedModel
    /// Наследник должен обеспечить получение данных модели из шины и заполнение _data
    /// </summary>
    public abstract class MappedModelStorage : MonoBehaviour, IDataStorage
    {
        protected IMappedModel _data;

        public abstract event Action OnUpdateLink;

        public T GetData<T>() where T : class
        {
            if (_data == null)
                Init();
            return _data as T;
        }

        protected abstract void Init();
    }
}