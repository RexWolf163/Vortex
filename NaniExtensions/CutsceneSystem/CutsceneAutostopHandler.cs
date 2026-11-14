using UnityEngine;
using Vortex.NaniExtensions.Core;
using Vortex.Unity.CutsceneSystem.Bus;

namespace Vortex.NaniExtensions.CutsceneSystem
{
    /// <summary>
    /// Аварийный останов катсцены по остановке сценария Naninovel. Ставится на сцену — на объект, который
    /// включён всё время, пока на сцене могут идти катсцены (обычно на объект оркестратора).
    ///
    /// Хэндлер держит защёлку «катсцена открыта»: событие шины «катсцена открывается» её открывает,
    /// «показ завершён» — закрывает. Сценарий остановился при открытой защёлке — катсцена прерывается, как по
    /// команде <c>@cutscene_Stop</c>: идущая пролетает до конца, загружаемая отменяется. Защёлка закрыта —
    /// хэндлер ничего не делает.
    ///
    /// Штатно катсцену закрывает её цепочка либо команда сценария. Хэндлер — страховка на случай, когда
    /// сценарий оборвался раньше (выход в меню, загрузка сохранения, ошибка в сценарии) и закрыть катсцену
    /// стало некому
    /// </summary>
    public class CutsceneAutostopHandler : MonoBehaviour
    {
        /// <summary>
        /// Катсцена открыта: загружается либо идёт
        /// </summary>
        private bool _open;

        private void OnEnable()
        {
            CutsceneBus.OnShowOpening += OnOpening;
            CutsceneBus.OnShowFinished += OnFinished;
            NaniWrapper.OnNaniStop += OnNaniStop;
        }

        private void OnDisable()
        {
            CutsceneBus.OnShowOpening -= OnOpening;
            CutsceneBus.OnShowFinished -= OnFinished;
            NaniWrapper.OnNaniStop -= OnNaniStop;

            //Выключенный хэндлер событий не слышит: положение защёлки после включения было бы случайным
            _open = false;
        }

        private void OnOpening(string key) => _open = true;

        private void OnFinished(string key) => _open = false;

        private void OnNaniStop()
        {
            if (_open)
                CutsceneBus.Skip();
        }
    }
}
