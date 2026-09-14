using Vortex.Core.SaveSystem.Abstraction;

namespace Vortex.Sdk.Core.GameCore
{
    /// <summary>
    /// Суммарное время в приложении за все запуски — модуль глобального хранилища. Ведёт и фиксирует учёт
    /// времени приложения <see cref="GameController"/>; отпечаток значения попадает в слот через
    /// <see cref="GameTimeData.AppSecondsSnapshot"/>.
    ///
    /// Важно: у класса обязан оставаться публичный конструктор без параметров — хранилище собирает модули
    /// рефлексией и берёт только типы с таким конструктором.
    /// </summary>
    public class AppTimeData : IGlobalData
    {
        /// <summary>Накопленные секунды в приложении.</summary>
        public long AppSeconds { get; internal set; }

        public string GetGlobalKey() => "Vortex.AppTime";
    }
}
