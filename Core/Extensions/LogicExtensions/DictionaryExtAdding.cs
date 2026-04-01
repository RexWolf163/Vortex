using System.Collections.Generic;
using Vortex.Core.LoggerSystem.Bus;
using Vortex.Core.LoggerSystem.Model;

namespace Vortex.Core.Extensions.LogicExtensions
{
    public static class DictionaryExtAdding
    {
        public static bool AddNew<T1, T2>(this IDictionary<T1, T2> dict, T1 key, T2 value)
        {
            if (dict.TryGetValue(key, out var temp))
            {
                if (!Equals(temp, value))
                {
                    Log.Print(new LogData(LogLevel.Error, $"This key({key}) is busy", "DictionaryExtAdding"));
                    return false;
                }

                Log.Print(new LogData(LogLevel.Warning,
                    $"This Object in dictionary already exists under this key ({key})", "DictionaryExtAdding"));
                return false;
            }

            dict.Add(key, value);
            return true;
        }

        public static T2 Get<T1, T2>(this IDictionary<T1, T2> dict, T1 key)
        {
            if (dict.TryGetValue(key, out var value))
                return value;
            Log.Print(new LogData(LogLevel.Warning,
                $"key({key}) not exists in dictionary", "DictionaryExtAdding"));
            return default;
        }
    }
}