using Naninovel;
using UnityEngine;
using Vortex.Unity.CutsceneSystem.Bus;

namespace Vortex.NaniExtensions.CutsceneSystem
{
    /// <summary>
    /// Ожидания для команд сценария: шина катсцен исполняет команды в конце кадра, а сценарий должен
    /// продолжиться только после их результата
    /// </summary>
    internal static class CutsceneWait
    {
        /// <summary>
        /// Пропустить идущую катсцену и дождаться, пока она закроется. Ждёт ближайшего «показ завершён»
        /// с любым ключом; остановка сценария ожидание снимает
        /// </summary>
        public static async UniTask Closed(AsyncToken token)
        {
            var closed = new UniTaskCompletionSource();

            void Release() => closed.TrySetResult();

            void OnFinished(string key) => Release();

            CutsceneBus.OnShowFinished += OnFinished;
            try
            {
                CutsceneBus.Skip();
                using (token.CancellationToken.Register(Release))
                    await closed.Task;
            }
            finally
            {
                CutsceneBus.OnShowFinished -= OnFinished;
            }
        }

        /// <summary>
        /// Дождаться, пока откроется замок «дальше» (<see cref="CutsceneBus.IsLocked"/>): узел с автопереходом
        /// доиграет. Замок проверяется в конце кадра — к этому моменту оркестратор уже исполнил автопереход,
        /// который отпустил замок. Ожидание снимают закрытие катсцены, остановка сценария и запрос плеера
        /// завершить команды
        /// </summary>
        public static async UniTask Unlocked(AsyncToken token)
        {
            var player = Engine.GetServiceOrErr<IScriptPlayer>();
            while (CutsceneBus.IsLocked.Value && CutsceneBus.IsActive && Application.isPlaying && !player.Completing &&
                   token.EnsureNotCanceledOrCompleted())
                await AsyncUtils.WaitEndOfFrame(token);
        }
    }
}
