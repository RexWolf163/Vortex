using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Vortex.Sdk.Quests.QuestsLogics
{
    /// <summary>
    /// Маркерная логика.
    /// QuestController обработает ее сохранив Step в квест-хозяин
    /// </summary>
    [Serializable]
    public class SavePoint : QuestLogic
    {
        [SerializeField, Range(1, 255)] private byte key = 1;

        public byte Key => key;

        public override async UniTask<bool> Run(CancellationToken token)
        {
            await UniTask.Yield();
            return true;
        }

#if UNITY_EDITOR
        protected override string GetEditorLabel() => "Save point";
#endif
    }
}