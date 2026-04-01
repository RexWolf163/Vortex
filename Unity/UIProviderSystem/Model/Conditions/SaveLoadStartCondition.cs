using System;
using Vortex.Core.SaveSystem.Bus;
using Vortex.Core.SaveSystem.Model;
using Vortex.Core.UIProviderSystem.Model;

namespace Vortex.Unity.UIProviderSystem.Model.Conditions
{
    /// <summary>
    /// Условие: Показывать пока идет процесс загрузки или сохранения
    /// (источник событий SaveController)
    /// </summary>
    [Serializable]
    public sealed class SaveLoadStartCondition : UnityUserInterfaceCondition
    {
        protected override void Run()
        {
            SaveController.OnLoadStart += RunCallback;
            SaveController.OnLoadComplete += RunCallback;
            SaveController.OnSaveStart += RunCallback;
            SaveController.OnSaveComplete += RunCallback;
        }

        public override void DeInit()
        {
            SaveController.OnLoadStart -= RunCallback;
            SaveController.OnLoadComplete -= RunCallback;
            SaveController.OnSaveStart -= RunCallback;
            SaveController.OnSaveComplete -= RunCallback;
        }

        public override ConditionAnswer Check() => SaveController.State != SaveControllerStates.Idle
            ? ConditionAnswer.Open
            : ConditionAnswer.Close;
    }
}