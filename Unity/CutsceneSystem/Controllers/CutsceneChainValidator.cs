using System.Collections.Generic;
using Vortex.Core.LogicChainsSystem.Model;
using Vortex.Unity.CutsceneSystem.LogicChains;
using ChainsBus = Vortex.Core.LogicChainsSystem.Bus.LogicChains;

namespace Vortex.Unity.CutsceneSystem.Controllers
{
    /// <summary>
    /// Проверка цепочки катсцены перед запуском. Цепочка обязана быть линейной, ждать только сигнала шины
    /// и заканчиваться этапом завершения — иначе пролёт застрянет, а контент не выгрузится
    /// </summary>
    internal static class CutsceneChainValidator
    {
        /// <summary>
        /// Проходит цепочку от начального этапа до конца. Правила: у этапа ровно один переход; на переходе
        /// либо нет условий, либо одно условие <see cref="CutsceneReadyCondition"/>; звено
        /// <see cref="CutsceneComplete"/> стоит в последнем этапе и только в нём; переход последнего этапа — без
        /// условий
        /// </summary>
        public static bool Validate(LogicChain chain, out string error)
        {
            var visited = new HashSet<string>();
            var guid = chain.StartStep;
            while (true)
            {
                if (string.IsNullOrEmpty(guid) || !chain.ChainSteps.TryGetValue(guid, out var step))
                {
                    error = "переход ведёт на несуществующий этап";
                    return false;
                }

                if (!visited.Add(guid))
                {
                    error = $"этап «{step.Name}» встречается дважды — цепочка зациклена";
                    return false;
                }

                var connectors = step.Connectors;
                if (connectors == null || connectors.Length != 1)
                {
                    error = $"у этапа «{step.Name}» должен быть ровно один переход";
                    return false;
                }

                var conditions = connectors[0].Conditions;
                var conditionsCount = conditions?.Length ?? 0;
                if (conditionsCount > 1 || (conditionsCount == 1 && conditions[0] is not CutsceneReadyCondition))
                {
                    error =
                        $"переход этапа «{step.Name}»: допустимо одно условие {nameof(CutsceneReadyCondition)} либо ни одного";
                    return false;
                }

                var completes = HasComplete(step);
                var last = connectors[0].TargetStepGuid == ChainsBus.CompleteChainStep;
                if (completes != last)
                {
                    error = last
                        ? $"в последнем этапе «{step.Name}» нет звена {nameof(CutsceneComplete)}"
                        : $"звено {nameof(CutsceneComplete)} стоит не в последнем этапе («{step.Name}»)";
                    return false;
                }

                if (last)
                {
                    if (conditionsCount != 0)
                    {
                        error = $"переход последнего этапа «{step.Name}» должен быть без условий";
                        return false;
                    }

                    error = null;
                    return true;
                }

                guid = connectors[0].TargetStepGuid;
            }
        }

        private static bool HasComplete(ChainStep step)
        {
            if (step.Actions == null)
                return false;

            foreach (var action in step.Actions)
                if (action is CutsceneComplete)
                    return true;

            return false;
        }
    }
}
