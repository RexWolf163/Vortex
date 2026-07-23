#if USING_VORTEX_ITEMS
using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;
using Vortex.Core.DatabaseSystem.Model.Enums;
using Vortex.Sdk.ItemsSystem.Bus;
using Vortex.Sdk.ItemsSystem.Model;
using Vortex.Unity.DatabaseSystem.Presets;
using Vortex.Unity.ExtensibleEnumSystem.Attributes;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Vortex.Sdk.ItemsSystem.Presets
{
    /// <summary>
    /// Пресет предмета — единственное место авторинга. Состав свойств задаётся полиморфно через
    /// <c>[SerializeReference]</c>; классы свойств объявляются на уровне 4, пакет о них не знает.
    ///
    /// Массив свойств переносится в модель как буфер и там же расходуется в словари: имя
    /// <see cref="PresetProperties"/> совпадает с одноимённым свойством модели, поэтому массив
    /// переносит CopyFrom глубокой копией — каждый предмет получает собственные независимые
    /// свойства. Категория переносится строковым ключом, а не разрешённым значением: копирование
    /// идёт через DeepCopy и продублировало бы singleton-инстанс расширяемого перечисления.
    ///
    /// Имена парных свойств пресета и модели не должны различаться только регистром —
    /// сопоставление в CopyFrom регистронезависимо, и такие пары схлопнутся в одну.
    ///
    /// Тип записи закреплён как многоэкземплярный: каждый предмет — самостоятельный экземпляр.
    /// </summary>
    [CreateAssetMenu(fileName = "ItemModel", menuName = "Database/Item")]
    public class ItemPreset : RecordPreset<ItemModel>
    {
        [SerializeReference]
        [InfoBox("$ValidationMessage", InfoMessageType.Error, "$HasValidationError")]
        [LabelText("Properties")]
        private ItemProperty[] properties = Array.Empty<ItemProperty>();

        /// <summary>Состав свойств под перенос в модель. Читается CopyFrom при построении записи.</summary>
        public ItemProperty[] PresetProperties => properties;

        [SerializeField, ExtEnumKey(typeof(ItemCategory))]
        private string categoryKey;

        /// <summary>Ключ категории. Разрешается в инстанс на стороне модели.</summary>
        public string CategoryKey => categoryKey;

#if UNITY_EDITOR

        private void OnValidate() => type = RecordTypes.MultiInstance;

        #region Валидация состава

        private const double ValidationInterval = 1.0;

        private double _validationTime;
        private string _validationMessage;

        private bool HasValidationError => !string.IsNullOrEmpty(ValidationMessage);

        /// <summary>
        /// Сообщение валидации для инспектора. Odin опрашивает условие на каждой перерисовке,
        /// а проверка перебирает интерфейсы всех свойств — поэтому результат кешируется на секунду:
        /// для глаза «живо», для процессора — раз в секунду.
        /// </summary>
        private string ValidationMessage
        {
            get
            {
                var now = EditorApplication.timeSinceStartup;
                if (_validationMessage != null && now - _validationTime < ValidationInterval)
                    return _validationMessage;

                _validationTime = now;
                _validationMessage = Validate();
                return _validationMessage;
            }
        }

        /// <summary>
        /// Проверяет состав на нарушения, которые иначе всплывут только в рантайме: пустые слоты,
        /// повтор класса, занятое назначение и свойство без единого интерфейса назначения —
        /// такое свойство найти невозможно, оно инертно.
        /// </summary>
        private string Validate()
        {
            if (properties == null || properties.Length == 0)
                return string.Empty;

            var errors = new List<string>();
            var classes = new HashSet<Type>();
            var taken = new Dictionary<Type, Type>();

            for (var i = 0; i < properties.Length; i++)
            {
                var property = properties[i];
                if (property == null)
                {
                    errors.Add($"[{i}] пустой слот");
                    continue;
                }

                var propertyType = property.GetType();
                if (!classes.Add(propertyType))
                {
                    // Назначения уже заняты первым вхождением класса — вторая жалоба на ту же
                    // причину только зашумила бы список.
                    errors.Add($"[{i}] {propertyType.Name}: класс уже присутствует в составе");
                    continue;
                }

                var purposes = ItemsBus.GetPurposeInterfaces(propertyType);
                if (purposes.Length == 0)
                {
                    errors.Add($"[{i}] {propertyType.Name}: нет интерфейсов назначения — свойство недостижимо");
                    continue;
                }

                foreach (var purpose in purposes)
                {
                    if (taken.TryGetValue(purpose, out var owner))
                    {
                        errors.Add($"[{i}] {propertyType.Name}: назначение {purpose.Name} уже занято ({owner.Name})");
                        continue;
                    }

                    taken[purpose] = propertyType;
                }
            }

            return errors.Count == 0 ? string.Empty : string.Join("\n", errors);
        }

        #endregion

#endif
    }
}
#endif