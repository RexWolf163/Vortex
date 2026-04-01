using UnityEngine;
using Vortex.Unity.AppSystem.System.TimeSystem;

namespace Vortex.Unity.UI.Misc
{
    /// <summary>
    /// Хэндлер для отложенной активации дочерних слоев
    /// </summary>
    public class EnableDelayForChild : MonoBehaviour
    {
        [SerializeField, Range(0, 10f)] private float delay = 1f;

        private Transform[] _childs;

        private void Awake()
        {
            var count = transform.childCount;
            _childs = new Transform[count];
            for (var i = 0; i < count; i++)
            {
                _childs[i] = transform.GetChild(i);
                _childs[i].gameObject.SetActive(false);
            }
        }

        private void OnEnable() => TimeController.Call(() => SwitchChild(true), delay, this);

        private void OnDisable()
        {
            SwitchChild(false);
            TimeController.RemoveCall(this);
        }

        private void SwitchChild(bool b)
        {
            foreach (var child in _childs)
                child.gameObject.SetActive(b);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            Awake();
            SwitchChild(false);
        }
#endif
    }
}