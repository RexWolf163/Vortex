using System;
using Naninovel;
using UnityEngine;
using UnityEngine.Scripting;
using Vortex.Unity.CutsceneSystem.Bus;

namespace Vortex.NaniExtensions.CutsceneSystem
{
    /// <summary>
    /// Nani-команда: открыть катсцену. Параметр — имя записи <c>LogicChain</c>, описывающей катсцену;
    /// идентификатор записи для шины катсцен команда находит по имени через <see cref="CutsceneNameIndex"/>.
    /// Идущая катсцена сначала пролетает до конца.
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
        /// Имя записи цепочки катсцены
        /// </summary>
        [ParameterAlias(NamelessParameterAlias), RequiredParameter]
        public StringParameter Key;

        public override async UniTask Execute(AsyncToken token = default)
        {
            var name = Key?.Value;
            if (string.IsNullOrEmpty(name))
            {
                Debug.LogError("[PlayCutsceneCommand] Пустое имя катсцены — команда пропущена.");
                return;
            }

            //Шина катсцен и её события работают с идентификатором записи цепочки
            var key = CutsceneNameIndex.GetGuid(name);
            if (key == null)
            {
                Debug.LogError(
                    $"[PlayCutsceneCommand] Записи LogicChain с именем '{name}' нет — команда пропущена.");
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
