using Naninovel;
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
    }
}
