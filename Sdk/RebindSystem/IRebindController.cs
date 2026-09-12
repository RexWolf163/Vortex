using System;
using System.Collections.Generic;
using Vortex.Sdk.RebindSystem.Model;

namespace Vortex.Sdk.RebindSystem
{
    /// <summary>
    /// Контракт контроллера системы переназначения клавиш. Публичный API синхронный. Адрес слота —
    /// <c>Карта/Экшен#Группа#N</c>.
    /// </summary>
    public interface IRebindController
    {
        /// <summary>Загрузка завершена: снимок применён, модель собрана.</summary>
        bool IsInitialized { get; }

        /// <summary>Модель состояния системы. <c>null</c> до загрузки.</summary>
        RebindModel Model { get; }

        /// <summary>Назначить клавишу в слот. Внутрикартовый конфликт — отказ со списком.</summary>
        RebindResult Assign(string bindKey, BindingValue value);

        /// <summary>Назначить клавишу, забрав её у команд той же карты и группы, с которыми был конфликт.</summary>
        RebindResult Take(string bindKey, BindingValue value);

        /// <summary>Снять клавишу со слота.</summary>
        RebindResult Clear(string bindKey);

        /// <summary>Поменять значения двух слотов местами.</summary>
        RebindResult Swap(string bindKeyA, string bindKeyB);

        /// <summary>Сбросить команду к заводским клавишам.</summary>
        void ResetCommand(string commandId);

        /// <summary>Сбросить карту к заводским клавишам.</summary>
        void ResetMap(string map);

        /// <summary>Сбросить все изменения до заводского состояния, включая активность групп.</summary>
        void ResetAll();

        /// <summary>Включить или выключить группу. Включение гасит остальные группы её набора.</summary>
        void SetGroupActive(string groupKey, bool active);

        /// <summary>Первая кнопка команды в группе. <c>null</c> — нет.</summary>
        BindSlot GetFirst(string commandId, string groupKey);

        /// <summary>Кнопки команды: в указанной группе или во всех.</summary>
        IReadOnlyList<BindSlot> GetAll(string commandId, string groupKey = null);

        /// <summary>Текст текущего снимка — точка отката. <c>null</c> — система не загружена.</summary>
        string Export();

        /// <summary>Массовый ремап из текста снимка: полностью заменяет пользовательское состояние.</summary>
        RebindResult Import(string json);

        /// <summary>Клапан перехвата — наблюдаемое состояние «ждём нажатия».</summary>
        CaptureValve Capture { get; }

        /// <summary>
        /// Открыть клапан перехвата для слота (открытый — перенацелить). Ответ — причина отказа открыть;
        /// <see cref="RejectReason.None"/> — открыт.
        /// </summary>
        RejectReason SaveSignalForBind(string bindKey);

        /// <summary>Прервать перехват: слот остаётся как был.</summary>
        void CancelSaving();

        /// <summary>
        /// Добавить постоянный фильтр кандидатов перехвата. Кандидат принят, если его пропустили все фильтры;
        /// отклонённый проходит в игру и UI как обычное нажатие.
        /// </summary>
        void AddCaptureFilter(Func<BindingValue, bool> filter);

        void RemoveCaptureFilter(Func<BindingValue, bool> filter);

        /// <summary>Изменения сохраняются. <c>false</c> — только в памяти: нет драйвера или хранилище не прочиталось.</summary>
        bool CanPersist { get; }

        /// <summary>
        /// Состояние отличается от заводского: переопределённый, добавленный или снятый слот либо активность групп не
        /// по умолчанию. Считается по запросу — обход всех слотов.
        /// </summary>
        bool HasChanges();
    }
}
