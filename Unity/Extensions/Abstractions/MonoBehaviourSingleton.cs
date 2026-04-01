using UnityEngine;
using Vortex.Unity.AppSystem.System.TimeSystem;

namespace Vortex.Unity.Extensions.Abstractions
{
    public class MonoBehaviourSingleton<T> : MonoBehaviour where T : MonoBehaviourSingleton<T>
    {
        private static T _instance;

        protected static T Instance
        {
            get
            {
                if (_instance == null)
                    _instance = FindAnyObjectByType<T>();
                return _instance;
            }
        }

        protected virtual void Awake()
        {
#if UNITY_EDITOR
            TimeController.Call(SetInstance, 0, this);
#else
            SetInstance();
#endif
        }

        protected virtual void OnDestroy()
        {
            _instance = null;
            TimeController.RemoveCall(this);
        }

        private void SetInstance()
        {
            if (_instance == this)
                return;
            if (_instance != null)
                Debug.LogError($"[{GetType().Name}: {name}] Singleton already created!");
            _instance = (T)this;
        }
    }
}