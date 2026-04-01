using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Vortex.Core.DatabaseSystem.Bus;
using Vortex.Core.Extensions.LogicExtensions;
using Vortex.Core.UIProviderSystem.Bus;
using Vortex.Core.UIProviderSystem.Model;
using Vortex.Unity.DatabaseSystem.Attributes;

namespace Vortex.Sdk.Quests.QuestsLogics
{
    public class CallUIOpen : QuestLogic
    {
        [SerializeField, DbRecord(typeof(UserInterfaceData))]
        private string uiId;

        public override async UniTask<bool> Run(CancellationToken token)
        {
            UIProvider.Open(uiId);
            await UniTask.Yield();
            return true;
        }

#if UNITY_EDITOR
        protected override string GetEditorLabel()
        {
            var text = "[NOT SET]";
            if (!uiId.IsNullOrWhitespace())
            {
                var record = Database.GetRecord<UserInterfaceData>(uiId);
                if (record != null)
                    text = $"«{record.Name}»";
            }

            return $"Open {text} UI";
        }
#endif
    }
}