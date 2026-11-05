using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using Sirenix.OdinInspector;
using UnityEngine;
using Vortex.Core.Extensions.LogicExtensions;
using Vortex.NaniExtensions.Core;
using Vortex.NaniExtensions.CustomVariablesDashboard;
using Vortex.Sdk.Quests.QuestsLogics;
using CustomVariablesConfiguration = Naninovel.CustomVariablesConfiguration;
using CustomVariableUpdatedArgs = Naninovel.CustomVariableUpdatedArgs;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Vortex.NaniExtensions.QuestSystem.Logics
{
    /// <summary>
    /// FALSE - квест провален!
    /// </summary>
    [Serializable]
    public class NaniVariableValueWait : QuestLogic, INaniVariableReference
    {
        [SerializeField, ValueDropdown("NaniVariables"), HorizontalGroup, HideLabel]
        private string variableName;

        [SerializeField, HorizontalGroup, HideLabel]
        private string target;

        string INaniVariableReference.VariableName => variableName;
        NaniVariableOperation INaniVariableReference.Operation => NaniVariableOperation.Wait;
        string INaniVariableReference.Operand => target;

        private string _target;

        private bool _checkResult;

        private string Target
        {
            get
            {
                if (_target.IsNullOrWhitespace())
                    _target = target.Trim().ToLowerInvariant(); // Invariant: культурная ToLower ломает I на турецкой локали
                return _target;
            }
        }

        public override async UniTask<bool> Run(CancellationToken token)
        {
            var variable = NaniWrapper.VariablesManager.GetVariableValue(variableName);
            _checkResult = variable.Check(target);
            if (!_checkResult)
                NaniWrapper.VariablesManager.OnVariableUpdated += Check;

            while (!_checkResult)
                await UniTask.Yield();

            NaniWrapper.VariablesManager.OnVariableUpdated -= Check;
            await UniTask.Yield();

            return true;
        }

        private void Check(CustomVariableUpdatedArgs arg)
        {
            var variable = NaniWrapper.VariablesManager.GetVariableValue(variableName);
            _checkResult = variable.Check(target);
        }

#if UNITY_EDITOR
        protected override string GetEditorLabel() =>
            $"Wait for {variableName ?? "[NOT SET]"} is {target ?? "[NOT SET]"}";

        private List<string> NaniVariables()
        {
            var result = new List<string>();
            var guids = AssetDatabase.FindAssets("t:CustomVariablesConfiguration");

            if (guids.Length == 0)
                return result;

            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var config = AssetDatabase.LoadAssetAtPath<CustomVariablesConfiguration>(path);
                if (config != null && config.PredefinedVariables != null)
                    result.AddRange(config.PredefinedVariables.Select(customVar => customVar.Name));
            }

            result.Sort();
            return result;
        }

#endif
    }
}