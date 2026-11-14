using Naninovel;
using UnityEngine;
using Vortex.Sdk.CutsceneSystem;

namespace Vortex.NaniExtensions.CutsceneSystem
{
    /// <summary>
    /// Сцена катсцены на стороне Naninovel: связывает катсцену <see cref="CutsceneController"/> с фон-актёром
    /// Naninovel. Ключ катсцены — он же id фон-актёра: префаб актёра несёт представление катсцены
    /// (<c>CutsceneView</c>), поэтому показ идёт, пока актёр существует.
    ///
    /// Своего состояния нет: какая катсцена на сцене, известно по <see cref="CutsceneController.Data"/>
    /// </summary>
    internal static class CutsceneStage
    {
        private const string LogTag = "[CutsceneStage]";

        /// <summary>
        /// Открыть катсцену и поднять её фон-актёра. Прежняя катсцена сначала убирается вместе со своим актёром.
        /// Ключа нет в каталоге — ошибку пишет контроллер, актёр не создаётся. Актёр не поднялся — катсцена
        /// закрывается, исключение уходит вызывающему
        /// </summary>
        public static async UniTask Show(string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                Debug.LogError($"{LogTag} Пустой ключ катсцены — команда пропущена.");
                return;
            }

            Hide();

            CutsceneController.Open(key);
            if (!IsShowing(key))
                return;

            var backgrounds = Engine.GetServiceOrErr<IBackgroundManager>();
            try
            {
                var actor = backgrounds.ActorExists(key)
                    ? backgrounds.GetActor(key)
                    : await backgrounds.AddActor(key);

                //Пока актёр грузился, катсцену закрыли или открыли другую — актёр больше не нужен
                if (!IsShowing(key))
                {
                    if (backgrounds.ActorExists(key))
                        backgrounds.RemoveActor(key);
                    return;
                }

                actor.Visible = true;
            }
            catch
            {
                //Без актёра катсцену показывать некому — открытой её не оставляем
                if (IsShowing(key))
                    Hide();
                throw;
            }
        }

        /// <summary>
        /// Закрыть катсцену и убрать её фон-актёра. Катсцена не открыта — ничего не делает
        /// </summary>
        public static void Hide()
        {
            if (!CutsceneController.IsOpen)
                return;

            var key = CutsceneController.Data.Cutscene.name;
            //Сначала закрытие: представление отпускает свои ассеты до того, как актёр будет уничтожен
            CutsceneController.Close();

            var backgrounds = Engine.GetServiceOrErr<IBackgroundManager>();
            if (backgrounds.ActorExists(key))
                backgrounds.RemoveActor(key);
        }

        private static bool IsShowing(string key) =>
            CutsceneController.IsOpen && CutsceneController.Data.Cutscene.name == key;
    }
}
