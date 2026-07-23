#if USING_VORTEX_ITEMS
using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;
using Vortex.Core.DatabaseSystem.Model.Enums;
using Vortex.Core.LoggerSystem.Bus;
using Vortex.Core.LoggerSystem.Model;
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
    /// Авторский массив сворачивается здесь же в словарь по классу свойства и в этой форме
    /// переносится в модель: имя <see cref="Properties"/> совпадает с одноимённым свойством
    /// модели, поэтому состав переносит CopyFrom глубокой копией — каждый предмет получает
    /// собственные независимые свойства. Категория переносится строковым ключом, а не разрешённым
    /// значением: копирование идёт через DeepCopy и продублировало бы singleton-инстанс
    /// расширяемого перечисления.
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

        private Dictionary<Type, ItemProperty> _composition;

        /// <summary>
        /// Состав свойств по конкретному классу — форма переноса в модель. Читается CopyFrom при
        /// построении записи: имя совпадает с одноимённым свойством модели, ключи словаря копируются
        /// как есть, значения — глубокой копией, поэтому экземпляры пресета наружу не утекают.
        ///
        /// Группировка выполняется здесь, а не на построении предмета: она зависит только от
        /// настройки, поэтому считается один раз на пресет, а не на каждый из десятков тысяч
        /// экземпляров. Диагностика ошибок настройки по той же причине пишется в лог однократно.
        /// </summary>
        public Dictionary<Type, ItemProperty> Properties => _composition ??= BuildComposition();

        /// <summary>
        /// Сворачивает авторский массив в словарь по классу. Пустые слоты и повтор класса —
        /// ошибки настройки: слот пропускается, первое вхождение класса выигрывает.
        /// </summary>
        private Dictionary<Type, ItemProperty> BuildComposition()
        {
            var composition = new Dictionary<Type, ItemProperty>();
            if (properties == null)
                return composition;

            foreach (var property in properties)
            {
                if (property == null)
                {
                    Log.Print(LogLevel.Error, $"[Items] Empty property slot in preset «{Name}»", this);
                    continue;
                }

                var propertyType = property.GetType();
                if (composition.ContainsKey(propertyType))
                {
                    Log.Print(LogLevel.Error,
                        $"[Items] Duplicated property class «{propertyType.Name}» in preset «{Name}»", this);
                    continue;
                }

                composition[propertyType] = property;
            }

            return composition;
        }

        [SerializeField, ExtEnumKey(typeof(ItemCategory))]
        private string categoryKey;

        /// <summary>Ключ категории. Разрешается в инстанс на стороне модели.</summary>
        public string CategoryKey => categoryKey;

#if UNITY_EDITOR

        /// <summary>Правка состава в инспекторе сбрасывает свёртку — иначе она осталась бы вчерашней.</summary>
        private void OnValidate()
        {
            type = RecordTypes.MultiInstance;
            _composition = null;
        }

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
                    errors.Add($"[{i}] {propertyType.Name}: класс уже присутствует в составе");

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