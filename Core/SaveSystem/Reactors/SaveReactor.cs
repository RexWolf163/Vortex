using System;

namespace Vortex.Core.SaveSystem.Reactors
{
    /// <summary>
    /// Блок коррекции устаревшего сейва. Работает по сырой строке тела сейва: распакованный XML
    /// <c>SavePreset</c> до разбора, поэтому реактор чинит и то, что новой сборкой уже не читается —
    /// переименованные поля, снятые типы, изменившийся смысл значения.
    ///
    /// Окно применимости задаётся версиями сборки, которой записан сейв (<c>SaveSummary.Version</c>):
    /// реактор применяется, когда версия сейва не старше <see cref="minVersion"/> и не новее
    /// <see cref="maxVersion"/>. Пустая граница — «без ограничения с этой стороны», пустая версия сейва
    /// считается самой старой. Сейв новее последнего реактора не попадает ни в одно окно и читается как есть.
    ///
    /// Реактор не знает о других реакторах и о структуре чужих модулей: нужный кусок он находит в строке сам.
    /// Порядок применения — порядок списка в <c>SaveSettings</c>. Сейв на диске не переписывается: коррекция
    /// живёт только в загруженных данных.
    /// </summary>
    [Serializable]
    public abstract class SaveReactor
    {
        /// <summary>Нижняя граница окна: версия сейва, начиная с которой реактор применяется. Пусто — без нижней границы.</summary>
        public string minVersion;

        /// <summary>Верхняя граница окна: последняя версия сейва, к которой реактор применяется. Пусто — без верхней границы.</summary>
        public string maxVersion;

        /// <summary>Подходит ли реактор сейву версии <paramref name="saveVersion"/>.</summary>
        public bool IsApplicable(string saveVersion)
        {
            if (!string.IsNullOrWhiteSpace(minVersion) && !SaveVersion.IsAtLeast(saveVersion, minVersion))
                return false;

            return string.IsNullOrWhiteSpace(maxVersion) || SaveVersion.IsAtMost(saveVersion, maxVersion);
        }

        /// <summary>
        /// Коррекция тела сейва. На входе — распакованный XML, на выходе — он же после правок.
        /// Возврат исходной строки означает «ничего не менял».
        /// </summary>
        public abstract string TransformRaw(string raw);
    }
}
