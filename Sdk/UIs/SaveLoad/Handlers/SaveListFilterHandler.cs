using System;
using UnityEngine;
using Vortex.Sdk.UIs.SaveLoad.Views;

namespace Vortex.Sdk.UIs.SaveLoad.Handlers
{
    /// <summary>
    /// Выставляет фильтры по метаданным на пролинкованный <see cref="SaveListView"/> при включении и перестраивает
    /// список. Фильтр — ключ метаблока и регулярное выражение для его значения (см. <see cref="SaveListView.SetFilter"/>).
    ///
    /// Порядок включения с самим списком не важен: включённый список перестроится по <see cref="SaveListView.Refresh"/>,
    /// ещё выключенный соберётся при своём включении уже с фильтрами.
    /// </summary>
    public class SaveListFilterHandler : MonoBehaviour
    {
        [Serializable]
        private class Filter
        {
            [SerializeField, Tooltip("Ключ метаблока сводки (kind, gameMode…)")]
            public string key;

            [SerializeField, Tooltip("Регулярное выражение для значения, совпадение всей строки. " +
                                     "Нет блока — пустая строка: «Main|» пропустит и старые сейвы без маркера.")]
            public string pattern;
        }

        [SerializeField] private SaveListView saveList;

        [SerializeField, Tooltip("Снять фильтры списка (в т.ч. заданные в его инспекторе) перед установкой своих")]
        private bool clearFilters = true;

        [SerializeField] private Filter[] filters = new Filter[0];

        private void OnEnable()
        {
            if (saveList == null)
            {
                Debug.LogError($"[SaveListFilterHandler] {name}: не назначен SaveListView.", this);
                return;
            }

            if (clearFilters)
                saveList.ClearFilters();
            foreach (var filter in filters)
                saveList.SetFilter(filter.key, filter.pattern);
            saveList.Refresh();
        }
    }
}
