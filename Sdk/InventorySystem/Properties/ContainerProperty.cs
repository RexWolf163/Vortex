#if USING_VORTEX_ITEMS
using System;
using Sirenix.OdinInspector;
using UnityEngine;
using Vortex.Sdk.InventorySystem.Model;
using Vortex.Sdk.ItemsSystem.Model;

namespace Vortex.Sdk.InventorySystem.Properties
{
    /// <summary>
    /// Свойство-контейнер: предмет несёт собственный инвентарь. Инвентарь настраивается прямо здесь —
    /// его верификаторы и стартовое наполнение приходят из настройки предмета, состав сохраняется.
    /// Каждый экземпляр предмета получает собственный независимый инвентарь через глубокое копирование.
    /// </summary>
    [Serializable]
    public class ContainerProperty : ItemProperty, IContainerProperty
    {
        [InfoBox("Свойство-контейнер: предмет несёт собственный инвентарь")] [SerializeField]
        private Inventory inventory = new();

        /// <summary>
        /// Вложенный инвентарь. Публичный геттер с приватным сеттером: геттер нужен сериализатору,
        /// чтобы сохранить состав, сеттер — глубокому копированию из настройки.
        /// </summary>
        public Inventory Inventory
        {
            get => inventory;
            private set => inventory = value;
        }
    }
}
#endif