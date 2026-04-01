using System;
using Vortex.Core.DatabaseSystem.Model;
using Vortex.Core.LoggerSystem.Bus;
using Vortex.Core.LoggerSystem.Model;
using Vortex.Core.UIProviderSystem.Bus;
using Vortex.Core.UIProviderSystem.Enums;

namespace Vortex.Core.UIProviderSystem.Model
{
    /// <summary>
    /// Модель данных для интерфейса.
    /// Располагается в шине UIProvider.
    ///
    /// Содержит данные для реализации перетаскивания окна.
    ///
    /// Содержит события открытия/закрытия с вызовом при подписке в активный момент.
    /// </summary>
    public class UserInterfaceData : Record
    {
        /// <summary>
        /// Событие открытия окна
        /// </summary>
        private event Action OnOpenPrv;

        /// <summary>
        /// Событие открытия окна
        /// </summary>
        private event Action OnClosePrv;

        /// <summary>
        /// Событие открытия окна
        /// </summary>
        public event Action OnOpen
        {
            add
            {
                OnOpenPrv += value;
                if (IsOpen)
                    value.Invoke();
            }

            remove => OnOpenPrv -= value;
        }

        /// <summary>
        /// Событие открытия окна
        /// </summary>
        public event Action OnClose
        {
            add
            {
                OnClosePrv += value;
                if (!IsOpen)
                    value.Invoke();
            }

            remove => OnClosePrv -= value;
        }

        /// <summary>
        /// точка якоря контейнера
        /// </summary>
        public (int x, int y) Offset = (0, 0);

        public bool IsOpen { get; internal set; } = false;

        public UserInterfaceCondition[] Conditions { get; protected set; }
        public UserInterfaceTypes UIType { get; set; }

        public void Init()
        {
            foreach (var condition in Conditions)
                condition.Init(this, CheckConditions);
            CheckConditions();
        }

        public void DeInit()
        {
            foreach (var condition in Conditions)
                condition.DeInit();
        }

        /// <summary>
        /// Проверка условия открытия окна.
        /// Idle не изменяет состояния
        /// Между Open и Close - приоритет на Close
        /// </summary>
        private void CheckConditions()
        {
            var state = IsOpen ? ConditionAnswer.Open : ConditionAnswer.Close;
            foreach (var condition in Conditions)
            {
                try
                {
                    var answer = condition.Check();
                    switch (answer)
                    {
                        case ConditionAnswer.Idle:
                            continue;
                        case ConditionAnswer.Open:
                            state = ConditionAnswer.Open;
                            continue;
                    }
                }
                catch (Exception ex)
                {
                    Log.Print(LogLevel.Error, ex.Message, this);
                }

                Close();
                return;
            }

            switch (state)
            {
                case ConditionAnswer.Open:
                    Open();
                    break;
                default:
                    Close();
                    break;
            }
        }

        public void Open()
        {
            if (IsOpen)
                return;
            IsOpen = true;
            OnOpenPrv?.Invoke();
            UIProvider.CallOnOpen();
        }

        public void Close()
        {
            if (!IsOpen)
                return;
            IsOpen = false;
            OnClosePrv?.Invoke();
            UIProvider.CallOnClose();
        }

        public override string GetDataForSave()
        {
            return $"{Offset.x};{Offset.y}";
        }

        public override void LoadFromSaveData(string data)
        {
            var ar = data.Split(';');
            Offset.x = Int32.Parse(ar[0]);
            Offset.y = Int32.Parse(ar[1]);
        }
    }
}