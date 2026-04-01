using Sirenix.OdinInspector;
using UnityEngine;
using Vortex.Unity.AppSystem.System.TimeSystem;

namespace Vortex.Unity.UI.TweenerSystem
{
    /// <summary>
    /// Узел-контроллер управления твинерами
    ///
    /// На Awake, все твиннеры сбрасывются в нулевое состояние.
    /// Если управляющий запрос приходит без skip в момент, когда хаб отключен,
    /// то анимация начнется только после включения
    /// </summary>
    public class TweenerHub : MonoBehaviour
    {
        [InfoBox("ВАЖНО! Твиннер не должен управлять RectTransform на котором находится!", InfoMessageType.Warning)]
        [SerializeReference]
        private TweenLogic[] tweeners = new TweenLogic[0];

        private bool _isForward = false;

        private void Awake()
        {
            foreach (var tweener in tweeners)
                tweener.Init();
        }

        private void OnEnable()
        {
            if (_isForward)
                Forward();
            else
                Back(true);
        }

        private void OnDisable()
        {
            TimeController.RemoveCall(this);
        }

        private void OnDestroy()
        {
            foreach (var tweener in tweeners)
                tweener.DeInit();
        }

        public void Forward(bool skip = false)
        {
#if UNITY_EDITOR
            if (!Application.isPlaying) skip = true;
#endif
            _isForward = true;
            if (skip)
                CallForward(true);
            else if (isActiveAndEnabled)
                TimeController.Accumulate(CallForward, this);
        }

        public void Back(bool skip = false)
        {
#if UNITY_EDITOR
            if (!Application.isPlaying) skip = true;
#endif
            _isForward = false;

            if (skip)
                CallBack(true);
            else if (isActiveAndEnabled)
                TimeController.Accumulate(CallBack, this);
        }

        public void Pulse()
        {
            if (!isActiveAndEnabled)
                return;
            if (!_isForward)
                TimeController.Accumulate(CallPulse, this);
            else
                Back();
        }

        private void CallBack() => CallBack(false);

        private void CallBack(bool skip)
        {
            foreach (var tweener in tweeners)
                tweener.Back(skip);
        }

        private void CallForward() => CallForward(false);

        private void CallForward(bool skip)
        {
            foreach (var tweener in tweeners)
                tweener.Forward(skip);
        }

        private void CallPulse()
        {
            foreach (var tweener in tweeners)
                tweener.Pulse();
        }
    }
}