using System;
using System.Collections.Generic;
using System.Linq;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Vortex.Unity.UI.UIBuilder.Base
{
    /// <summary>
    /// Общие настройки модуля UIBuilder: где искать примитивы и что подставлять в окно создания по умолчанию.
    /// Экземпляр на каждый модуль хранится в <see cref="UIBuilderSettings"/>. Наследник модуля задаёт своё имя слоя
    /// по умолчанию через конструктор и может добавлять собственные поля.
    /// </summary>
    [Serializable]
    public abstract class UIBuilderModuleSettings
    {
        [SerializeField, FolderPath(RequireExistingPath = true)]
        [Tooltip("Папка примитивов. Сканируется вместе с подпапками. Пусто — модуль не настроен.")]
        private string folder;

        [SerializeField, ValueDropdown(nameof(GetPrefabs))]
        [Tooltip("Примитив, выбранный при открытии окна. Список — из папки примитивов; вне папки — игнорируется.")]
        private GameObject defaultPrefab;

        [SerializeField, Tooltip("Имя создаваемого слоя по умолчанию.")]
        private string defaultLayerName;

        [SerializeField, Tooltip("Размер создаваемого слоя (RectTransform.sizeDelta).")]
        private Vector2 defaultSize = new(240, 80);

        protected UIBuilderModuleSettings(string defaultLayerName) => this.defaultLayerName = defaultLayerName;

        public string Folder => folder;

        public GameObject DefaultPrefab => defaultPrefab;

        public string DefaultLayerName => defaultLayerName;

        public Vector2 DefaultSize => defaultSize;

        private IEnumerable<ValueDropdownItem<GameObject>> GetPrefabs()
        {
            UIBuilderCatalog.Load(folder, out var labels, out var prefabs);
            return labels.Select((label, i) => new ValueDropdownItem<GameObject>(label, prefabs[i]));
        }
    }
}
