using System;
using System.Text.RegularExpressions;
using Sirenix.OdinInspector;
using UnityEngine;
using Vortex.Core.DatabaseSystem.Model;
using Vortex.Core.DatabaseSystem.Model.Enums;
using Vortex.Core.Extensions.LogicExtensions;
using Vortex.Unity.EditorTools.DataModelSystem;
using Vortex.Unity.Extensions.Abstractions;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Vortex.Unity.DatabaseSystem.Presets
{
    public abstract class RecordPreset<T> : SoData, IRecordPreset where T : Record, new()
    {
        private const string DefaultName = "DBItem";

        [SerializeField] protected RecordTypes type = RecordTypes.Singleton;

        [SerializeField, DisplayAsString] private string guid = Crypto.GetNewGuid();

        [SerializeField, OnValueChanged("OnNameChanged"), LabelText("Name")]
        private string nameRecord = typeof(T).Name;

        [SerializeField] private string description;
        [PreviewField, SerializeField] private Sprite icon;

#if UNITY_EDITOR
        [ShowInInspector, DataModel, HideIf("@type==RecordTypes.MultiInstance&&!UnityEngine.Application.isPlaying")]
        [TabGroup("Debug"), PropertyOrder(100)]
        private T _data;
#endif

        public RecordTypes RecordType => type;

        /// <summary>
        /// Глобально уникальный идентификатор 
        /// </summary>
        public string GuidPreset => guid;

        /// <summary>
        /// Наименование элемента БД
        /// </summary>
        public string Name => nameRecord;

        /// <summary>
        /// Описание элемента БД
        /// </summary>
        public string Description => description;

        /// <summary>
        /// Иконка элемента
        /// </summary>
        public Sprite Icon => icon;

        /// <summary>
        /// Возвращает модель данных заполненную из хранилища
        /// </summary>
        /// <returns></returns>
        public Record GetData()
        {
            var record = new T();
            record.CopyFrom(this);
#if UNITY_EDITOR
            _data = record;
#endif
            return record;
        }

        public bool CheckRecordType<TU>() where TU : Record => CheckRecordType(typeof(TU));

        public bool CheckRecordType(Type t) => t == typeof(T) || t.IsAssignableFrom(typeof(T));

#if UNITY_EDITOR
        [Button]
        public void ResetGuid() => guid = Crypto.GetNewGuid();

        private void OnNameChanged()
        {
            var number = 0;
            nameRecord = CleanFileName(nameRecord);
            var name = nameRecord;
            if (name == string.Empty) name = DefaultName;

            while (true)
            {
                var assetPath = AssetDatabase.GetAssetPath(GetInstanceID());
                var result =
                    AssetDatabase.RenameAsset(assetPath, $"{name}{(number > 0 ? $" ({number})" : "")}");
                if (result != "")
                {
                    number++;
                    continue;
                }

                AssetDatabase.SaveAssets();
                break;
            }

            if (number > 0 && nameRecord != string.Empty)
                Debug.LogError($"[DbRecord] Name {name} for records already exists!");
        }

        private static string CleanFileName(string fileName)
        {
            // Удаляем все знаки препинания и специальные символы
            var cleanName = Regex.Replace(fileName, @"[^\.\w\s-]", "");
            // Удаляем множественные подчеркивания
            cleanName = Regex.Replace(cleanName, @"_+", "_");

            return cleanName;
        }
#endif
    }
}