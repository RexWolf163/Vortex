using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Serialization;
using Vortex.Core.Extensions.ReactiveValues;
using Vortex.Core.SaveSystem.Abstraction;
using Vortex.Core.SaveSystem.Bus;
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
        /// <summary>
        /// Фильтр списка по метаблоку сводки: значение блока <see cref="key"/> сверяется с регулярным выражением
        /// <see cref="pattern"/> целиком (якоря <c>^…$</c> добавляются сами, регистр учитывается).
        ///
        /// Отсутствующий блок считается пустой строкой — так выражение покрывает и сейвы, записанные до появления
        /// метаданных: <c>Main|</c> — режим Main или блока нет (старый сейв), <c>.*</c> — что угодно, в т.ч. без блока,
        /// <c>.+</c> — только сейвы с блоком. Невалидное выражение — ошибка в лог, фильтр не пропускает ничего.
        /// </summary>
        [Serializable]
        private class MetaFilter
        {
            [SerializeField, Tooltip("Ключ метаблока сводки (preview, kind, gameMode…)")]
            private string key;

            [SerializeField, FormerlySerializedAs("mask")]
            [Tooltip("Регулярное выражение для значения, совпадение всей строки. " +
                                     "Нет блока — пустая строка: «Main|» пропустит и старые сейвы без маркера.")]
            private string pattern;

            /// <summary>Скомпилированное выражение и выражение-источник: пересборка при смене поля в инспекторе</summary>
            private Regex _regex;

            private string _compiledPattern;

            private bool _invalid;

            public MetaFilter()
            {
            }

            public MetaFilter(string key, string pattern)
            {
                this.key = key;
                this.pattern = pattern;
            }

            public string Key => key;

            public bool Match(SaveSummary summary)
            {
                var regex = GetRegex();
                return regex != null && regex.IsMatch(summary.GetMeta(key) ?? string.Empty);
            }

            private Regex GetRegex()
            {
                var source = pattern ?? string.Empty;
                if (source == _compiledPattern)
                    return _invalid ? null : _regex;

                _compiledPattern = source;
                try
                {
                    _regex = new Regex($"^(?:{source})$", RegexOptions.CultureInvariant);
                    _invalid = false;
                }
                catch (ArgumentException e)
                {
                    _regex = null;
                    _invalid = true;
                    Debug.LogError($"[SaveListView] Невалидный фильтр метаданных «{key}»: «{source}» — {e.Message}");
                }

                return _regex;
            }
        }

        [SerializeField] private Pool pool;

        [SerializeField]
        [Tooltip("Фильтры по метаданным сводки: в списке только сейвы, прошедшие ВСЕ фильтры. Пусто — все сейвы.")]
        private MetaFilter[] filters = new MetaFilter[0];

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
            try
            {
                SaveController.OnSaveComplete += Refresh;
                SaveController.OnLoadComplete += Refresh;
                SaveController.OnRemove += Refresh;

                _isInit = true;
                _saveSlots.Clear();
                _index = SaveController.GetIndex();
                _index = _index
                    .Where(s => PassFilters(s.Value))
                    .OrderByDescending(s => s.Value.UnixTimestamp)
                    .ToDictionary(s => s.Key, s => s.Value);

                pool.Clear();

                if (_index == null || _index.Count == 0)
                    return;

                var first = _index.First();
                _focused = new StringData(null);
                _focused.Set(first.Key);
                var f = true;
                foreach (var guid in _index.Keys)
                {
                    await UniTask.Yield(token);
                    if (token.IsCancellationRequested)
                        return;
                    var summary = _index[guid];
                    var slotData = new SaveSlotData(guid, summary);
                    _saveSlots[guid] = slotData;
                    pool.AddItem(slotData, _focused, (Action<string>)SetFocus);
                    if (!f) continue;
                    f = false;
                    storageFocusedSlot.SetData(_saveSlots[first.Key]);
                }
            }
            catch (OperationCanceledException)
            {
                Dispose();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                Dispose();
            }
        }

        /// <summary>
        /// Задать фильтр по ключу метаблока: прежние фильтры с этим ключом заменяются одним новым. Список не
        /// перестраивается — после изменения фильтров вызвать <see cref="Refresh"/>.
        /// </summary>
        /// <param name="key">Ключ метаблока сводки</param>
        /// <param name="pattern">Регулярное выражение для значения (совпадение всей строки; нет блока — пустая строка)</param>
        public void SetFilter(string key, string pattern)
        {
            var list = new List<MetaFilter>(filters.Length + 1);
            foreach (var filter in filters)
                if (filter.Key != key)
                    list.Add(filter);
            list.Add(new MetaFilter(key, pattern));
            filters = list.ToArray();
        }

        /// <summary>
        /// Снять все фильтры. Список не перестраивается — вызвать <see cref="Refresh"/>.
        /// </summary>
        public void ClearFilters() => filters = new MetaFilter[0];

        /// <summary>
        /// Перестроить список сейвов по текущему индексу и фильтрам. Выключенное представление не трогается —
        /// список соберётся при включении.
        /// </summary>
        public void Refresh()
        {
            if (!isActiveAndEnabled)
                return;
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

        /// <summary>
        /// Сейв прошёл все фильтры (AND); без фильтров — проходит любой
        /// </summary>
        private bool PassFilters(SaveSummary summary)
        {
            foreach (var filter in filters)
                if (!filter.Match(summary))
                    return false;
            return true;
        }

        private void SetFocus(string guid)
        {
            _focused.Set(guid);
            storageFocusedSlot.SetData(_saveSlots[guid]);
        }
    }
}