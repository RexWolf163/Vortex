using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Vortex.Sdk.Quests.QuestsLogics;
#if USING_STEAM
using Vortex.Steam.SteamAchievements;
using Vortex.Steam.SteamConnectionSystem;
#endif

namespace Vortex.Steam.SteamExtensions.Quests
{
    [Serializable]
    public class ActivateAchievementsLogic : QuestLogic
    {
        [SerializeField] private string name;
        [SerializeField] private string achievementId;

        public override async UniTask<bool> Run(CancellationToken token)
        {
#if USING_STEAM
            SteamBus.User.UnlockAchievement(achievementId);
#else
            return false;
#endif
            await UniTask.Yield();
            return true;
        }

#if UNITY_EDITOR
        protected override string GetEditorLabel() => $"Unlock SteamAchievement: {name}";
#endif
    }
}