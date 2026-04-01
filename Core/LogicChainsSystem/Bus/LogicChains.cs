using System;
using System.Collections.Generic;
using Vortex.Core.DatabaseSystem.Bus;
using Vortex.Core.Extensions.LogicExtensions;
using Vortex.Core.LoggerSystem.Bus;
using Vortex.Core.LoggerSystem.Model;
using Vortex.Core.LogicChainsSystem.Model;
using Vortex.Core.SettingsSystem.Bus;

namespace Vortex.Core.LogicChainsSystem.Bus
{
    public static class LogicChains
    {
        public const string CompleteChainStep = "-1";
        private static readonly Dictionary<string, LogicChain> Index = new();

        /// <summary>
        /// Создать новую цепочку и добавить ее в реестр
        /// </summary>
        /// <param name="chain"></param>
        /// <returns></returns>
        public static string AddChain(LogicChain chain)
        {
            var guid = Crypto.GetNewGuid();
            Index.Add(guid, chain);
            return guid;
        }

        /// <summary>
        /// Создать новую цепочку и добавить ее в реестр
        /// </summary>
        /// <param name="chainPresetGuid"></param>
        /// <returns></returns>
        public static string AddChain(string chainPresetGuid)
        {
            var chain = Database.GetNewRecord<LogicChain>(chainPresetGuid);
            var guid = Crypto.GetNewGuid();
            Index.Add(guid, chain);
            return guid;
        }

        /// <summary>
        /// Запустить цепочку
        /// </summary>
        /// <param name="guid"></param>
        public static void RunChain(string guid)
        {
            if (!Index.TryGetValue(guid, out var chain))
            {
                Log.Print(new LogData(LogLevel.Error, $"Logic chain not found", "LogicChain"));
                return;
            }

            string startStep;
            if (chain.CurrentStep == null || chain.CurrentStep == "")
            {
                startStep = chain.StartStep;
                chain.CurrentStep = startStep;
            }
            else
                startStep = chain.CurrentStep;

            if (!chain.ChainSteps.TryGetValue(startStep, out var step))
            {
                Log.Print(new LogData(LogLevel.Error, $"Start step error for chain #{chain.Name}", "LogicChain"));
                return;
            }

            var actions = step.Actions;
            foreach (var action in actions)
                action.Invoke();

            var connectors = step.Connectors;
            foreach (var connector in connectors)
            {
                var conditions = connector.Conditions;
                if (conditions.Length == 0)
                {
                    //Нет условий - значит автоматический переход
                    CheckConditions(guid, connector);
                    return;
                }

                foreach (var condition in conditions)
                    condition.Init(() =>
                    {
                        try
                        {
                            CheckConditions(guid, connector);
                        }
                        catch (Exception ex)
                        {
                            Log.Print(new LogData(LogLevel.Error, ex.Message, chain));
                            condition.DeInit();
                        }
                    });
            }
        }

        private static void CheckConditions(string chainGuid, Connector connector)
        {
            if (!Index.ContainsKey(chainGuid))
            {
                Log.Print(new LogData(LogLevel.Error, "Logic chain not found", "LogicChain"));
                return;
            }

            var owner = Index[chainGuid];

            var step = owner.ChainSteps.Get(owner.CurrentStep);
            var conditions = connector.Conditions;
            //Проверка всех условий
            foreach (var condition in conditions)
                if (!condition.Check())
                    return;

            //Если условия выполнены - останавливаем все проверки и уходим на следующий этап
            var targetStep = connector.TargetStepGuid;
            var connectors = step.Connectors;
            foreach (var conn in connectors)
            {
                conditions = conn.Conditions;
                foreach (var condition in conditions)
                    condition.DeInit();
            }

            owner.CurrentStep = targetStep;
            if (targetStep == CompleteChainStep)
            {
                Index.Remove(chainGuid);
                if (Settings.Data().DebugMode)
                    Log.Print(new LogData(LogLevel.Common, "Chain completed and removed", "LogicChains"));
                return;
            }

            RunChain(chainGuid);
        }
    }
}