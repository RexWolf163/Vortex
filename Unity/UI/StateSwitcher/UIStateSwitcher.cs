using System;
using System.Collections.Generic;
using System.Linq;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Serialization;
using Vortex.Unity.EditorTools.Attributes;

namespace Vortex.Unity.UI.StateSwitcher
{
    /// <summary>
    /// Контроллер переключения состояний UI 
    /// </summary>
    [HideMonoScript]
    public class UIStateSwitcher : MonoBehaviour
    {
        [OnInspectorGUI("OnInspectorGUI")]

        #region inner Classes

        [ClassLabel("$name")]
        [Serializable, HideReferenceObjectPicker]
        public class StateData : IDisposable
        {
            [FormerlySerializedAs("_name")]
            [HorizontalGroup("top", 0.8f)]
            [SerializeField, Sirenix.OdinInspector.HideLabel]
            [Sirenix.OdinInspector.LabelText("Состояние")]
            private string name;

            [FormerlySerializedAs("_stateItems")]
            [SerializeReference]
            [Sirenix.OdinInspector.LabelText("Элементы")]
            [HideReferenceObjectPicker]
            [ValueDropdown("$GetItems", AppendNextDrawer = true)]
            private StateItem[] stateItems = { };


            public void Set()
            {
                if (stateItems == null) return;
                foreach (var stateItem in stateItems)
                    stateItem.Set();
            }

            public void DefaultState()
            {
                if (stateItems == null) return;
                foreach (var stateItem in stateItems)
                {
                    stateItem.DefaultState();
                }
            }

            public void Dispose()
            {
                if (stateItems == null) return;
                foreach (var stateItem in stateItems)
                {
                    stateItem.Dispose();
                }
            }

            public string Name => name;
            public StateItem[] StateItems => stateItems;

#if UNITY_EDITOR
            internal void AddStateItem(StateItem stateItem)
            {
                var list = stateItems.ToList();
                list.Add(stateItem);
                stateItems = list.ToArray();
            }

            private static ValueDropdownList<StateItem> _cachedItems;

            [UnityEditor.Callbacks.DidReloadScripts]
            private static void ResetCache() => _cachedItems = null;

            private ValueDropdownList<StateItem> GetItems()
            {
                if (_cachedItems != null) return _cachedItems;

                _cachedItems = new ValueDropdownList<StateItem>();
                var checkList = new List<string>();
                foreach (var type in GetDerivedTypes<StateItem>())
                {
                    var instance = (StateItem)Activator.CreateInstance(type);
                    var index = $"{instance.DropDownGroupName}/{instance.DropDownItemName}";
                    if (checkList.Contains(index))
                    {
                        Debug.LogError($"{type.Name} has duplicate {index} from other state logic.");
                        continue;
                    }

                    _cachedItems.Add(index, (StateItem)Activator.CreateInstance(type));
                    checkList.Add(index);
                }

                return _cachedItems;
            }

            public static IEnumerable<Type> GetDerivedTypes<T>() where T : StateItem
            {
                Type baseType = typeof(T);

                return AppDomain.CurrentDomain.GetAssemblies().SelectMany(a => a.GetTypes())
                    .Where(type => baseType.IsAssignableFrom(type) && type != baseType);
            }

            internal UIStateSwitcher owner;

            [HorizontalGroup("top", 0.2f), GUIColor("@Color.green")]
            [Button]
            private void SetSwitcher() => owner.Set(this);
#endif
        }

        #endregion

        #region Params

        [SerializeField] [ValueDropdown("$GetDropDownStatesList")]
        private int stateOnEnable;

#if UNITY_EDITOR
        [FormerlySerializedAs("_duplicateOnCreate")] [Sirenix.OdinInspector.LabelText("Дублировать")] [SerializeField]
        private bool duplicateOnCreate = true;
#endif
        [FormerlySerializedAs("_states")]
        [Sirenix.OdinInspector.LabelText("Состояния")]
        [ListDrawerSettings(OnBeginListElementGUI = "OnBeginListElementGUI",
            OnEndListElementGUI = "OnEndListElementGUI", CustomAddFunction = "CustomAddFunction")]
        [SerializeField]
        private StateData[] states = Array.Empty<StateData>();

        public StateData[] States => states;

        private int _state = -1;

        /// <summary>
        /// Служебная переменная. Хранит путь до слоя на случай исключений связанных с удалением объекта
        /// </summary>
        private string _goName;

        /// <summary>
        /// Флаг защиты от реентрантного вызова
        /// </summary>
        private bool _isSwitching;

        /// <summary>
        /// Положение которое нужно принять если запрос прошел до обработки Awake
        /// </summary>
        private int _startState = -1;

        /// <summary>
        /// Свитчер был инициирован
        /// </summary>
        private bool _wasInitialized;

