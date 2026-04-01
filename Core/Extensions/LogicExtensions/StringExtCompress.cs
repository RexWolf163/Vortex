using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;

namespace Vortex.Core.Extensions.LogicExtensions
{
    /// <summary>
    /// Расширение для упаковки строк и обратной их распаковки
    /// </summary>
    public static class StringExtCompress
    {
        /// <summary>
        /// Сжатие строки
        /// </summary>
        /// <param name="data"></param>
        /// <param name="key"></param>
        /// <returns></returns>
        public static string Compress(this string data, string key = "data")
        {
            using MemoryStream memoryStream = new MemoryStream();
            // Создаем новый архив в потоке памяти
            using (var archive = new ZipArchive(memoryStream, ZipArchiveMode.Create))
            {
                var entry = archive.CreateEntry(key);
                using StreamWriter writer = new StreamWriter(entry.Open());
                writer.Write(data);
            }

            var bytes = memoryStream.ToArray();
            return Convert.ToBase64String(bytes);
        }

        /// <summary>
        /// Восстановление сжатой строки
        /// </summary>
        /// <param name="data"></param>
        /// <param name="key"></param>
        /// <returns></returns>
        public static string Decompress(this string data, string key = "data")
        {
            var bytes = Convert.FromBase64String(data);
            using MemoryStream ms = new MemoryStream(bytes);

            // Открываем архив из массива байтов
            using (var archive = new ZipArchive(ms, ZipArchiveMode.Read))
            {
                if (archive.Entries.Count > 0)
                {
                    var entry = archive.Entries.FirstOrDefault(e => e.FullName.EndsWith(key));

                    // Читаем содержимое файла
                    if (entry != null)
                    {
                        using StreamReader reader = new StreamReader(entry.Open(), Encoding.UTF8);
                        return reader.ReadToEnd(); // Чтение содержимого в строку
                    }

                    return null;
                }
            }

            return null;
        }
    }
}