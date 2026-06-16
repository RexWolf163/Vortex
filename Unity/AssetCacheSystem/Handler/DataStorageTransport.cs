using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.AddressableAssets;
using Vortex.Unity.AssetCacheSystem.Bus;
using Vortex.Unity.UI.Misc;

namespace Vortex.Unity.AssetCacheSystem.Handler
{
    /// <summary>
    /// Этот наследник от DataStorage предназначен для отключения/подключения GameObject массива
    /// и связывание его данных с общим контекстом сцены через передачу содержимого DayaStorage контейнера
    /// </summary>
    public class DataStorageTransport : DataStorage
    {
        [InfoBox(
            "Этот наследник от DataStorage предназначен для отключения/подключения GameObject массива и связывание его данных с общим контекстом сцены через передачу содержимого DayaStorage контейнера",
            InfoMessageType.Warning)]
        [InfoBox("Слой содержит детей!\nЭто не является нормальной конфигурацией!", InfoMessageType.Error, "HasChilds")]
        [SerializeField]
        private AssetReferenceGameObject prefabReference;

        [SerializeField, InfoBox("Опционально. Если не выставлено, работает со своим Transform")]
        private Transform targetForInstance;

        private GameObject _instance;

        private CancellationTokenSource _cts;

        private DataStorage _storage;

        private void Awake()
        {
            if (targetForInstance == null)
                targetForInstance = transform;
        }

        protected override void OnEnable()
        {
            if (targetForInstance == null)
                targetForInstance = transform;
            Load().Forget(Debug.LogException);
        }

        protected override void OnDisable()
        {
            _cts.Cancel();
            _cts.Dispose();
            _cts = null;
            _storage = null;
            Destroy(_instance);
            AssetCache.Release(this);
            base.OnDisable();
        }

        private async UniTask Load()
        {
            try
            {
                _cts = new CancellationTokenSource();
                var go = await AssetCache.Load<GameObject>(this, prefabReference, _cts.Token);
                _instance = Instantiate(go, targetForInstance);
                if (Data.Count == 0)
                    return;
                _storage = _instance.GetComponent<DataStorage>();
                _storage?.SetData(Data.ToArray());
            }
            catch (OperationCanceledException)
            {
                /*Игнорим прерывание*/
            }
        }

#if UNITY_EDITOR
        [HorizontalGroup, Button]
        private void Connect()
        {
            var go = prefabReference.editorAsset;
            _instance = Instantiate(go, targetForInstance ?? transform);
            _instance.GetComponent<DataStorage>();
        }

        [HorizontalGroup, Button]
        private void Disconnect()
        {
            var target = targetForInstance;
            if (targetForInstance == null)
                target = transform;

            var childrensCount = target.childCount;
            for (var i = childrensCount - 1; i >= 0; i--)
            {
                var child = target.GetChild(i);
                DestroyImmediate(child.gameObject);
            }
        }

        private bool HasChilds()
        {
            if (targetForInstance == null)
                return transform.childCount > 0;
            return targetForInstance.childCount > 0;
        }

#endif
    }
}