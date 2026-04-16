using System;
using Sirenix.OdinInspector;
using UnityEngine;
using Vortex.Core.System.Abstractions;
using Vortex.Unity.Camera.Model;
using Vortex.Unity.EditorTools.Attributes;
using Vortex.Unity.EditorTools.DataModelSystem;
using Vortex.Unity.Extensions.ReactiveValues;

namespace Vortex.Unity.Camera.View
{
    [RequireComponent(typeof(UnityEngine.Camera))]
    public class CameraDataStorage : MonoBehaviour, IDataStorage
    {
        /// <summary>
        /// Пустой ивент. Не вызывается
        /// </summary>
        public event Action OnUpdateLink;

        public event Action<CameraModel> OnUpdateData;

        /// <summary>
        /// Ссылка на камеру
        /// TODO внести управляемые данные в модель и контроллеры 
        /// </summary>
        [SerializeField, AutoLink] private UnityEngine.Camera camera;

        [ShowInInspector, HideInEditorMode, DataModel]
        private CameraModel _data;

        internal CameraModel Data => _data ??= new CameraModel();

        private float _size;

        private void Awake()
        {
            CameraBus.Registration(this);

            _size = -1;
            Data.OnUpdateData += UpdateData;
            Data.CameraRect.SetOwner(this);
        }

        private void OnDestroy()
        {
            CameraBus.Remove(this);
            if (_data == null) return;
            _data.OnUpdateData -= UpdateData;
            _data = null;
        }

        private void UpdateData() => OnUpdateData?.Invoke(Data);

        public T GetData<T>() where T : class
        {
            var type = typeof(T);
            T data;

            if (type == typeof(Vector2Data))
                data = Data.Position as T;
            else
                data = Data as T;

            return data;
        }

        private void Update()
        {
            if (Mathf.Approximately(_size, camera.orthographicSize))
                return;

            _size = camera.orthographicSize;
            var h = camera.orthographicSize * 2;
            var w = h * camera.aspect;
            Data.CameraRect.Set(new Vector2(w, h), this);
        }
    }
}