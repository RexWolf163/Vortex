using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Sirenix.OdinInspector;

namespace Vortex.Sdk.Quests.QuestsLogics
{
    /// <summary>
    /// Логика квеста с жёстким <c>false</c>. Для <c>UnFailable</c>-квеста — способ его зациклить: по
    /// <c>false</c> квест уходит в <c>Locked</c> (а не Failed) и не попадает в <c>CompletedQuests</c>,
    /// после чего перепроверка условий старта запускает его заново. Ставить последней логикой цикла
    /// (после полезных логик и наград). У обычного (не UnFailable) квеста завершит его как <c>Failed</c>.
    ///
    /// Перед <c>false</c> уступает кадр (<see cref="UniTask.Yield()"/>): иначе при постоянно-истинных
    /// условиях старта луп ушёл бы в синхронную рекурсию <c>RunQuest → перепроверка → RunQuest</c> и повесил
    /// бы кадр. С уступкой — один прогон цикла за кадр.
    /// </summary>
    [Serializable]
    public class AlwaysFail : QuestLogic
    {
        [DisplayAsString, HideLabel, ShowInInspector]
        private string label => GetType().Name;

        public override async UniTask<bool> Run(CancellationToken token)
        {
            await UniTask.Yield();
            return false;
        }

#if UNITY_EDITOR
        protected override string GetEditorLabel() => "Always fail (loop UnFailable)";
#endif
    }
}