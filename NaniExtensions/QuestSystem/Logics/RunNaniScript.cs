using System;
using System.Threading;
using Naninovel;
using UnityEngine;
using Vortex.NaniExtensions.Core;
using Vortex.NaniExtensions.NaniScriptLinks;
using Vortex.Sdk.Quests.QuestsLogics;

namespace Vortex.NaniExtensions.QuestSystem.Logics
{
    [Serializable]
    public class RunNaniScript : QuestLogic, INaniScriptLaunch
    {
        [SerializeField] private Script naninovelScript;

        /// <summary>Проигрываемый скрипт — нужен снаружи (напр. привязка подсказки к квесту по его логике).</summary>
        public Script NaniScript => naninovelScript;

        /// <summary>Путь запускаемого сценария для валидатора связей (Nani Script Links); не задан — null</summary>
        string INaniScriptLaunch.ScriptPath => naninovelScript != null ? naninovelScript.Path : null;

        private IChoiceHandlerManager _choiceManager;

        private bool _playing;

        // Путь скрипта, зафиксированный при первом ЖИВОМ доступе. Naninovel грузит скрипт ПО ПУТИ (LoadAndPlay)
        // и по ходу проигрывания выгружает сам ассет своим resource-provider'ом; сериализованная ссылка
        // naninovelScript указывает на ТОТ ЖЕ ассет и после выгрузки становится Unity fake-null (уничтоженный
        // объект). У зацикленного квеста (UnFailable + AlwaysFail) на очередном витке ссылка уже мертва →
        // naninovelScript.Path читается как null. Поэтому не полагаемся на ассет: фиксируем путь, пока ссылка
        // жива, и грузим по строке — Naninovel перезагрузит скрипт по пути сам.
        private string _scriptPath;

        private IChoiceHandlerManager ChoiceManager =>
            _choiceManager ??= Engine.GetServiceOrErr<IChoiceHandlerManager>();

        private string ScriptPath
        {
            get
            {
                if (string.IsNullOrEmpty(_scriptPath) && naninovelScript?.Path != null)
                    _scriptPath = naninovelScript.Path;
                return _scriptPath;
            }
        }

        public override async Cysharp.Threading.Tasks.UniTask<bool> Run(CancellationToken token)
        {
            while (NaniWrapper.NaniIsPlaying())
                await Cysharp.Threading.Tasks.UniTask.Yield();
            if (token.IsCancellationRequested)
                return true;
            NaniWrapper.ResetNani();

            var path = ScriptPath;
            if (string.IsNullOrEmpty(path))
            {
                // Пусто только если ассет не задан ИЛИ был выгружен ещё до первого запуска. В спин-луп не уходим.
                Debug.LogError(
                    $"{nameof(RunNaniScript)}: путь скрипта пуст — ассет не задан либо выгружен до первого запуска.");
                return true;
            }

            if (token.IsCancellationRequested)
                return true;

            // Через шлюз NaniWrapper, а не напрямую LoadAndPlay: между Load и Play есть асинхронное окно,
            // в котором плеер ещё не Playing. Параллельный запуск (другой квест, катсцена галереи) успевал
            // войти в него и своим UnloadAll() освободить уже подготовленный ассет — отсюда падение
            // «Failed to get '<path>' resource of type 'Naninovel.Script': not loaded» на Play.
            await NaniWrapper.PlayScript(path, token: token);
            return true;
        }

#if UNITY_EDITOR
        protected override string GetEditorLabel() => $"Run NaniScript {naninovelScript?.Path ?? "[NOT SET]"}";
#endif
    }
}