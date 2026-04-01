using System;
using System.Collections.Generic;
using Vortex.Core.LoggerSystem.Bus;
using Vortex.Core.LoggerSystem.Model;

namespace Vortex.Core.Extensions.LogicExtensions.Actions
{
    /// <summary>
    /// Расширение класса Action для запуска без проверки существования подписок
    /// (Синтаксический сахар)
    /// </summary>
    public static class ActionExt
    {
        /// <summary>
        /// Подписка с проверкой от дубликата аналогичной подписки 
        /// </summary>
        /// <param name="action"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public static Action AddSafe(this Action action, Action value)
        {
            if (action == null || Array.IndexOf(action.GetInvocationList(), value) < 0)
                action += value;
            else
                Log.Print(LogLevel.Warning, "Action already added.",
                    $"{(value.Target?.GetType().Name ?? "static")}:{value.Method.Name}");

            return action;
        }

        public static void Fire(this Action action) => action?.Invoke();

        public static void Fire<T1>(this Action<T1> action, T1 t1) => action?.Invoke(t1);

        public static void Fire<T1, T2>(this Action<T1, T2> action, T1 t1, T2 t2) =>
            action?.Invoke(t1, t2);

        public static void Fire<T1, T2, T3>(this Action<T1, T2, T3> action, T1 t1, T2 t2, T3 t3) =>
            action?.Invoke(t1, t2, t3);

        public static void Fire<T1, T2, T3, T4>(this Action<T1, T2, T3, T4> action, T1 t1, T2 t2, T3 t3, T4 t4) =>
            action?.Invoke(t1, t2, t3, t4);

        public static void Fire<T1, T2, T3, T4, T5>(this Action<T1, T2, T3, T4, T5> action, T1 t1, T2 t2, T3 t3, T4 t4,
            T5 t5) =>
            action?.Invoke(t1, t2, t3, t4, t5);

        /// <summary>
        /// Обработка подписок по логике AND 
        /// </summary>
        /// <param name="func"></param>
        /// <param name="returnOnZero">что вернуть если нет подписок</param>
        /// <returns></returns>
        public static bool FireAnd(this Func<bool> func, bool returnOnZero = true)
        {
            try
            {
                var list = func?.GetInvocationList() ?? Array.Empty<Delegate>();
                if (list.Length == 0)
                    return returnOnZero;
                foreach (var d in list)
                    if (!((Func<bool>)d)())
                        return false;
            }
            catch (Exception ex)
            {
                Log.Print(new LogData(LogLevel.Error, ex.Message, func?.Method.Name ?? "ActionExt"));
            }

            return true;
        }

        /// <summary>
        /// Обработка подписок по логике OR
        /// Выполнены будут все подписки, невзирая на последовательность и результат
        /// </summary>
        /// <param name="func"></param>
        /// <param name="returnOnZero">что вернуть если нет подписок</param>
        /// <returns></returns>
        public static bool FireOr(this Func<bool> func, bool returnOnZero = true)
        {
            var result = false;
            try
            {
                var list = func?.GetInvocationList() ?? Array.Empty<Delegate>();
                if (list.Length == 0)
                    return returnOnZero;
                foreach (var d in list)
                    if (((Func<bool>)d)())
                        result = true;
            }
            catch (Exception ex)
            {
                Log.Print(new LogData(LogLevel.Error, ex.Message, func?.Method.Name ?? "ActionExt"));
            }

            return result;
        }

        public static bool FireAnd<T1>(this Func<T1, bool> func, T1 arg1, bool returnOnZero = true)
        {
            try
            {
                var list = func?.GetInvocationList() ?? Array.Empty<Delegate>();
                if (list.Length == 0)
                    return returnOnZero;
                foreach (var d in list)
                    if (!((Func<T1, bool>)d)(arg1))
                        return false;
            }
            catch (Exception ex)
            {
                Log.Print(new LogData(LogLevel.Error, ex.Message, func?.Method.Name ?? "ActionExt"));
            }

            return true;
        }

