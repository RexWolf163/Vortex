using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using Vortex.Core.SettingsSystem.Bus;
using Vortex.Unity.EditorTools.Attributes;
using Vortex.Unity.UI.Misc;

namespace Vortex.Unity.InputBusSystem.Handlers
{
    /// <summary>
    /// Хэндлер подписок на клавиши управления
    /// </summary>
    public class KeyboardHandler : MonoBehaviour
    {
        [Serializable]
        [ClassLabel("$Label")]
        public struct KeyCombination
        {
            [SerializeField, HideLabel] private Key[] keys;
            public Key[] Keys => keys;

            private string Label() => keys.Length > 0 ? string.Join('+', keys) : "[EMPTY]";
        }

        [SerializeField] private AdvancedButton button;
        [SerializeField] private UnityEvent onPressed;
        [SerializeField] private UnityEvent onReleased;

        [InfoBubble("Отдельные клавиши вызывающие активацию логики")] [SerializeField, VortexCollection]
        private Key[] buttonCode;

        [InfoBubble(
            "Комбинации клавиш вызывающие активацию логики. Активирующей считается только последняя. Остальные - модификаторы")]
        [SerializeField, VortexCollection]
        private KeyCombination[] buttonsCombinations;

        private InputAction _inputAction;

        private void Awake()
        {
            if (Keyboard.current == null)
                Debug.LogWarning("[KeyboardHandler] Keyboard.current is null. Can't work correctly.");

            _inputAction = new InputAction(
                name: $"ButtonHandler KeyGroup «{name} ({string.Join(";", buttonCode)})»",
                type: InputActionType.Button
            );

            foreach (var code in buttonCode)
                _inputAction.AddBinding($"<Keyboard>/{code}");

            foreach (var combination in buttonsCombinations)
            {
                if (combination.Keys.Length < 2) continue;

                var modifierCount = combination.Keys.Length - 1;

                var compositeType = modifierCount switch
                {
                    1 => "OneModifier",
                    2 => "TwoModifiers",
                    3 => "ThreeModifiers",
                    _ => null
                };

                if (compositeType == null)
                {
                    Debug.LogWarning($"[KeyboardHandler] Unsupported modifier count: {modifierCount}");
                    continue;
                }

                var bind = _inputAction.AddCompositeBinding(compositeType);
                for (var i = 0; i < combination.Keys.Length - 1; i++)
                {
                    var code = combination.Keys[i];
                    var partName = modifierCount == 1
                        ? "modifier"
                        : $"modifier{i + 1}";
                    bind = bind.With(partName, $"<Keyboard>/{code}");
                }

                bind.With("binding", $"<Keyboard>/{combination.Keys[^1]}");
            }

            _inputAction.started += OnPressed;
            _inputAction.canceled += OnCanceled;
        }

        private void OnDestroy()
        {
            _inputAction.started -= OnPressed;
            _inputAction.canceled -= OnCanceled;
            _inputAction?.Dispose();
        }

        private void OnEnable() => _inputAction.Enable();

        private void OnDisable() => _inputAction.Disable();

        private void OnPressed(InputAction.CallbackContext ctx)
        {
            if (Keyboard.current == null)
                return;

            if (Settings.Data().DebugMode)
                Debug.Log($"[KeyboardHandler] {ctx.control.name} was pressed");

            button?.Press();
            onPressed?.Invoke();
        }

        private void OnCanceled(InputAction.CallbackContext ctx)
        {
            if (Settings.Data().DebugMode)
                Debug.Log($"[KeyboardHandler] {ctx.control.name} was released");
            button?.Release();
            onReleased?.Invoke();
        }
    }
}