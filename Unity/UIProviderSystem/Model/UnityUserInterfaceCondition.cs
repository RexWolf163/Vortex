using System;
using Sirenix.OdinInspector;
using Vortex.Core.UIProviderSystem.Model;

namespace Vortex.Unity.UIProviderSystem.Model
{
    /// <summary>
    /// Прокладка с сахаром, для подсветки названия при отсутствии прочих параметров,
    /// чтобы отображаемый список не схлопывался
    /// </summary>
    [Serializable]
    public abstract class UnityUserInterfaceCondition : UserInterfaceCondition
    {
        [DisplayAsString, ShowInInspector, HideLabel, PropertyOrder(-100)]
        private string Name => GetType().Name;
    }
}