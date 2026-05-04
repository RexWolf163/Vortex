using System;
using Vortex.Sdk.CharacterViewSystem.Models;
using UnityEngine;
using Vortex.Core.DatabaseSystem.Model.Enums;

namespace Vortex.Sdk.CharacterViewSystem.Presets
{
    [CreateAssetMenu(fileName = "NpcPreset", menuName = "Database/NpcPreset")]
    [Serializable]
    public partial class NpcPreset : CharacterPreset<NonPlayableCharacter>
    {
#if UNITY_EDITOR
        private void OnValidate() => type = RecordTypes.MultiInstance;
#endif
    }
}
