using System;
using Sirenix.OdinInspector;
using UnityEngine;
using Vortex.Core.MaskSystem;
using Vortex.Core.NetworkSystem;
using Vortex.Unity.MaskSystem;

namespace Vortex.Unity.NetworkSystem
{
    /// <summary>Хост, которому разрешён http.</summary>
    [Serializable]
    public class HttpAllowEntry
    {
        [SerializeReference, MaskSelector, HideLabel]
        public Mask mask;
    }

    /// <summary>Исключение «маска URL → провайдер тела».</summary>
    [Serializable]
    public class BodyProviderRule
    {
        [SerializeReference, MaskSelector]
        public Mask mask;

        [SerializeReference]
        public IBodyProvider provider;
    }

    /// <summary>Правило белого списка авторизации «маска URL → провайдер авторизации».</summary>
    [Serializable]
    public class AuthRule
    {
        [SerializeReference, MaskSelector]
        public Mask mask;

        [SerializeReference]
        public IAuthProvider provider;
    }
}
