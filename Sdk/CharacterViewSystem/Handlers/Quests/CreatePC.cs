using System;
using System.Threading;
using Vortex.Sdk.CharacterViewSystem.Models;
using Cysharp.Threading.Tasks;
using Sirenix.OdinInspector;
using UnityEngine;
using Vortex.Sdk.Quests.QuestsLogics;
using Vortex.Unity.DatabaseSystem.Attributes;

namespace Vortex.Sdk.CharacterViewSystem.Handlers.Quests
{
    [Serializable]
    public class CreatePC : QuestLogic
    {
        [SerializeField, DbRecord(typeof(PlayableCharacter))]
        private string id;

        [SerializeField, InfoBox("Мягкая проверка. Не проваливается, при неудаче.")]
        private bool softControl;

        public override async UniTask<bool> Run(CancellationToken token)
        {
            var pc = CharactersBus.SpawnPc(id);
            await UniTask.Yield();
            return softControl || pc != null;
        }

#if UNITY_EDITOR
        protected override string GetEditorLabel() => "Create New PC";
#endif
    }
}