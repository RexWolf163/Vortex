using System.Collections.Generic;
using System.Linq;
using Naninovel;
using Sirenix.OdinInspector;
using UnityEngine;
using Vortex.Core.Extensions.LogicExtensions;
using Vortex.NaniExtensions.AudioSystem;
using Vortex.NaniExtensions.Core;
using Vortex.Unity.AppSystem.System.TimeSystem;
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
        [SerializeField, ValueDropdown("NaniVariables")]
        private string actorNameVar = "ActorsNames";

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
            TimeController.RemoveCall(this);
            NaniVoicePlayBus.OnVoiceStart -= OnVoiceStart;
            NaniVoicePlayBus.OnVoiceStop -= OnVoiceStop;
        }

        private void OnVoiceStart(string authorId)
        {
            if (authorId != characterName)
                authorId = CheckPseudonym(authorId);

            if (authorId != characterName) return;
            //Защита от проблем с псевдонимами
            TimeController.Accumulate(() => tweener.Forward(), this);
        }

        private void OnVoiceStop(string authorId)
        {
            if (authorId != characterName)
                authorId = CheckPseudonym(authorId);

            if (authorId != characterName) return;
            TimeController.RemoveCall(this);
            tweener.Back();
        }

        private string CheckPseudonym(string authorId)
        {
            var variablesManager = Engine.GetServiceOrErr<ICustomVariableManager>();
            var actorNames = variablesManager.GetVariableValue(actorNameVar);
            var arNames = actorNames.String.Split(";").Select(s => s.Split(":"));
            var associations = new Dictionary<string, string>();
            foreach (var arName in arNames)
            {
                if (arName[0].IsNullOrWhitespace())
                    continue;
                var name = arName[0];
                var temp = arName[1].Split(",");
                foreach (var pseudoName in temp)
                    associations.Add(pseudoName, name);
            }

            if (associations.Keys.Contains(authorId))
                authorId = associations[authorId];

            return authorId;
        }

#if UNITY_EDITOR
        private List<string> NaniVariables() => NaniWrapper.GetNaniVariables();
        private List<string> NaniCharacters() => NaniWrapper.GetNaniCharacters();
#endif
    }
}