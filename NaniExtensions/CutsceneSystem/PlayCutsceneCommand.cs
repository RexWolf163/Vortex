using System;
using Naninovel;
using UnityEngine.Scripting;

namespace Vortex.NaniExtensions.CutsceneSystem
{
    /// <summary>
    /// Nani-команда: открыть катсцену по ключу каталога и показать её. Ключ — он же id фон-актёра Naninovel,
    /// на префабе которого стоит представление катсцены. Открытая катсцена сначала закрывается.
    /// Команда завершается, когда фон-актёр поднят; загрузку скелета и звука катсцены она не ждёт
    /// </summary>
    /// <example>@cutscene_Play SexSceneAster1</example>
    [Serializable]
    [CommandAlias("cutscene_Play"), Preserve]
    public class PlayCutsceneCommand : Command
    {
        [ParameterAlias(NamelessParameterAlias), RequiredParameter]
        public StringParameter Key;

        public override UniTask Execute(AsyncToken token = default) => CutsceneStage.Show(Key?.Value);
    }
}
