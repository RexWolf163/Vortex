using System.Collections.Generic;
using System.Linq;
using Naninovel;
using Sirenix.OdinInspector;
using UnityEditor;
using UnityEngine;
using Vortex.Core.Extensions.DefaultEnums;
using Vortex.Core.Extensions.LogicExtensions;
using Vortex.NaniExtensions.Core;
using Vortex.Unity.Extensions;
using Vortex.Unity.UI.Attributes;
using Vortex.Unity.UI.StateSwitcher;

namespace Vortex.NaniExtensions.Misc
{
    public class ActiveCharacterHandler : MonoBehaviour
    {
        [SerializeField, ValueDropdown("NaniVariables")]
        private string actorNameVar = "ActorsNames";

        [SerializeField, ValueDropdown("NaniCharacters")]
        private string characterName;

        [SerializeField, StateSwitcher(typeof(SwitcherState))]
        private UIStateSwitcher switcher;

        private void OnEnable()
        {
            NaniWrapper.TextPrinterManager.OnPrintStarted += OnSpeak;
        }

        private void OnDisable()
        {
            NaniWrapper.TextPrinterManager.OnPrintStarted -= OnSpeak;
        }

        private void OnSpeak(PrintMessageArgs message)
        {
            var authorId = message.Message.Author.Value.Id;

            if (authorId != characterName)
                authorId = CheckPseudonym(authorId);

            var b = authorId == characterName;
            switcher.Set(b ? SwitcherState.On : SwitcherState.Off);
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
        private List<string> NaniVariables()
        {
            var result = new List<string>();
            var config = AssetDatabaseExt.GetSingletonAsset<CustomVariablesConfiguration>();
            if (config != null && config.PredefinedVariables != null)
                result.AddRange(config.PredefinedVariables.Select(customVar => customVar.Name));

            return result;
        }

        private List<string> NaniCharacters()
        {
            var result = new List<string>();
            var guids = AssetDatabase.FindAssets("t:CharactersConfiguration");

            if (guids.Length == 0)
                return result;

            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var config = AssetDatabase.LoadAssetAtPath<CharactersConfiguration>(path);
                if (config != null && config.Metadata != null)
                    result.AddRange(config.Metadata.GetAllIds());
            }

            return result;
        }
#endif
    }
}