        public int State
        {
            get => _state;
            private set
            {
                if (states.Length == 0 || value == _state)
                    return;
                if (_isSwitching)
                {
                    Debug.LogError(
                        $"[UIStateSwitcher] Реентрантный вызов Set({value}) во время переключения состояния");
                    return;
                }

                _isSwitching = true;
                try
                {
                    // Проверка на выход за границы массива
                    if (value < -1 || value >= states.Length)
                    {
                        Debug.LogError($"[UIStateSwitcher] Попытка установить некорректный индекс состояния: {value}");
                        return;
                    }
#if UNITY_EDITOR
                    if (Application.isPlaying)
                    {
#endif
                        if (!_wasInitialized)
                        {
                            _startState = value;
                            return;
                        }
#if UNITY_EDITOR
                    }
                    else
                    {
                        foreach (var state in states)
                            state.DefaultState();
                    }
#endif
                    //foreach (var state in _states)
                    if (_state >= 0 && _state < states.Length)
                        states[_state].DefaultState();

                    if (value != -1)
                        states[value].Set();

                    _state = value;
                    OnStateSwitch?.Invoke(value == -1 ? null : States[value]);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"{_goName}: {ex}");
                }
                finally
                {
                    _isSwitching = false;
                }
            }
        }

        /// <summary>
        /// Ивент на смену состояния. Передаётся текущее состояние
        /// </summary>
        public event Action<StateData> OnStateSwitch;

        #endregion

        #region Public

        /// <summary>
        /// Сброс состояний свитчера и переключения его в позицию на старте
        /// </summary>
        [Button]
        public void ResetStates()
        {
            if (states.Length == 0)
                return;
            foreach (var state in states)
                state.DefaultState();

            State = -1;
            Set(_startState >= 0 ? _startState : stateOnEnable);
            _startState = -1;
        }

        /// <summary>
        /// Возвращает результат поиска состояния с указанным названием в виде номера состояния
        /// </summary>
        /// <param name="state"></param>
        /// <returns></returns>
        public int GetState(string state)
        {
            for (var i = 0; i < states.Length; i++)
                if (states[i].Name == state)
                    return i;

            return -1;
        }

        /// <summary>
        /// Выставление указанного состояния
        /// </summary>
        /// <param name="state">состояниe в виде строкового названия, указанного в <see cref="StateData"/></param>
        public void Set(string state)
        {
            for (var i = 0; i < states.Length; i++)
            {
                if (states[i].Name == state)
                {
                    Set(i);
                    return;
                }
            }

            Debug.LogError($"[UIStateSwitcher] В '{gameObject.name}' отсутствует состояние '{state}'");
        }

        /// <summary>
        /// Выставление указанного состояния
        /// </summary>
        /// <param name="state">состояниe в виде элемента перечисления. Используется Хэш код параметра</param>
        public void Set(Enum state) => Set(Convert.ToInt32(state));


        /// <summary>
        /// Выставление указанного состояния
        /// </summary>
        /// <param name="stateNumber">номер состояния в списке</param>
        public void Set(byte stateNumber) => Set((int)stateNumber);

        /// <summary>
        /// Выставление указанного состояния
        /// </summary>
        /// <param name="stateNumber">номер состояния в списке</param>
        public void Set(int stateNumber)
        {
            State = stateNumber;
        }

        public ValueDropdownList<int> GetDropDownStatesList()
        {
            var list = new ValueDropdownList<int>();
            for (var i = 0; i < states.Length; i++)
            {
                list.Add(states[i].Name, i);
            }

            return list;
        }

        #endregion

        #region Private

#if UNITY_EDITOR

        private void OnBeginListElementGUI(int index)
        {
            if (index == State)
                GUI.color = new Color(0.7f, 1f, 0.7f);
        }

        private void OnEndListElementGUI(int index)
        {
            GUI.color = Color.white;
        }

        private StateData CustomAddFunction()
        {
            var stateData = new StateData();
            if (!duplicateOnCreate || states.Length == 0) return stateData;
            var lastStateData = states.Last();
            foreach (var stateItem in lastStateData.StateItems)
            {
                stateData.AddStateItem(stateItem.Clone());
            }

            return stateData;
        }

        private void Set(StateData stateData)
        {
            for (var i = states.Length - 1; i >= 0; i--)
            {
                var state = states[i];
                if (state != stateData) continue;
                State = i;
                return;
            }
        }
#endif

        private void Awake()
        {
            _goName = name;
            var parent = transform.parent;
            while (parent != null)
            {
                _goName = parent.name + "/" + _goName;
                parent = parent.parent;
            }

            _wasInitialized = true;
            ResetStates();
        }

        private void OnDestroy()
        {
            if (states.Length == 0)
                return;
            foreach (var state in states)
                state.Dispose();
        }

#if UNITY_EDITOR
        private void OnInspectorGUI()
        {
            foreach (var state in states)
                state.owner = this;
        }
#endif

        #endregion
    }
}