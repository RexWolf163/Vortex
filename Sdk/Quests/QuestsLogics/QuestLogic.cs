using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Sirenix.OdinInspector;
using Vortex.Unity.EditorTools.Attributes;

namespace Vortex.Sdk.Quests.QuestsLogics
{
    /// <summary>
    /// Логика относящаяся к квесту
    /// Активируется снаружи (из контроллера)
    /// Имеет логику запуска, возвращающую по завершении bool 
    /// </summary>
    [Serializable, HideReferenceObjectPicker, ClassLabel("$GetEditorLabel")]
    public abstract class QuestLogic
    {
        public abstract UniTask<bool> Run(CancellationToken token);

#if UNITY_EDITOR
        protected abstract string GetEditorLabel();
#endif
    }
}