        public static bool FireOr<T1>(this Func<T1, bool> func, T1 arg1, bool returnOnZero = true)
        {
            var result = false;
            try
            {
                var list = func?.GetInvocationList() ?? Array.Empty<Delegate>();
                if (list.Length == 0)
                    return returnOnZero;
                foreach (var d in list)
                    if (((Func<T1, bool>)d)(arg1))
                        result = true;
            }
            catch (Exception ex)
            {
                Log.Print(new LogData(LogLevel.Error, ex.Message, func?.Method.Name ?? "ActionExt"));
            }

            return result;
        }

        public static bool FireAnd<T1, T2>(this Func<T1, T2, bool> func, T1 arg1, T2 arg2, bool returnOnZero = true)
        {
            try
            {
                var list = func?.GetInvocationList() ?? Array.Empty<Delegate>();
                if (list.Length == 0)
                    return returnOnZero;
                foreach (var d in list)
                    if (!((Func<T1, T2, bool>)d)(arg1, arg2))
                        return false;
            }
            catch (Exception ex)
            {
                Log.Print(new LogData(LogLevel.Error, ex.Message, func?.Method.Name ?? "ActionExt"));
            }

            return true;
        }

        public static bool FireOr<T1, T2>(this Func<T1, T2, bool> func, T1 arg1, T2 arg2, bool returnOnZero = true)
        {
            var result = false;
            try
            {
                var list = func?.GetInvocationList() ?? Array.Empty<Delegate>();
                if (list.Length == 0)
                    return returnOnZero;
                foreach (var d in list)
                    if (((Func<T1, T2, bool>)d)(arg1, arg2))
                        result = true;
            }
            catch (Exception ex)
            {
                Log.Print(new LogData(LogLevel.Error, ex.Message, func?.Method.Name ?? "ActionExt"));
            }

            return result;
        }

        public static bool FireAnd<T1, T2, T3>(this Func<T1, T2, T3, bool> func, T1 arg1, T2 arg2, T3 arg3,
            bool returnOnZero = true)
        {
            try
            {
                var list = func?.GetInvocationList() ?? Array.Empty<Delegate>();
                if (list.Length == 0)
                    return returnOnZero;
                foreach (var d in list)
                    if (!((Func<T1, T2, T3, bool>)d)(arg1, arg2, arg3))
                        return false;
            }
            catch (Exception ex)
            {
                Log.Print(new LogData(LogLevel.Error, ex.Message, func?.Method.Name ?? "ActionExt"));
            }

            return true;
        }

        public static bool FireOr<T1, T2, T3>(this Func<T1, T2, T3, bool> func, T1 arg1, T2 arg2, T3 arg3,
            bool returnOnZero = true)
        {
            var result = false;
            try
            {
                var list = func?.GetInvocationList() ?? Array.Empty<Delegate>();
                if (list.Length == 0)
                    return returnOnZero;
                foreach (var d in list)
                    if (((Func<T1, T2, T3, bool>)d)(arg1, arg2, arg3))
                        result = true;
            }
            catch (Exception ex)
            {
                Log.Print(new LogData(LogLevel.Error, ex.Message, func?.Method.Name ?? "ActionExt"));
            }

            return result;
        }

        public static bool FireAnd<T1, T2, T3, T4>(this Func<T1, T2, T3, T4, bool> func,
            T1 arg1, T2 arg2, T3 arg3, T4 arg4,
            bool returnOnZero = true)
        {
            try
            {
                var list = func?.GetInvocationList() ?? Array.Empty<Delegate>();
                if (list.Length == 0)
                    return returnOnZero;
                foreach (var d in list)
                    if (!((Func<T1, T2, T3, T4, bool>)d)(arg1, arg2, arg3, arg4))
                        return false;
            }
            catch (Exception ex)
            {
                Log.Print(new LogData(LogLevel.Error, ex.Message, func?.Method.Name ?? "ActionExt"));
            }

            return true;
        }

        public static bool FireOr<T1, T2, T3, T4>(this Func<T1, T2, T3, T4, bool> func,
            T1 arg1, T2 arg2, T3 arg3, T4 arg4,
            bool returnOnZero = true)
        {
            var result = false;
            try
            {
                var list = func?.GetInvocationList() ?? Array.Empty<Delegate>();
                if (list.Length == 0)
                    return returnOnZero;
                foreach (var d in list)
                    if (((Func<T1, T2, T3, T4, bool>)d)(arg1, arg2, arg3, arg4))
                        result = true;
            }
            catch (Exception ex)
            {
                Log.Print(new LogData(LogLevel.Error, ex.Message, func?.Method.Name ?? "ActionExt"));
            }

            return result;
        }

