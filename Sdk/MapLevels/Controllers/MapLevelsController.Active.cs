using System;
using UnityEngine;
using Vortex.Sdk.Core.GameCore;
using Vortex.Sdk.MapLevels.Model;

namespace Vortex.Sdk.MapLevels.Controllers
{
    public partial class MapLevelsController
    {
        public event Action<string> OnActiveLevelChanged;

        public void Enter(string levelGuid)
        {
            if (!IsInitialized)
            {
                Debug.LogError("[MapLevelsController] Enter до Init — отмена.");
                return;
            }

            if (string.IsNullOrEmpty(levelGuid))
            {
                Debug.LogError("[MapLevelsController] Enter с пустым levelGuid — отмена.");
                return;
            }

            if (!Model.Catalog.ContainsKey(levelGuid))
            {
                Debug.LogError($"[MapLevelsController] Уровень с GUID \"{levelGuid}\" не найден в каталоге.");
                return;
            }

            // Если активный уровень не меняется — ничего не делаем (защита от повторных вызовов).
            if (Model.ActiveLevelGuid.Value == levelGuid)
                return;

            // Гарантированно загружаем целевой уровень синхронно (если ещё не загружен).
            EnsureLoaded(levelGuid);

            // Переключаем активный.
            Model.ActiveLevelGuid.Set(levelGuid, this);

            // Сохраняем первичные данные в GameModel.
            var data = GameController.Get<MapLevelsGameData>();
            if (data != null)
                data.CurrentLevelGuid = levelGuid;

            OnActiveLevelChanged?.Invoke(levelGuid);

            // Запускаем фоновый стриминг план: предзагрузка соседей hop=1, выгрузка hop >= X.
            // Async — внутри контура, наружу не торчит.
            ApplyStreamingPlan(levelGuid);
        }
    }
}
