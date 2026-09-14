using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Xml.Serialization;
using Vortex.Core.Extensions.LogicExtensions;
using Vortex.Core.Extensions.LogicExtensions.SerializationSystem;
using Vortex.Core.LoggerSystem.Bus;
using Vortex.Core.LoggerSystem.Model;
using Vortex.Core.SaveSystem.Abstraction;
using Vortex.Core.SaveSystem.Model;
using Vortex.Core.SettingsSystem.Bus;

namespace Vortex.Core.SaveSystem.Bus
{
    /// <summary>
    /// Кодек контейнера и работа с драйвером: чтение с восстановлением из копии, запись, резервные копии.
    /// Контейнер — <see cref="GlobalContainer"/> в XML, сжатый <c>Compress</c>, как тело слота. Шифрования нет:
    /// защита данных от правки — забота модуля.
    /// </summary>
    public partial class GlobalSaveController
    {
        private const string PackKey = "global";

        private static readonly XmlSerializer ContainerSerializer = new(typeof(GlobalContainer));

        /// <summary>Резервная копия этого запуска уже сделана.</summary>
        private static bool _copyDone;

        /// <summary>Сколько резервных копий держать (<c>SaveSettings</c>). 0 — копий нет.</summary>
        private static int Backups => Math.Max(0, Settings.Data()?.GlobalSaveBackups ?? 0);

        /// <summary>
        /// Прочитать контейнер. Нет контейнера — пустой набор папок (первый запуск). Нечитаемый — самая свежая
        /// целая копия, если копии включены. <c>false</c> — прочитать нечего.
        /// </summary>
        private static bool TryReadStorage(out Dictionary<string, string> folders, out bool restored)
        {
            restored = false;
            folders = null;
            if (!HasDriver())
            {
                Log.Print(LogLevel.Error, "Драйвер глобального хранилища не подключён", LogTag);
                return false;
            }

            var status = Driver.Read(out var raw);
            if (status == GlobalReadStatus.NoData)
            {
                folders = new Dictionary<string, string>();
                return true;
            }

            if (status == GlobalReadStatus.Ok && TryDecode(raw, out folders))
                return true;

            Log.Print(LogLevel.Warning, "Основной контейнер глобального хранилища нечитаем", LogTag);
            restored = TryRestore(out folders);
            return restored;
        }

        private static bool TryRestore(out Dictionary<string, string> folders)
        {
            folders = null;
            if (Backups <= 0)
                return false;

            // Идентификаторы — UTC-тики фиксированной длины: порядок строк совпадает с порядком времени
            var copies = Driver.GetCopies() ?? Array.Empty<string>();
            Array.Sort(copies, StringComparer.Ordinal);
            for (var i = copies.Length - 1; i >= 0; i--)
            {
                if (!Driver.ReadCopy(copies[i], out var raw) || !TryDecode(raw, out folders))
                    continue;

                Log.Print(LogLevel.Warning, $"Глобальное хранилище восстановлено из резервной копии {copies[i]}", LogTag);
                return true;
            }

            return false;
        }

        private static bool TryDecode(string raw, out Dictionary<string, string> folders)
        {
            folders = null;
            try
            {
                var xml = raw.Decompress(PackKey);
                if (string.IsNullOrEmpty(xml))
                    return false;

                using var reader = new StringReader(xml);
                if (ContainerSerializer.Deserialize(reader) is not GlobalContainer container)
                    return false;

                folders = new Dictionary<string, string>();
                if (container.Modules == null)
                    return true;

                foreach (var module in container.Modules)
                    if (!string.IsNullOrEmpty(module.Id))
                        folders[module.Id] = module.Data;
                return true;
            }
            catch (Exception e)
            {
                Log.Print(LogLevel.Warning, $"Контейнер глобального хранилища не разобран: {e.Message}", LogTag);
                return false;
            }
        }

        /// <summary>Контейнер из текущих модулей. Пишутся только модули индекса.</summary>
        private static string Encode()
        {
            var container = new GlobalContainer();
            foreach (var pair in ByKey)
                container.Modules.Add(new SaveData { Id = pair.Key, Data = pair.Value.SerializeProperties() });

            using var writer = new StringWriter();
            ContainerSerializer.Serialize(writer, container);
            return writer.ToString().Compress(PackKey);
        }

        /// <summary>Записать основной контейнер. Ошибка — лог; данные остаются в памяти, следующая запись повторит.</summary>
        private static void WriteMain()
        {
            if (!HasDriver())
            {
                Log.Print(LogLevel.Error, "Драйвер глобального хранилища не подключён — запись невозможна", LogTag);
                return;
            }

            try
            {
                if (!Driver.Write(Encode()))
                    Log.Print(LogLevel.Error,
                        "Запись глобального хранилища не удалась — данные в памяти, следующая фиксация повторит запись",
                        LogTag);
            }
            catch (Exception e)
            {
                Log.Print(LogLevel.Error, $"Запись глобального хранилища: {e.Message}", LogTag);
            }
        }

        /// <summary>Одна резервная копия за запуск; при переполнении удаляются самые старые.</summary>
        private static void MakeCopy()
        {
            var limit = Backups;
            if (_copyDone || limit <= 0 || !IsInit || !HasDriver())
                return;
            _copyDone = true;

            try
            {
                var id = DateTime.UtcNow.Ticks.ToString("D19", CultureInfo.InvariantCulture);
                if (!Driver.WriteCopy(id, Encode()))
                {
                    Log.Print(LogLevel.Error, "Резервная копия глобального хранилища не записана", LogTag);
                    return;
                }

                var copies = Driver.GetCopies() ?? Array.Empty<string>();
                Array.Sort(copies, StringComparer.Ordinal);
                for (var i = 0; i < copies.Length - limit; i++)
                    Driver.DeleteCopy(copies[i]);
            }
            catch (Exception e)
            {
                Log.Print(LogLevel.Error, $"Резервная копия глобального хранилища: {e.Message}", LogTag);
            }
        }
    }
}
