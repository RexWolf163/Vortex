using Sirenix.OdinInspector;
using UnityEngine;

namespace Vortex.Unity.Components.SceneControllers
{
    public abstract class SceneHandler : MonoBehaviour
    {
        [SerializeField, ValueDropdown("@DropDawnHandler.GetScenes()")]
        protected string sceneName;

        [Button]
        public abstract void Run();
    }
}