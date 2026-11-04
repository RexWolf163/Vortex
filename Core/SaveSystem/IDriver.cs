using System.Collections.Generic;
using Vortex.Core.SaveSystem.Abstraction;
using Vortex.Core.System.Abstractions;

namespace Vortex.Core.SaveSystem
{
    public interface IDriver : ISystemDriver
    {
        /// <summary>
        /// Сохранить строки в сейв 
        /// </summary>
        /// <param name="name">Название для сейва</param>
        /// <param name="guid"></param>
        /// <returns></returns>
        public void Save(string name, string guid);

        /// <summary>
        /// Загрузить сейв
        /// </summary>
        /// <param name="guid"></param>
        /// <returns></returns>
        public void Load(string guid);

        /// <summary>
        /// Удалить сейв по ID
        /// </summary>
        /// <param name="guid"></param>
        public void Remove(string guid);

        /// <summary>
        /// Передать линк на индекс
        /// </summary>
        public void SetIndexLink(Dictionary<string, Dictionary<string, string>> index);

        /// <summary>
        /// Возвращает все существующие сейвы
        /// </summary>
        /// <returns></returns>
        public Dictionary<string, SaveSummary> GetIndex();

        /// <summary>
        /// Возвращает номер-инкремент последнего сейва
        /// Индексация идет непрерывно
        /// </summary>
        /// <returns></returns>
        public int GetNumberLastSave();

        /// <summary>
        /// Начало цикла сохранения: драйвер заводит новую сводку с пустыми метаданными. Вызывается
        /// <c>SaveController</c> перед событием начала сохранения — метаданные прошлого цикла (в т.ч. неудачного)
        /// в новый не переносятся. Новая сводка, а не очистка старой: копия прошлой сводки в индексе сейвов
        /// ссылается на тот же список метаданных.
        /// Необязательно: драйвер без поддержки метаданных пишет сейвы как раньше.
        /// </summary>
        public void BeginSummary()
        {
        }

        /// <summary>
        /// Записать метаблок в сводку текущего цикла сохранения (<see cref="SaveSummary.Meta"/>). Действует от
        /// <see cref="BeginSummary"/> до <see cref="Save"/>; вне окна запись уйдёт в черновик и будет сброшена
        /// следующим циклом. Повторный ключ перезаписывает значение.
        /// Необязательно: драйвер без поддержки метаданных вызов игнорирует.
        /// </summary>
        /// <param name="key">Ключ метаблока</param>
        /// <param name="metadata">Значение</param>
        public void AddMeta(string key, string metadata)
        {
        }
    }
}