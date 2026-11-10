using Cysharp.Threading.Tasks;

namespace Vortex.Unity.AssetSwapSystem
{
    /// <summary>
    /// Пауза между шагами тяжёлых операций. В обычном режиме — <c>UniTask.Yield()</c>: редактор не подвисает,
    /// progress bar и лог обновляются. В синхронном режиме (<see cref="Synchronous"/>, его включают sync-обёртки
    /// <see cref="AssetSwapController"/>) — уже завершённая задача: async-метод проходит до конца без возврата в
    /// цикл кадра, и <c>GetResult()</c> отдаёт результат. Без этого <c>GetResult()</c> на незавершённой UniTask
    /// бросает «Not yet completed» — UniTask не умеет блокирующего ожидания.
    /// </summary>
    internal static class AssetSwapPause
    {
        /// <summary>Шаги выполняются подряд, без пауз. Выставляется только на время sync-вызова.</summary>
        internal static bool Synchronous;

        internal static UniTask Next() => Synchronous ? UniTask.CompletedTask : UniTask.Yield().ToUniTask();
    }
}
