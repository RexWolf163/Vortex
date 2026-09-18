using System;
using UnityEngine;
using Vortex.Core.DatabaseSystem.Model;
using Vortex.Core.DatabaseSystem.Model.Enums;

namespace Vortex.Unity.DatabaseSystem.Attributes
{
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = true)]
    public class DbRecordAttribute : PropertyAttribute
    {
        public Type RecordClass { get; private set; }
        public RecordTypes? RecordType { get; private set; }

        /// <summary>
        /// Опциональная группировка выпадашки по короткому имени типа записи
        /// (<c>Type.Name</c> без namespace). При включении drawer префиксует каждое
        /// имя записи «TypeShortName/» — <c>SearchablePopup</c> строит из этого отдельные
        /// раскрываемые разделы. Полезно, когда <c>RecordClass = null</c> и выпадашка
        /// содержит записи разных типов.
        /// </summary>
        public bool GroupByType { get; set; }


        public DbRecordAttribute(Type @class)
        {
            RecordClass = @class;
            RecordType = null;
        }

        public DbRecordAttribute(RecordTypes recordType)
        {
            RecordClass = typeof(Record);
            RecordType = recordType;
        }

        public DbRecordAttribute(Type @class, RecordTypes recordType)
        {
            RecordClass = @class;
            RecordType = recordType;
        }

        public DbRecordAttribute()
        {
            RecordClass = typeof(Record);
            RecordType = null;
        }
    }
}