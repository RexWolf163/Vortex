using System.Collections.Generic;
using Steamworks;
using UnityEngine;
using Vortex.Steam.SteamAchievements.Model;
using Vortex.Steam.SteamConnectionSystem;

namespace Vortex.Steam.SteamAchievements
{
    /// <summary>
    /// Контроллер управления ачивками
    /// Хранит Индекс всех ачивок
    /// 
    /// Частично AI-генерация
    /// </summary>
    internal static class AchievementsController
    {
#if USING_STEAM
        /// <summary>
        /// Индекс всех ачивок проекта
        /// </summary>
        internal static Dictionary<string, Achievement> _achievementsIndex = new();

        [RuntimeInitializeOnLoadMethod]
        private static void Run()
        {
            if (SteamBus.IsInitialized)
                LoadAllAchievements();
            else
                SteamBus.OnCallServices += LoadAllAchievements;
        }

        /// <summary>
        /// Загружаем из сервера все ачивки
        /// </summary>
        private static void LoadAllAchievements()
        {
            if (!SteamBus.IsInitialized)
                return;
            SteamBus.OnCallServices -= LoadAllAchievements;
            _achievementsIndex.Clear();

            // Получаем количество достижений
            var numAchievements = SteamUserStats.GetNumAchievements();

            Debug.Log($"Всего достижений в игре: {numAchievements}");

            for (uint i = 0; i < numAchievements; i++)
            {
                // Получаем ID достижения по индексу
                var achievementID = SteamUserStats.GetAchievementName(i);

                SteamUserStats.GetAchievement(achievementID, out var unlocked);
                var hidden = SteamUserStats.GetAchievementDisplayAttribute(achievementID, "hidden");
                var ach = new Achievement
                {
                    ID = achievementID,
                    Name = SteamUserStats.GetAchievementDisplayAttribute(achievementID, "name"),
                    Description = SteamUserStats.GetAchievementDisplayAttribute(achievementID, "desc"),
                    IsUnlocked = unlocked,
                    IsHidden = hidden == "1"
                };

                _achievementsIndex.Add(ach.ID, ach);
#if UNITY_EDITOR
                Debug.Log(
                    $"[{i}] ID: {ach.ID}\n    Название: {ach.Name}\n    Описание: {ach.Description}\n    Разблокировано: {ach.IsUnlocked}\n    Скрытое: {ach.IsHidden}");
#endif
            }
        }

#endif
    }
}