using System;
using System.Linq;
using System.Threading;
using Naninovel;
using Sirenix.OdinInspector;
using UnityEngine;
using Vortex.Core.Extensions.LogicExtensions;
using Vortex.NaniExtensions.Core;
using Vortex.Sdk.Quests.QuestsLogics;

namespace Vortex.NaniExtensions.QuestSystem.Logics
{
    [Serializable]
    public class WaitStopNaniScript : QuestLogic
    {
        [InfoBox(
            "Если поле пустое - остановка текущего скрипта. Иначе проверяет запущен ли указанный скрипт и если нет - выполняется сразу, если да - ждет остановки")]
        [SerializeField]
        private Script naninovelScript;

        private bool _isRun;

        private IChoiceHandlerManager _choiceManager;

        // См. RunNaniScript: сериализованная ссылка на Script после выгрузки ассета Naninovel'ом становится
        // Unity fake-null. Тогда naninovelScript == null ложно читалось бы как «поле пустое», ожидание останова
        // пропускалось бы и квест улетал в цикл. «Пусто/задано» решаем по зафиксированному пути, а не по ссылке.
        private string _scriptPath;

        private IChoiceHandlerManager ChoiceManager =>
            _choiceManager ??= Engine.GetServiceOrErr<IChoiceHandlerManager>();

        private string ScriptPath
        {
            get
            {
                if (string.IsNullOrEmpty(_scriptPath) && naninovelScript != null)
                    _scriptPath = naninovelScript.Path;
                return _scriptPath;
            }
        }

        public override async Cysharp.Threading.Tasks.UniTask<bool> Run(CancellationToken token)
        {
            var player = NaniWrapper.ScriptPlayer;
            // «Поле пустое» — по зафиксированному пути, а не по ссылке (та могла стать fake-null).
            if (string.IsNullOrEmpty(ScriptPath) && player.PlayedScript != null)
                return true;

            NaniWrapper.OnNaniStop += OnStopped;
            _isRun = NaniWrapper.NaniIsPlaying();
            try
            {
                while (_isRun)
                {
                    await UniTask.Delay(100, cancellationToken: token);
                }

                return true;
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
            finally
            {
                _isRun = false;
                NaniWrapper.OnNaniStop -= OnStopped;
            }

            return true;
        }

        private void OnStopped() => _isRun = NaniWrapper.NaniIsPlaying();

#if UNITY_EDITOR
        protected override string GetEditorLabel() =>
            $"Wait Stopping NaniScript {naninovelScript?.Path ?? "[NOT SET]"}";
#endif
    }
}
