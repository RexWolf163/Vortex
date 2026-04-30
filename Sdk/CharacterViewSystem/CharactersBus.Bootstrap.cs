using AppScripts.CharacterViewSystem.Controllers;
using AppScripts.CharacterViewSystem.Models;
using UnityEngine;
using Vortex.Core.DatabaseSystem.Bus;
using Vortex.Core.Extensions.LogicExtensions;
using Vortex.Core.SaveSystem.Bus;

namespace AppScripts.CharacterViewSystem
{
    public partial class CharactersBus
    {
        [RuntimeInitializeOnLoadMethod]
        public static void Boot()
        {
            Database.OnInit -= Init;
            Database.OnInit += Init;

            SaveController.Register(Instance);
        }

        private static void Init()
        {
            PcIndex.Clear();
            var list = Database.GetRecords<PlayableCharacter>();
            foreach (var record in list)
            {
                record.Lock();
                PcIndex.AddNew(record.GuidPreset, record);
            }

            NpcIndex.Clear();
        }
    }
}