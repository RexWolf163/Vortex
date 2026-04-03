using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Vortex.Core.SaveSystem.Abstraction;
using Vortex.Core.SaveSystem.Bus;
using Vortex.Core.System.Abstractions.ReactiveValues;
using Vortex.Sdk.UIs.SaveLoad.Models;
using Vortex.Unity.AppSystem.System.TimeSystem;
using Vortex.Unity.UI.Misc;
using Vortex.Unity.UI.PoolSystem;

namespace Vortex.Sdk.UIs.SaveLoad.Views
{
    /// <summary>
    /// Представление списка сейвов
    ///
    /// Держит линк на DataStorage, куда кладет слот в фокусе 
    /// </summary>
    public class SaveListView : MonoBehaviour
    {
        [SerializeField] private Pool pool;

        /// <summary>
        /// Хранилище данных слота в фокусе
        /// </summary>
        [SerializeField] private DataStorage storageFocusedSlot;

        private bool _isInit;

        /// <summary>
        /// GUID выбранного сейва
        /// </summary>
        private StringData _focused;

        /// <summary>
        /// Данные выбранного сейва
        /// </summary>
        private SaveSlotData _focusedSlotData;

        /// <summary>
        /// Ссылка на индекс сейвов
        /// </summary>
        private Dictionary<string, SaveSummary> _index;

        private void OnEnable()
        {
            TimeController.RemoveCall(this);
            Init().Forget(Debug.LogException);
        }

        private void OnDisable()
        {
            TimeController.Call(Dispose, this);
        }

        /// <summary>
        /// Асинхронная инициализация, для того чтобы растянуть формирование превью из строки
        /// </summary>
        private async UniTask Init()
        {
            if (_isInit)
                return;
            _isInit = true;

            _index = SaveController.GetIndex();

            pool.Clear();

            var first = _index.First();
            _focused.Set(first.Key);

            _focusedSlotData = new SaveSlotData(first.Key, first.Value);
            storageFocusedSlot.SetData(_focusedSlotData);

            foreach (var guid in _index.Keys)
            {
                var summary = _index[guid];
                var slotData = new SaveSlotData(guid, summary);
                pool.AddItem(slotData, _focused, (Action<string>)SetFocus);
                await UniTask.Yield();
            }
        }

        private void Dispose()
        {
            _isInit = false;
            _focused = null;
            pool.Clear();
        }

        private void SetFocus(string guid)
        {
            _focused.Set(guid);
            _focusedSlotData = new SaveSlotData(guid, _index[guid]);
        }
    }
}