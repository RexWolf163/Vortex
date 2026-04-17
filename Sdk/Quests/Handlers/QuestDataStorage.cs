using System;
using Sirenix.OdinInspector;
using UnityEngine;
using Vortex.Core.System.Abstractions;
using Vortex.Sdk.Core.GameCore;
using Vortex.Unity.AppSystem.System.TimeSystem;
using Vortex.Unity.DatabaseSystem.Attributes;
using Vortex.Unity.EditorTools.DataModelSystem;

namespace Vortex.Sdk.Quests.Handlers
{
    public class QuestDataStorage : MonoBehaviour, IDataStorage

    {
        [SerializeField, DbRecord(typeof(QuestModel))]
        private string questGuid;

        public event Action OnUpdateLink;

#if UNITY_EDITOR
        [ShowInInspector, HideInEditorMode, DataModel]
        private QuestModels _quests;

        [ShowInInspector, HideInEditorMode, DataModel]
        private QuestModel _questModel;
#endif

        public T GetData<T>() where T : class
        {
            var quests = GameController.Get<QuestModels>();
#if UNITY_EDITOR
            _quests = quests;
#endif
            if (quests.Index.TryGetValue(questGuid, out var quest))
            {
#if UNITY_EDITOR
                _questModel = quest;
#endif
                return quest as T;
            }

            return null;
        }

        private void OnEnable()
        {
            GameController.OnNewGame += OnNewGame;
            GameController.OnLoadGame += OnNewGame;
        }

        private void OnDisable()
        {
            GameController.OnNewGame -= OnNewGame;
            GameController.OnLoadGame -= OnNewGame;
        }

        private void OnDestroy()
        {
            TimeController.RemoveCall(this);
        }

        private void OnNewGame() => TimeController.Call(() => OnUpdateLink?.Invoke(), this);
    }
}