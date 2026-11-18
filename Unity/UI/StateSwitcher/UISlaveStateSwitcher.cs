using Sirenix.OdinInspector;
using UnityEngine;

namespace Vortex.Unity.UI.StateSwitcher
{
    /// <summary>
    /// Ведомый свитчер: повторяет номер состояния мастер-свитчера. При включении принимает текущее состояние
    /// мастера и подписывается на его смену; при выключении отписывается. Состояния сопоставляются по индексу —
    /// их число и имена должны совпадать с мастером (кнопка «Синхронизировать с мастером», редактор)
    /// </summary>
    [HideMonoScript]
    public class UISlaveStateSwitcher : UIStateSwitcher
    {
        [SerializeField, Required, PropertyOrder(-10), InfoBox("Мастер-свитчер: его состояние повторяется по индексу")]
        private UIStateSwitcher master;

        public UIStateSwitcher Master => master;

        private void OnEnable()
        {
            master.OnStateSwitch += Follow;
            Set(master.State);
        }

        private void OnDisable() => master.OnStateSwitch -= Follow;

        private void Follow(StateData _) => Set(master.State);

#if UNITY_EDITOR
        /// <summary>
        /// Число и имена состояний — как у мастера, по индексу. Недостающие добавляются пустыми, элементы уже
        /// настроенных не трогаются. Лишние не удаляются — ошибка в лог
        /// </summary>
        [Button("Синхронизировать с мастером"), PropertyOrder(-9), ShowIf("@master != null")]
        private void SyncWithMaster()
        {
            var source = master.States;
            var so = new UnityEditor.SerializedObject(this);
            var states = so.FindProperty("states");

            for (var i = states.arraySize; i < source.Length; i++)
            {
                states.InsertArrayElementAtIndex(i); // копия последнего — элементы чистим
                states.GetArrayElementAtIndex(i).FindPropertyRelative("stateItems").arraySize = 0;
            }

            for (var i = 0; i < source.Length; i++)
                states.GetArrayElementAtIndex(i).FindPropertyRelative("name").stringValue = source[i].Name;

            so.ApplyModifiedProperties();

            if (states.arraySize > source.Length)
                Debug.LogError($"[UISlaveStateSwitcher] {name}: лишние состояния ({states.arraySize} при " +
                               $"{source.Length} у мастера) — удалите вручную", this);
        }

        protected override void OnValidate()
        {
            base.OnValidate();
            if (master == null)
                return;

            if (master == this)
            {
                Debug.LogError($"[UISlaveStateSwitcher] {name}: мастер — сам свитчер", this);
                return;
            }

            var own = States;
            var source = master.States;
            var mismatch = own.Length != source.Length;
            for (var i = 0; !mismatch && i < own.Length; i++)
                mismatch = own[i].Name != source[i].Name;
            if (mismatch)
                Debug.LogError($"[UISlaveStateSwitcher] {name}: состояния не совпадают с мастером " +
                               $"«{master.name}» — нажмите «Синхронизировать с мастером»", this);
        }
#endif
    }
}
