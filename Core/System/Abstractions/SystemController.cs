using System;
using System.Collections.Generic;

namespace Vortex.Core.System.Abstractions
{
    /// <summary>
    /// Абстрактная основа для системного контроллера, работающего по принципу подключения драйвера
    /// соответствующего текущему движку. 
    /// </summary>
    /// <typeparam name="T">Сам системный контроллер (рефлекс-линк)</typeparam>
    /// <typeparam name="TD">Драйвер</typeparam>
    public abstract class SystemController<T, TD> : Singleton<T>, ISystemController
        where T : SystemController<T, TD>, new()
        where TD : ISystemDriver
    {
        public static bool IsInit { get; protected set; }

        /// <summary>
        /// очередь ожидающих инициализации системы
        /// </summary>
        private static readonly List<Action> InitQueue = new();

        /// <summary>
        /// Кешированное значение белого списка драйверов 
        /// </summary>
        private static Dictionary<string, string> whiteList;

        public static event Action OnInit
        {
            add
            {
                if (IsInit)
                    value?.Invoke();
                else
                    InitQueue.Add(value);
            }
            remove => InitQueue.Remove(value);
        }

        protected static TD Driver;

        /// <summary>
        /// Установить реализацию
        /// </summary>
        /// <param name="driver"></param>
        /// <returns>TRUE - если слот драйвера был пустым, FALSE - если слот был полным. В этом случае
        /// запускаются процессы отключения старого драйвера и инициализации нового</returns>
        public static bool SetDriver(TD driver)
        {
            if (driver == null)
                return false;

            var systemTypeName = Instance.GetType().AssemblyQualifiedName ?? string.Empty;
            try
            {
                //Чтобы избежать ошибок компиляции, первый раз список вынимаем через рефлексию
                if (whiteList == null)
                {
                    var type = Type.GetType("Vortex.Core.System.DriversGenericList");
                    var property = type.GetProperty("WhiteList");
                    whiteList = (Dictionary<string, string>)property.GetValue(null);
                }
            }
            catch (Exception)
            {
                return false;
            }

            if (!whiteList.TryGetValue(systemTypeName, out var driverType)
                || driverType != driver.GetType().AssemblyQualifiedName)
                return false;

            if (Driver != null && !Driver.Equals(driver))
            {
                Driver.OnInit -= CallOnInit;
                Instance.OnDriverDisconnect();
                Driver.Destroy();
                Driver = driver;
                Instance.OnDriverConnect();
                Driver.OnInit += CallOnInit;
                Driver.Init();
                return false;
            }

            Driver = driver;
            Instance.OnDriverConnect();
            Driver.OnInit += CallOnInit;
            Driver.Init();
            return true;
        }

        /// <summary>
        /// Проверка наличия драйвера
        /// </summary>
        /// <returns></returns>
        public static bool HasDriver() => Driver != null;

        /// <summary>
        /// Обработка подключения нового драйвера
        /// </summary>
        protected abstract void OnDriverConnect();

        /// <summary>
        /// Обработка отключения нового драйвера
        /// </summary>
        protected abstract void OnDriverDisconnect();

        /// <summary>
        /// Вызов всех "ожиданцев" после инициализации драйвера
        /// </summary>
        protected static void CallOnInit()
        {
            IsInit = true;
            Driver.OnInit -= CallOnInit;
            var queue = InitQueue.ToArray();
            foreach (var action in queue)
                action?.Invoke();

            InitQueue?.Clear();
        }

        /// <summary>
        /// Возвращает интерфейс драйвера для текущей системы
        /// </summary>
        /// <returns></returns>
        public static Type GetDriverType() => typeof(TD);
    }
}