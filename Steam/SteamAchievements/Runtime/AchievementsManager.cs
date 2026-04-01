#if USING_STEAM && UNITY_EDITOR
using UnityEngine;
using Vortex.Steam.SteamAchievements.Model;
using Vortex.Steam.SteamConnectionSystem;
using Vortex.Unity.EditorTools.Abstraction;
using Vortex.Unity.EditorTools.Attributes;

namespace Vortex.Steam.SteamAchievements.Runtime
{
    public class AchievementsManager : MonoBehaviour, IUseVortexCollectionRendering
    {
        private static AchievementsManager _instance;

        [VortexCollection]
        [LabelText("Ачивки")]
        [SerializeField] public AchievementHandler[] index;

        [RuntimeInitializeOnLoadMethod]
        private static void Init()
        {
            var go = new GameObject("AchievementsManager [TEST]");
            _instance = go.AddComponent<AchievementsManager>();
            DontDestroyOnLoad(_instance);
        }

        private void Start()
        {
            if (_instance != this)
            {
                Destroy(this);
                Debug.LogError("Попытка повторного создания AchievementsManager");
                return;
            }

            SteamBus.User.OnUpdated += Refresh;
            Refresh();
        }

        private void OnDestroy()
        {
            SteamBus.User.OnUpdated -= Refresh;
            if (_instance == this)
                _instance = null;
        }

        private void Refresh()
        {
            if (!SteamBus.IsLoaded)
                return;
            var achs = SteamBus.User.GetAllAchievementsID();
            index = new AchievementHandler[achs.Length];
            for (var i = achs.Length - 1; i >= 0; i--)
            {
                var achId = achs[i];
                var ach = SteamBus.User.GetAchievement(achId);
                index[i] = new AchievementHandler()
                {
                    Id = ach.ID,
                    Name = ach.Name,
                    Description = ach.Description,
                };
            }
        }
    }
}

#endif