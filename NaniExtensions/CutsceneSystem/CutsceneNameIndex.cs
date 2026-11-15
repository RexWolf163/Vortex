using System.Collections.Generic;
using UnityEngine;
using Vortex.Core.DatabaseSystem.Bus;
using Vortex.Core.LogicChainsSystem.Model;

namespace Vortex.NaniExtensions.CutsceneSystem
{
    /// <summary>
    /// Индекс «имя записи → идентификатор записи» для команд сценария: в сценарии катсцену называют именем
    /// записи её цепочки, а шина катсцен принимает идентификатор.
    ///
    /// Строится лениво — при первом обращении, один раз за запуск, по всем записям <c>LogicChain</c> базы
    /// данных. Пока база не загружена, индекс не строится и ничего не находит: пустой индекс не запоминается.
    ///
    /// Имя должно быть уникальным среди записей <c>LogicChain</c>. Если его носят несколько записей,
    /// в индекс попадает первая, а о повторе пишется ошибка в лог
    /// </summary>
    internal static class CutsceneNameIndex
    {
        /// <summary>
        /// Имя записи → идентификатор записи. <c>null</c> — индекс ещё не построен
        /// </summary>
        private static Dictionary<string, string> _guids;

        /// <summary>
        /// Сброс статики: индекс прошлого запуска (вход в Play Mode без перезагрузки домена) новому не нужен
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Init() => _guids = null;

        /// <summary>
        /// Идентификатор записи цепочки по её имени. <c>null</c> — записи с таким именем нет либо база
        /// данных ещё не загружена
        /// </summary>
        public static string GetGuid(string name)
        {
            if (_guids == null)
            {
                if (!Database.IsInit)
                {
                    Debug.LogError(
                        $"[CutsceneNameIndex] База данных ещё не загружена: имя '{name}' искать негде.");
                    return null;
                }

                _guids = Build();
            }

            return _guids.TryGetValue(name, out var guid) ? guid : null;
        }

        private static Dictionary<string, string> Build()
        {
            var guids = new Dictionary<string, string>();
            foreach (var chain in Database.GetNewRecords<LogicChain>())
            {
                if (string.IsNullOrEmpty(chain.Name))
                    continue;

                if (guids.TryAdd(chain.Name, chain.GuidPreset))
                    continue;

                Debug.LogError(
                    $"[CutsceneNameIndex] Имя '{chain.Name}' носят несколько записей LogicChain: по имени " +
                    $"вызывается '{guids[chain.Name]}', запись '{chain.GuidPreset}' по имени недоступна.");
            }

            return guids;
        }
    }
}
