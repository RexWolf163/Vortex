using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
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
        /// Ссылка на индекс сейвов
        /// </summary>
        private IDictionary<string, SaveSummary> _index;

        /// <summary>
        /// токен-ресурс прерывания
        /// </summary>
        private CancellationTokenSource _cts = new();

        /// <summary>
        /// Токен прерывания
        /// </summary>
        private CancellationToken Token => _cts.Token;

        /// <summary>
        /// Кеш созданных контейнеров, чтобы потом удалить
        /// </summary>
        private Dictionary<string, SaveSlotData> _saveSlots = new();

        private void OnEnable()
        {
            TimeController.RemoveCall(this);

            Init(Token).Forget(Debug.LogException);
        }

        private void OnDisable()
        {
            TimeController.Call(Dispose, this);
        }

        /// <summary>
        /// Асинхронная инициализация, для того чтобы растянуть формирование превью из строки
        /// </summary>
        private async UniTask Init(CancellationToken token)
        {
            if (_isInit)
                return;

            SaveController.OnSaveComplete += Refresh;
            SaveController.OnLoadComplete += Refresh;
            SaveController.OnRemove += Refresh;

            _isInit = true;
            _saveSlots.Clear();
            _index = SaveController.GetIndex();
            _index = _index.OrderByDescending(s => s.Value.UnixTimestamp).ToDictionary(s => s.Key, s => s.Value);

            pool.Clear();

            if (_index == null || _index.Count == 0)
                return;

            var first = _index.First();
            _focused = new StringData(null);
            _focused.Set(first.Key);

            foreach (var guid in _index.Keys)
            {
                await UniTask.Yield(token);
                if (token.IsCancellationRequested)
                    return;
                var summary = _index[guid];
                var slotData = new SaveSlotData(guid, summary);
                _saveSlots[guid] = slotData;
                pool.AddItem(slotData, _focused, (Action<string>)SetFocus);
            }

            storageFocusedSlot.SetData(_saveSlots[first.Key]);
        }

        private void Refresh()
        {
            Dispose();
            Init(Token).Forget(Debug.LogException);
        }

        private void Dispose()
        {
            SaveController.OnSaveComplete -= Refresh;
            SaveController.OnLoadComplete -= Refresh;
            SaveController.OnRemove -= Refresh;

            _cts.Cancel();
            _cts.Dispose();
            _cts = new CancellationTokenSource();
            _isInit = false;
            _focused = null;
            storageFocusedSlot.SetData(null);
            foreach (var slot in _saveSlots.Values)
                slot.Dispose();
            pool.Clear();
        }

        private void SetFocus(string guid)
        {
            _focused.Set(guid);
            storageFocusedSlot.SetData(_saveSlots[guid]);
        }
    }
}