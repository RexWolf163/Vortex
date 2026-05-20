using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;
using Vortex.NaniExtensions.AudioSystem;
using Vortex.NaniExtensions.Core;
using Vortex.Unity.EditorTools.Attributes;
using Vortex.Unity.UI.TweenerSystem;

namespace Vortex.NaniExtensions.Misc
{
    /// <summary>
    /// Связывает события <see cref="NaniVoicePlayBus"/> с <see cref="TweenerHub"/>:
    /// начало реплики персонажа, чей ключ выбран в поле <see cref="characterName"/>, запускает Forward;
    /// завершение реплики — Back. На остальных персонажей не реагирует.
    /// </summary>
    public class CharacterVoiceTweenerHandler : MonoBehaviour
    {
        [SerializeField, ValueDropdown("NaniCharacters")]
        private string characterName;

        [SerializeField, AutoLink] private TweenerHub tweener;

        private void OnEnable()
        {
            tweener.Back(true);
            NaniVoicePlayBus.OnVoiceStart += OnVoiceStart;
            NaniVoicePlayBus.OnVoiceStop += OnVoiceStop;
        }

        private void OnDisable()
        {
            NaniVoicePlayBus.OnVoiceStart -= OnVoiceStart;
            NaniVoicePlayBus.OnVoiceStop -= OnVoiceStop;
        }

        private void OnVoiceStart(string authorId)
        {
            if (authorId != characterName) return;
            tweener.Forward();
        }

        private void OnVoiceStop(string authorId)
        {
            if (authorId != characterName) return;
            tweener.Back();
        }

#if UNITY_EDITOR
        private List<string> NaniCharacters() => NaniWrapper.GetNaniCharacters();
#endif
    }
}
