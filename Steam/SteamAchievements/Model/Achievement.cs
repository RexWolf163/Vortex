namespace Vortex.Steam.SteamAchievements.Model
{
    public class Achievement
    {
        public string ID { get; internal set; }
        public string Name { get; internal set; }
        public string Description { get; internal set; }
        public bool IsUnlocked { get; internal set; }
        public bool IsHidden { get; internal set; }
    }
}