#if UNITY_EDITOR && USING_STEAM
using System;
using System.Collections.Generic;
using UnityEngine;
using Vortex.Steam.SteamConnectionSystem;
using Vortex.Unity.EditorTools.Attributes;

namespace Vortex.Steam.SteamAchievements.Model
{
    [Serializable, ClassLabel("$Label")]
    public class AchievementHandler
    {
        [InfoBubble("$Info")]
        [ToggleButton("Labels", isSingleButton: true), SerializeField, OnChanged("UpdateAchievements")]
        internal bool isUnlocked = false;

        internal string Id { get; set; }
        internal string Name { get; set; }
        internal string Description { get; set; }

        private string Label() => $"{Name}";
        private string Info() => $"{Name}\n {Description}";

        private void UpdateAchievements()
        {
            if (isUnlocked)
                SteamBus.User.UnlockAchievement(Id);
            else
                AchievementsExtensions.ClearAchievement(Id);
        }

        private Dictionary<int, string> Labels() => new Dictionary<int, string>()
        {
            { 0, "Locked" },
            { 1, "Unlocked" },
        };
    }
}
#endif