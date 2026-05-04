using System;
using Vortex.Sdk.CharacterViewSystem.Models;
using UnityEngine;
using Vortex.Core.DatabaseSystem.Model.Enums;

namespace Vortex.Sdk.CharacterViewSystem.Presets
{
    [CreateAssetMenu(fileName = "PcPreset", menuName = "Database/PcPreset")]
    [Serializable]
    public partial class PcPreset : CharacterPreset<PlayableCharacter>
    {
#if UNITY_EDITOR
        private void OnValidate() => type = RecordTypes.Singleton;
#endif
    }
}
