using System;
using UnityEngine;
using Vortex.Core.AppSystem.Bus;
using Vortex.Core.DatabaseSystem.Bus;
using Vortex.Core.Extensions.LogicExtensions;
using Vortex.Core.System.Enums;
using Vortex.Sdk.Core.GameCore;
using Vortex.Sdk.MapLevels.Bus;
using Vortex.Sdk.MapLevels.Model;
using Object = UnityEngine.Object;

namespace Vortex.Sdk.MapLevels.Controllers
{
    public partial class MapLevelsController
    {
        public event Action OnInitialized;
        public event Action OnReleased;

        public void Init()
        {
            if (IsInitialized) return;

            Model = new MapLevelsModel();
            Model.ActiveLevelGuid.SetOwner(this);

            BuildCatalog();

            GameController.OnNewGame += OnGameReset;
            GameController.OnLoadGame += OnGameReset;

            _ = VoidParent;

            MapLevelsBus.OnMapsContainerRegistered += MoveMaps;
            MapLevelsBus.OnMapsContainerReleased += MoveMaps;

            IsInitialized = true;
            OnInitialized?.Invoke();

            // Если игра уже идёт (например, контроллер инициализирован после NewGame) —
            // войти в текущий уровень из GameModel.
            EnterCurrentFromGameData();
        }

        /// <summary>
        /// Переброска загруженных префабов между контейнерами
        /// </summary>
        private void MoveMaps()
        {
            var list = Model.Containers.Values;
            if (App.GetState() == AppStates.Stopping)
            {
                foreach (var mapContainer in list)
                {
                    if (mapContainer.Instance == null)
                        continue;
                    Object.Destroy(mapContainer.Instance);
                }

                return;
            }

            var parent = GetMapsParent();
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

            MapLevelsBus.OnMapsContainerRegistered -= MoveMaps;
            MapLevelsBus.OnMapsContainerReleased -= MoveMaps;

            UnloadAll();

            if (_voidParent != null)
            {
                Object.Destroy(_voidParent.gameObject);
                _voidParent = null;
            }

            Model = null;
            IsInitialized = false;
            OnReleased?.Invoke();
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