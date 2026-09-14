using UnityEngine;

namespace Vortex.Unity.DebugSystem.Handlers
{
    /// <summary>
    /// Отладочный хэндлер: пишет в лог фазы жизненного цикла объекта — Awake, OnEnable, OnDisable, OnDestroy.
    /// В записи — имя, путь в иерархии и InstanceID (у элементов пула имена совпадают) и номер кадра, чтобы
    /// видеть порядок фаз и выключение-включение в одном кадре. Клик по записи в консоли подсвечивает объект.
    /// </summary>
    public class DebugLifecycleLogHandler : MonoBehaviour
    {
        private void Awake() => Print("Awake");

        private void OnEnable() => Print("OnEnable");

        private void OnDisable() => Print("OnDisable");

        private void OnDestroy() => Print("OnDestroy");

        private void Print(string phase) =>
            Debug.Log($"[Lifecycle] {phase} — {name} #{gameObject.GetInstanceID()} ({Path()}) frame {Time.frameCount}",
                this);

        private string Path()
        {
            var path = name;
            for (var parent = transform.parent; parent != null; parent = parent.parent)
                path = $"{parent.name}/{path}";
            return path;
        }
    }
}
