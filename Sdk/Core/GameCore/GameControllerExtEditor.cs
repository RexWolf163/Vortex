#if UNITY_EDITOR

using System;
using System.Collections.Generic;

namespace Vortex.Sdk.Core.GameCore
{
    /// <summary>
    /// Editor-доступ к модели игры для инструментов (окно <c>Tools/Vortex/SaveData/Game Index</c>): перечисление модулей
    /// <see cref="GameModel.IGameData"/> и сброс к значениям по умолчанию. Рабочий код берёт данные через
    /// <see cref="GameController.Get{T}"/>.
    ///
    /// Вне Play Mode обращение к модели создаёт временный экземпляр — правки не переживают выход из окна.
    /// </summary>
    public partial class GameController
    {
        /// <summary>Модули модели: тип → экземпляр.</summary>
        public static IReadOnlyDictionary<Type, GameModel.IGameData> EditorModules() => GetData().GetEditorIndex();

        /// <summary>Пересоздать модуль со значениями по умолчанию и разослать событие обновления данных.</summary>
        public static void EditorResetModule(Type type)
        {
            GetData().EditorResetModule(type);
            CallUpdateEvent();
        }

        /// <summary>Пересобрать модель целиком (как при новой игре) и разослать событие обновления данных.</summary>
        public static void EditorResetAll()
        {
            GetData().Init();
            CallUpdateEvent();
        }

        /// <summary>Сообщить подписчикам, что данные изменены — после правки свойства модуля из инструмента.</summary>
        public static void EditorCommit() => CallUpdateEvent();
    }
}
#endif
