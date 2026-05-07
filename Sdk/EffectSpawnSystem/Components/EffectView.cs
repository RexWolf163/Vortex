using UnityEngine;
using Vortex.Sdk.EffectSpawnSystem.Bus;
using Vortex.Unity.UI.TweenerSystem;

namespace Vortex.Sdk.EffectSpawnSystem.Components
{
    /// <summary>
    /// Компонент на корне префаба эффекта.
    /// Управляет жизнью одного активного цикла:
    ///  • <see cref="OnEnable"/> запускает <see cref="TweenerHub.Forward"/>, фиксирует <see cref="_spawnTime"/>;
    ///  • <see cref="Update"/> отсчитывает <see cref="duration"/> в unscaled-времени и вызывает <see cref="Release"/>;
    ///  • <see cref="OnDisable"/> мгновенно сбрасывает анимацию через <c>tweenerHub.Back(skip: true)</c>.
    ///
    /// Активация / деактивация управляются переносом между активным parent (target) и неактивным <c>Storage</c>
    /// в <see cref="Pool.EffectPool"/> — никаких ручных <c>SetActive</c>.
    /// </summary>
    [DisallowMultipleComponent, RequireComponent(typeof(TweenerHub))]
    public class EffectView : MonoBehaviour
    {
        [SerializeField, Tooltip("Длительность активного цикла в секундах (unscaled-время).")]
        private float duration = 1.0f;

        [SerializeField, Tooltip("TweenerHub-источник анимации эффекта. Заполняется автоматически в OnValidate.")]
        private TweenerHub tweenerHub;

        private float _spawnTime;
        private float _pausedAccum;
        private bool _paused;

        public float Duration => duration;

        private void OnValidate()
        {
            if (tweenerHub == null) tweenerHub = GetComponent<TweenerHub>();
        }

        private void OnEnable()
        {
            _spawnTime = Time.unscaledTime;
            _pausedAccum = 0f;
            _paused = EffectSpawn.IsPaused;

            if (tweenerHub != null) tweenerHub.Forward();
        }

        private void OnDisable()
        {
            // Возврат в Storage (parent inactive) — мгновенный сброс анимации в исходное состояние.
            if (tweenerHub != null) tweenerHub.Back(skip: true);
        }

        private void Update()
        {
            if (_paused)
            {
                _pausedAccum += Time.unscaledDeltaTime;
                return;
            }

            if (Time.unscaledTime - _spawnTime - _pausedAccum >= duration)
                Release();
        }

        /// <summary>
        /// Досрочный возврат эффекта в пул. Идемпотентен в пределах одного активного цикла.
        /// </summary>
        public void Release()
        {
            EffectSpawn.Release(this);
        }

        // Вызывается шиной при изменении состояния игры (Pause/Resume).
        // TODO: заморозка TweenerHub'а на паузе — отдельная задача (нужен Pause/Resume в TweenerHub).
        internal void OnPause() => _paused = true;
        internal void OnResume() => _paused = false;
    }
}