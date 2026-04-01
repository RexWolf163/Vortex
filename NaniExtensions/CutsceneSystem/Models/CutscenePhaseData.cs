using UnityEngine;

namespace Vortex.NaniExtensions.CutsceneSystem.Models
{
    [System.Serializable]
    public class CutscenePhaseData
    {
        [field: SerializeField] public string AuthorTextKey { get; private set; } = string.Empty;
        [field: SerializeField] public string DialogueTextKey { get; private set; } = string.Empty;
        [field: SerializeField] public AudioClip RuVO { get; private set; } = null;
        [field: SerializeField] public AudioClip EnVO { get; private set; } = null;
    }
}