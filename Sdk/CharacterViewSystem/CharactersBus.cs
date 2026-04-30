using System;
using System.Collections.Generic;
using Vortex.Sdk.CharacterViewSystem.Controllers;
using Vortex.Sdk.CharacterViewSystem.Models;
using Vortex.Core.DatabaseSystem.Bus;
using Vortex.Core.Extensions.LogicExtensions;
using Vortex.Core.System.Abstractions;

namespace Vortex.Sdk.CharacterViewSystem
{
    /// <summary>
    /// Шина доступа к данным персонажей
    /// </summary>
    public partial class CharactersBus : Singleton<CharactersBus>
    {
        public static event Action OnPcChanged;
        public static event Action OnNpcChanged;

        /// <summary>
        /// Индекс персонажей игрока.
        /// </summary>
        private static readonly Dictionary<string, PlayableCharacter> PcIndex = new();

        /// <summary>
        /// Индекс NPC.
        /// </summary>
        private static readonly Dictionary<string, Dictionary<string, NonPlayableCharacter>> NpcIndex = new();

        /// <summary>
        /// Добавить в индекс персонажа по указанному пресету.
        /// Возвращает модель персонажа.
        /// Если ID не найден или персонаж уже принадлежит игроку - вернет null
        /// </summary>
        /// <param name="charId"></param>
        /// <returns></returns>
        public static PlayableCharacter SpawnPc(string charId)
        {
            if (!PcIndex.ContainsKey(charId) || PcIndex[charId].IsOwned)
                return null;
            PcIndex[charId].SetOwnedValue(true);

            OnPcChanged?.Invoke();
            return PcIndex[charId];
        }

        /// <summary>
        /// Добавить в индекс персонажа по указанному пресету
        /// Возвращает модель персонажа
        /// </summary>
        /// <param name="charPresetId"></param>
        /// <returns></returns>
        public static NonPlayableCharacter SpawnNpc(string charPresetId)
        {
            if (!NpcIndex.TryGetValue(charPresetId, out var characters))
            {
                characters = new Dictionary<string, NonPlayableCharacter>();
                NpcIndex.Add(charPresetId, characters);
            }

            var charId = Crypto.GetNewGuid();
            var newRecord = Database.GetNewRecord<NonPlayableCharacter>(charPresetId);
            newRecord.Lock();
            characters.Add(charId, newRecord);
            OnNpcChanged?.Invoke();
            return characters[charId];
        }

        /// <summary>
        /// Возвращает модель персонажа с указанным ID пресета.
        /// Если модель еще не получена из БД, она будет вставлена в индекс в этот момент
        /// </summary>
        /// <param name="charId"></param>
        /// <returns></returns>
        public static PlayableCharacter GetPlayableCharacter(string charId)
        {
            var character = PcIndex[charId];
            if (character == null)
            {
                PcIndex[charId] = Database.GetRecord<PlayableCharacter>(charId);
                character = PcIndex[charId];
            }

            return character;
        }

        /// <summary>
        /// Возвращает полный индекс-словарь всех игровых персонажей, включая те, которыми не владеет игрок
        /// </summary>
        /// <returns></returns>
        public static IReadOnlyDictionary<string, PlayableCharacter> GetPlayableCharacters() => PcIndex;

        /// <summary>
        /// Возвращает полный индекс-словарь всех неигровых персонажей для данного пресета
        /// </summary>
        /// <returns></returns>
        public static IReadOnlyDictionary<string, NonPlayableCharacter> GetNpcCharacters(string presetId) =>
            NpcIndex[presetId];

        /// <summary>
        /// Удаляет модель данных из индекса
        /// </summary>
        /// <param name="charId"></param>
        public static void KillPc(string charId)
        {
            PcIndex.Remove(charId);
            OnPcChanged?.Invoke();
        }

        /// <summary>
        /// Удаляет модель данных из индекса
        /// </summary>
        /// <param name="charId"></param>
        public static void KillNpc(string charPresetId, string charId)
        {
            NpcIndex[charPresetId].Remove(charId);
            if (NpcIndex[charPresetId].Count == 0)
                NpcIndex.Remove(charPresetId);
            OnNpcChanged?.Invoke();
        }
    }
}