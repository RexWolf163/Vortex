using System;
using UnityEngine;
using Vortex.Core.System.Abstractions;
using Vortex.Sdk.Core.GameCore;
using Vortex.Unity.AppSystem.System.TimeSystem;
using Vortex.Unity.DatabaseSystem.Attributes;

namespace Vortex.Sdk.Quests.Handlers
{
    public class QuestDataStorage : MonoBehaviour, IDataStorage

    {
        [SerializeField, DbRecord(typeof(QuestModel))]
        private string questGuid;

        public event Action OnUpdateLink;

        public T GetData<T>() where T : class
        {
            var quests = GameController.Get<QuestModels>();
            if (quests.Index.TryGetValue(questGuid, out var quest))
                return quest as T;
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