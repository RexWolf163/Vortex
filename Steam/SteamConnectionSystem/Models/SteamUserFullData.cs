using System.Collections.Generic;
using UnityEngine;
#if USING_STEAM
using Steamworks;
using System;
#endif
namespace Vortex.Steam.SteamConnectionSystem.Models
{
    public class SteamUserData : SteamUserShortData
    {
#if USING_STEAM
        public event Action OnUpdated;
        public Dictionary<CSteamID, SteamUserShortData> Friends { get; internal set; } = new();

        internal void Init(CSteamID steamID)
        {
            SteamID = steamID;
            Name = SteamFriends.GetPersonaName();
            Friends.Clear();
            var count = SteamFriends.GetFriendCount(EFriendFlags.k_EFriendFlagImmediate);
            for (int i = count - 1; i >= 0; i--)
            {
                var friendId = SteamFriends.GetFriendByIndex(i, EFriendFlags.k_EFriendFlagImmediate);
                var friend = GetShortData(friendId);

                if (Friends.TryAdd(friendId, friend)) continue;
                Debug.LogError("There is broken data");
            }
        }

        internal void CallOnUpdated() => OnUpdated?.Invoke();
#endif
    }
}