using System;
using System.Linq;
using UnityEngine;
using Vortex.Core.DatabaseSystem.Bus;
using Vortex.Core.Extensions.LogicExtensions;
using Vortex.Sdk.Core.GameCore;
using Vortex.Sdk.MapLevels.Bus;
using Vortex.Sdk.MapLevels.Model;

namespace Vortex.Sdk.MapLevels.Controllers
{
    public partial class MapLevelsController
    {
        public event Action OnInitialized;

        public void Init()
        {
            if (IsInitialized) return;

            Model = new MapLevelsModel();
            Model.ActiveLevelGuid.SetOwner(this);

            BuildCatalog();

            GameController.OnNewGame += OnGameReset;
            GameController.OnLoadGame += OnGameReset;

            IsInitialized = true;
            MapLevelsBus.NotifyReady();
            OnInitialized?.Invoke();

            MapLevelsBus.OnMapsContainerRegistered += MoveMaps;
            MapLevelsBus.OnMapsContainerReleased += MoveMaps;

            // Если игра уже идёт (например, контроллер инициализирован после NewGame) —
            // войти в текущий уровень из GameModel.
            EnterCurrentFromGameData();
        }

        /// <summary>
        /// Переброска загруженных префабов между контейнерами
        /// </summary>
        private void MoveMaps()
        {
            var parent = GetMapsParent();
            var list = Model.Containers.Values;
            foreach (var mapContainer in list)
            {
                if (mapContainer.Instance == null)
                    continue;
                mapContainer.Instance.transform.SetParent(parent, false);
                mapContainer.Instance.transform.localPosition = Vector3.zero;
                mapContainer.Instance.transform.localRotation = Quaternion.identity;
            }
        }

        public void Cleanup()
        {
            if (!IsInitialized) return;

            GameController.OnNewGame -= OnGameReset;
            GameController.OnLoadGame -= OnGameReset;

            UnloadAll();

            Model = null;
            IsInitialized = false;
            MapLevelsBus.NotifyReleased();
        }

        /// <summary>
        /// Заполняет MapLevelsModel.Catalog рабочими копиями всех Singleton-записей MapLevelModel.
        /// Контейнеры создаются пустыми — инстанциация лениво, при Enter.
        /// </summary>
        private void BuildCatalog()
        {
            // MapLevelPreset фиксирован как Singleton → используется GetRecords (без New).
            var records = Database.GetRecords<MapLevelModel>();
            if (records == null) return;

            foreach (var record in records)
            {
                if (record == null) continue;
                Model.Catalog[record.GuidPreset] = record;
                Model.Containers[record.GuidPreset] = new MapContainer(record.GuidPreset);
            }
        }

        /// <summary>
        /// Обработчик OnNewGame и OnLoadGame после первой инициализации.
        /// Сбрасывает всё загруженное и заходит в уровень из MapLevelsGameData.
        /// </summary>
        private void OnGameReset()
        {
            UnloadAll();
            EnterCurrentFromGameData();
        }

        /// <summary>
        /// Читает CurrentLevelGuid из MapLevelsGameData и входит в этот уровень.
        /// Если поле пустое — выбирает первый из каталога (MVP-поведение).
        /// </summary>
        private void EnterCurrentFromGameData()
        {
            var data = GameController.Get<MapLevelsGameData>();
            if (data == null) return;

            var target = data.CurrentLevelGuid;
            if (target.IsNullOrWhitespace())
                return;

            Enter(target);
        }
    }
}