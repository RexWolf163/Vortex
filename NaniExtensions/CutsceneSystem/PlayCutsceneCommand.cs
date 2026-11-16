using System;
using Naninovel;
using UnityEngine;
using UnityEngine.Scripting;
using Vortex.Unity.CutsceneSystem.Bus;

namespace Vortex.NaniExtensions.CutsceneSystem
{
    /// <summary>
    /// Nani-команда: открыть катсцену. Параметр — ключ катсцены в индексе оркестратора сцены, передаётся шине
    /// как есть. Идущая катсцена сначала закрывается.
    ///
    /// Сценарий ждёт, пока контент загрузится и показ начнётся, — либо пока открытие не будет отклонено
    /// (причину пишет в лог пакет катсцен). Конца катсцены команда не ждёт
    /// </summary>
    /// <example>@cutscene_Play SexSceneSibylla1</example>
    [Serializable]
    [CommandAlias("cutscene_Play"), Preserve]
    public class PlayCutsceneCommand : Command
    {
        /// <summary>
        /// Ключ катсцены
        /// </summary>
        [ParameterAlias(NamelessParameterAlias), RequiredParameter]
        public StringParameter Key;

        public override async UniTask Execute(AsyncToken token = default)
        {
            var key = Key?.Value;
            if (string.IsNullOrEmpty(key))
            {
                Debug.LogError("[PlayCutsceneCommand] Пустой ключ катсцены — команда пропущена.");
                return;
            }

            //Идущую катсцену закрываем и ждём её конца до своей подписки: у неё может быть тот же ключ,
            //и её «показ завершён» отпустил бы это ожидание раньше времени
            if (CutsceneBus.IsActive)
                await CutsceneWait.Closed(token);

            //Сценарий остановили, пока команда ждала: ожидание снято, но открывать катсцену уже не для кого
            if (token.CancellationToken.IsCancellationRequested)
                return;

            var opened = new UniTaskCompletionSource();

            void Release() => opened.TrySetResult();

            void OnShow(string shown)
            {
                if (shown == key)
                    Release();
            }

            CutsceneBus.OnShowStarted += OnShow;
            CutsceneBus.OnShowFinished += OnShow;
            try
            {
                CutsceneBus.Open(key);
                using (token.CancellationToken.Register(Release))
                    await opened.Task;
            }
            finally
            {
                CutsceneBus.OnShowStarted -= OnShow;
                CutsceneBus.OnShowFinished -= OnShow;
            }
        }
    }
}
