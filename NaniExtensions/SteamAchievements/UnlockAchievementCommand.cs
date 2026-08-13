using System;
using Naninovel;
using UnityEngine;
using UnityEngine.Scripting;
#if USING_STEAM
using Vortex.Steam.SteamAchievements;
using Vortex.Steam.SteamConnectionSystem;
#endif

namespace Vortex.NaniExtensions.SteamAchievements
{
    /// <summary>
    /// Nani-команда: разблокировать Steam-достижение по строковому ключу (Steam achievement ID).
    /// Кросс-пакет Naninovel ↔ <c>Vortex.Steam.SteamAchievements</c>: сама команда живёт под
    /// <c>USING_NANINOVELL</c> (чтобы скрипты всегда парсились), а обращение к Steam — под
    /// <c>USING_STEAM</c>. Без Steam команда существует, но выполняется как no-op.
    /// Гарды (<c>SteamBus.IsLoaded</c>, наличие ключа в индексе) — внутри
    /// <see cref="AchievementsExtensions.UnlockAchievement"/>.
    /// </summary>
    /// <example>@achievement ACH_WIN_FIRST_BATTLE</example>
    [Serializable]
    [CommandAlias("UnlockSteamAchievement"), Preserve]
    public class UnlockAchievementCommand : Command
    {
        [ParameterAlias(NamelessParameterAlias), RequiredParameter]
        public StringParameter Key;

        public override UniTask Execute(AsyncToken token = default)
        {
            var key = Key?.Value;
            if (string.IsNullOrEmpty(key))
            {
                Debug.LogError("[UnlockAchievementCommand] Пустой ключ достижения — команда пропущена.");
                return UniTask.CompletedTask;
            }

#if USING_STEAM
            SteamBus.User.UnlockAchievement(key);
#else
            Debug.LogWarning(
                $"[UnlockAchievementCommand] USING_STEAM выключен — достижение '{key}' не разблокировано (no-op).");
#endif
            return UniTask.CompletedTask;
        }
    }
}