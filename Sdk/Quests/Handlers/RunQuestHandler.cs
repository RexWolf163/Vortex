using Sirenix.OdinInspector;
using UnityEngine;
using Vortex.Core.System.Abstractions;
using Vortex.Unity.EditorTools.Attributes;

namespace Vortex.Sdk.Quests.Handlers
{
    public class RunQuestHandler : MonoBehaviour
    {
        [SerializeField, ClassFilter(typeof(IDataStorage))]
        private MonoBehaviour dataStorage;

        private IDataStorage DataStorage => dataStorage as IDataStorage;

        [Button, HideInEditorMode]
        public void RunQuest()
        {
            var quest = DataStorage.GetData<QuestModel>();
            if (quest.State == QuestState.Ready)
                quest.Run();
        }
    }
}