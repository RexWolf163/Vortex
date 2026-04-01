#if USING_STEAM
using Steamworks;
#endif
namespace Vortex.Steam.SteamConnectionSystem.Models
{
    public class SteamUserShortData
    {
#if USING_STEAM
        public CSteamID SteamID { get; internal set; }
        public string Name { get; internal set; }

        internal static SteamUserShortData GetShortData(CSteamID steamID)
        {
            return new SteamUserShortData
            {
                SteamID = steamID,
                Name = SteamFriends.GetFriendPersonaName(steamID)
            };
        }
#endif
    }
}