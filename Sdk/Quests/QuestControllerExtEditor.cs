#if UNITY_EDITOR
using UnityEditor;
using Vortex.Sdk.Core.GameCore;

namespace Vortex.Sdk.Quests
{
    /// <summary>
    /// Расширение на функциональный сахар для редактора
    /// </summary>
    public static partial class QuestController
    {
        [InitializeOnLoadMethod]
        private static void EditorRun()
        {
            GameController.OnEditorGetData -= NewGameLogic;
            GameController.OnEditorGetData += NewGameLogic;
        }
    }
}

#endif