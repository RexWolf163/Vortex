using System.Threading;
using Cysharp.Threading.Tasks;

namespace Vortex.Sdk.Quests.QuestsLogics
{
    /// <summary>
    /// Маркерная логика.
    /// QuestController обработает ее сохранив Step в квест-хозяин
    /// </summary>
    public class SavePoint : QuestLogic
    {
        public override async UniTask<bool> Run(CancellationToken token)
        {
            await UniTask.Yield();
            return true;
        }

#if UNITY_EDITOR
        protected override string GetEditorLabel() => "Save point";
#endif
    }
}