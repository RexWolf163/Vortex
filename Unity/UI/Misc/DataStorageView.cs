using Sirenix.OdinInspector;
using UnityEngine;
using Vortex.Core.Extensions.ReactiveValues;
using Vortex.Core.System.Abstractions;
using Vortex.Unity.EditorTools.Attributes;
using Vortex.Unity.EditorTools.DataModelSystem;

namespace Vortex.Unity.UI.Misc
{
    /// <summary>
    /// Базовый абстрактный класс для работы с IDataStorage
    /// </summary>
    public abstract class DataStorageView<T> : MonoBehaviour where T : class, IReactiveData
    {
        #region IDataStorage

        /// <summary>
        /// Ссылка на класс-хранилище нужной модели данных
        /// </summary>
        [SerializeField, ClassFilter(typeof(IDataStorage)), AutoLink]
        private MonoBehaviour source;

        private IDataStorage _storage;
        private IDataStorage Storage => _storage ??= source as IDataStorage;

        /// <summary>
        /// Модель данных из хранилища
        /// </summary>
        [DataModel, ShowInInspector, HideInEditorMode]
        protected T Data;

        #endregion

        protected virtual void OnEnable()
        {
            Storage.OnUpdateLink += UpdateLink;
            Init();
        }

        protected virtual void OnDisable()
        {
            DeInit();
            Storage.OnUpdateLink -= UpdateLink;
        }

        protected virtual void Init()
        {
            Data = Storage.GetData<T>();
            if (Data == null)
                return;
            Data.OnUpdateData += OnDataUpdated;
        }

        protected virtual void DeInit()
        {
            if (Data != null)
                Data.OnUpdateData -= OnDataUpdated;
            Data = null;
        }

        protected virtual void UpdateLink()
        {
            DeInit();
            Init();
        }

        /// <summary>
        /// Обработка изменений модели данных
        /// </summary>
        protected abstract void OnDataUpdated();
    }
}