#if USING_STEAM

using System.Collections.Generic;
using System.Linq;
using Steamworks;
using UnityEngine;
using Vortex.Steam.SteamAchievements.Model;
using Vortex.Steam.SteamConnectionSystem;
using Vortex.Steam.SteamConnectionSystem.Models;
using Vortex.Unity.AppSystem.System.TimeSystem;

namespace Vortex.Steam.SteamAchievements
{
    public static class AchievementsExtensions
    {
        /// <summary>
        /// Разблокировать ачивку
        /// </summary>
        /// <param name="user">Модель данных пользователя стим (просто как якорь метода, так как одна на проект)</param>
        /// <param name="achievementID"></param>
        public static void UnlockAchievement(this SteamUserData user, string achievementID)
        {
            if (!SteamBus.IsLoaded) return;

            var achievement = user.GetAchievement(achievementID);
            if (achievement == null)
            {
                Debug.LogError($"Achievement not found for id: {achievementID}");
                return;
            }

            achievement.IsUnlocked = true;
            SteamUserStats.SetAchievement(achievement.ID);
            TimeController.Accumulate(() => SteamUserStats.StoreStats(), "AchievementsExtensions");
            Debug.Log($"Достижение разблокировано: {achievement.ID}");
        }

        /// <summary>
        /// Получить список всех IDs ачивок проекта
        /// </summary>
        /// <returns></returns>
        public static string[] GetAllAchievementsID(this SteamUserData user) =>
            AchievementsController._achievementsIndex.Keys.ToArray();

        /// <summary>
        /// Получить конкретную ачивку
        /// </summary>
        /// <param name="user"></param>
        /// <param name="achievementID"></param>
        /// <returns></returns>
        public static Achievement GetAchievement(this SteamUserData user, string achievementID) =>
            AchievementsController._achievementsIndex.GetValueOrDefault(achievementID);

#if UNITY_EDITOR
        /// <summary>
        /// Сброс достижения (только для тестирования!)
        /// </summary>
        /// <param name="achievementID"></param>
        public static void ClearAchievement(string achievementID)
        {
            if (!SteamBus.IsLoaded) return;

            SteamUserStats.ClearAchievement(achievementID);
            SteamUserStats.StoreStats();

            Debug.Log($"Достижение сброшено: {achievementID}");
        }


        // Сброс всех достижений (для тестирования)
        public static void ResetAllAchievements()
        {
            if (!SteamBus.IsLoaded) return;

            SteamUserStats.ResetAllStats(true);
            SteamUserStats.StoreStats();

            Debug.Log("Все достижения сброшены!");
        }
#endif
    }
}

#endif