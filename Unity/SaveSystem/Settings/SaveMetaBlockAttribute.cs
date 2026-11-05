using System;

namespace Vortex.Unity.SaveSystem.Presets
{
    /// <summary>
    /// Помечает bool-поле <see cref="SaveSettings"/> как переключатель метаблока сводки сейва с ключом
    /// <see cref="Key"/>. Поле объявляет пакет-владелец метаблока в своём partial <see cref="SaveSettings"/>
    /// (asmref в <c>ru.vortex.unity.settings</c>).
    /// </summary>
    [AttributeUsage(AttributeTargets.Field)]
    public sealed class SaveMetaBlockAttribute : Attribute
    {
        public string Key { get; }

        public SaveMetaBlockAttribute(string key) => Key = key;
    }
}
