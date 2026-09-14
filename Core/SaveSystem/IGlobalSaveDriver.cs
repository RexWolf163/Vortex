using System;
using Vortex.Core.System.Abstractions;

namespace Vortex.Core.SaveSystem
{
    /// <summary>Результат чтения основного контейнера глобального хранилища.</summary>
    public enum GlobalReadStatus
    {
        /// <summary>Данные прочитаны.</summary>
        Ok,

        /// <summary>Контейнера нет — первый запуск.</summary>
        NoData,

        /// <summary>Хранилище не прочиталось (ввод-вывод, доступ).</summary>
        Error
    }

    /// <summary>
    /// Драйвер глобального хранилища. Выбирается в <c>DriverConfig</c>.
    ///
    /// Драйвер — «глупое» хранилище строк: формат контейнера, выбор целой копии и ротацию копий знает только
    /// <c>GlobalSaveController</c>. Драйверы различаются лишь местом хранения.
    ///
    /// Готовность системы объявляет контроллер по завершении чтения, поэтому драйвер событие
    /// <see cref="ISystemDriver.OnInit"/> не поднимает.
    /// </summary>
    public interface IGlobalSaveDriver : ISystemDriver
    {
        /// <summary>Прочитать основной контейнер.</summary>
        GlobalReadStatus Read(out string data);

        /// <summary>
        /// Записать основной контейнер. Запись атомарная: оборванная запись оставляет прежний контейнер целым.
        /// <c>false</c> — запись не удалась (подробности — в лог драйвера).
        /// </summary>
        bool Write(string data);

        /// <summary>Записать резервную копию с идентификатором <paramref name="id"/>.</summary>
        bool WriteCopy(string id, string data);

        /// <summary>Идентификаторы существующих копий, в любом порядке.</summary>
        string[] GetCopies();

        /// <summary>Прочитать копию. <c>false</c> — копии нет или она не прочиталась.</summary>
        bool ReadCopy(string id, out string data);

        /// <summary>Удалить копию.</summary>
        void DeleteCopy(string id);

        /// <summary>
        /// Отложить вызов до конца кадра. Повторный вызов в том же кадре заменяет предыдущий — так фиксации
        /// кадра схлопываются в одну запись. Конец кадра — понятие движка, поэтому это забота драйвера.
        /// </summary>
        void ScheduleFlush(Action flush);
    }
}
