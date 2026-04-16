using System;
using AppScripts.Camera.Model;
using Sirenix.OdinInspector;
using UnityEngine;
using Vortex.Core.System.Abstractions;
using Vortex.Unity.EditorTools.Attributes;
using Vortex.Unity.EditorTools.DataModelSystem;
using Vortex.Unity.Extensions.ReactiveValues;

namespace AppScripts.Camera.View
{
    [RequireComponent(typeof(UnityEngine.Camera))]
    public class CameraDataStorage : MonoBehaviour, IDataStorage
    {
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

        private Vector3 _lastPosition;

        private void Awake()
        {
            CameraBus.Registration(this);

            Data.OnUpdateData += UpdateData;
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

            if (type == typeof(Vector3))
                data = Data.Position.Value as T;
            else if (type == typeof(Vector2))
                data = Data.Position.Value as T;
            else if (type == typeof(Vector2Data))
                data = Data.Position as T;
            else
                data = Data as T;

            return data;
        }

        /*
        private void UpdatePosition()
        {
            _lastPosition = transform.position;
            Data.Position.Set(transform.position);
            Data.CallOnUpdate();
        }

        private void Update()
        {
            if (transform.position == _lastPosition)
                return;
            UpdatePosition();
        }
    */
    }
}