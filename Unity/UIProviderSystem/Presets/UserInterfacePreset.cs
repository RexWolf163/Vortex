using System.Linq;
using Sirenix.OdinInspector;
using UnityEngine;
using Vortex.Core.UIProviderSystem.Enums;
using Vortex.Core.UIProviderSystem.Model;
using Vortex.Unity.DatabaseSystem.Presets;

namespace Vortex.Unity.UIProviderSystem.Presets
{
    [CreateAssetMenu(fileName = "UiPreset", menuName = "Database/UserInterface Preset")]
    public class UserInterfacePreset : RecordPreset<UserInterfaceData>
    {
        [SerializeField] private UserInterfaceTypes uiType;

        public UserInterfaceTypes UIType => uiType;

        [SerializeReference, HideReferenceObjectPicker]
        private UserInterfaceCondition[] conditions = new UserInterfaceCondition[0];

        public UserInterfaceCondition[] Conditions => conditions.ToArray();
    }
}