        public static bool FireAnd<T1, T2, T3, T4, T5>(this Func<T1, T2, T3, T4, T5, bool> func,
            T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5,
            bool returnOnZero = true)
        {
            try
            {
                var list = func?.GetInvocationList() ?? Array.Empty<Delegate>();
                if (list.Length == 0)
                    return returnOnZero;
                foreach (var d in list)
                    if (!((Func<T1, T2, T3, T4, T5, bool>)d)(arg1, arg2, arg3, arg4, arg5))
                        return false;
            }
            catch (Exception ex)
            {
                Log.Print(new LogData(LogLevel.Error, ex.Message, func?.Method.Name ?? "ActionExt"));
            }

            return true;
        }

        public static bool FireOr<T1, T2, T3, T4, T5>(this Func<T1, T2, T3, T4, T5, bool> func,
            T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5,
            bool returnOnZero = true)
        {
            var result = false;
            try
            {
                var list = func?.GetInvocationList() ?? Array.Empty<Delegate>();
                if (list.Length == 0)
                    return returnOnZero;
                foreach (var d in list)
                    if (((Func<T1, T2, T3, T4, T5, bool>)d)(arg1, arg2, arg3, arg4, arg5))
                        result = true;
            }
            catch (Exception ex)
            {
                Log.Print(new LogData(LogLevel.Error, ex.Message, func?.Method.Name ?? "ActionExt"));
            }

            return result;
        }

        /// <summary>
        /// Сбор параметров от подписчиков в единый массив
        /// </summary>
        /// <param name="func"></param>
        /// <returns></returns>
        public static T[] Accumulate<T>(this Func<T[]> func)
        {
            var result = new List<T>();
            var list = func?.GetInvocationList() ?? Array.Empty<Delegate>();
            try
            {
                foreach (var d in list)
                {
                    var outData = ((Func<T[]>)d)();
                    result.AddRange(outData);
                }
            }
            catch (Exception ex)
            {
                Log.Print(new LogData(LogLevel.Error, ex.Message, func?.Method.Name ?? "ActionExt"));
            }

            return result.ToArray();
        }

        /// <summary>
        /// Сбор параметров от подписчиков в единый массив
        /// </summary>
        /// <param name="func"></param>
        /// <returns></returns>
        public static T[] Accumulate<T>(this Func<T> func)
        {
            var result = new List<T>();
            var list = func?.GetInvocationList() ?? Array.Empty<Delegate>();
            try
            {
                foreach (var d in list)
                {
                    var outData = ((Func<T>)d)();
                    result.Add(outData);
                }
            }
            catch (Exception ex)
            {
                Log.Print(new LogData(LogLevel.Error, ex.Message, func?.Method.Name ?? "ActionExt"));
            }

            return result.ToArray();
        }

        /// <summary>
        /// Возвращает первый не нулл результат
        /// </summary>
        /// <param name="func"></param>
        /// <returns></returns>
        public static T FirstNotNull<T>(this Func<T> func) where T : class
        {
            try
            {
                var list = func?.GetInvocationList() ?? Array.Empty<Delegate>();
                foreach (var d in list)
                    if (((Func<T>)d)() is { } result)
                        return result;
            }
            catch (Exception ex)
            {
                Log.Print(new LogData(LogLevel.Error, ex.Message, func?.Method.Name ?? "ActionExt"));
            }

            return default;
        }

        /// <summary>
        /// Возвращает первый не нулл результат
        /// </summary>
        /// <param name="func"></param>
        /// <param name="arg"></param>
        /// <returns></returns>
        public static T2 FirstNotNull<T1, T2>(this Func<T1, T2> func, T1 arg) where T2 : class
        {
            try
            {
                var list = func?.GetInvocationList() ?? Array.Empty<Delegate>();
                foreach (var d in list)
                    if (((Func<T1, T2>)d)(arg) is { } result)
                        return result;
            }
            catch (Exception ex)
            {
                Log.Print(new LogData(LogLevel.Error, ex.Message, func?.Method.Name ?? "ActionExt"));
            }

            return default;
        }
    }
}