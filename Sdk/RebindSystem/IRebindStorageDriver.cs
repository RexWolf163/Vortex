using Vortex.Core.System.Abstractions;

namespace Vortex.Sdk.RebindSystem
{
    /// <summary>Результат чтения снимка из хранилища.</summary>
    public enum StorageReadStatus
    {
        /// <summary>Данные прочитаны.</summary>
        Ok,

        /// <summary>Снимка нет — игрок ничего не менял, действуют заводские настройки.</summary>
        NoData,

        /// <summary>Хранилище не прочиталось (ввод-вывод, доступ).</summary>
        Error
    }

    /// <summary>
    /// Драйвер хранения снимка переназначений. Выбирается в <c>DriverConfig</c>.
    ///
    /// Драйвер — «глупое» хранилище строки: формат снимка знает только контроллер системы. Так смена
    /// хранилища (PlayerPrefs / файл / облако платформы) не затрагивает правила применения и разбора.
    /// </summary>
    public interface IRebindStorageDriver : ISystemDriver
    {
        /// <summary>Прочитать сохранённый снимок.</summary>
        StorageReadStatus Load(out string data);

        /// <summary>Записать снимок. <c>false</c> — запись не удалась (подробности — в лог драйвера).</summary>
        bool Save(string data);

        /// <summary>
        /// Сохранить копию нечитаемого снимка рядом с основным, с отметкой времени в имени, — до того как
        /// основной будет перезаписан.
        /// </summary>
        bool Backup(string data, string stamp);
    }
}
