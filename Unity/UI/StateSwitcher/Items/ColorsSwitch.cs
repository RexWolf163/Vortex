using System;
using System.Linq;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.UI;
using Vortex.Unity.UI.TweenerSystem.UniTaskTweener;
using Object = UnityEngine.Object;

namespace Vortex.Unity.UI.StateSwitcher.Items
{
    [Serializable]
    public class ColorsSwitch : StateItem
    {
        [HorizontalGroup("h1")] [HideLabel] [SerializeField]
        private Color _color = Color.white;

        [HorizontalGroup("h1")] [HideLabel] [SerializeField] [OnValueChanged("CheckObject")]
        private Object[] _objects = { };

        [ToggleGroup("_smoothChange", "Smooth change")] [SerializeField]
        private bool _smoothChange;

        [ToggleGroup("_smoothChange", "Smooth change")] [SerializeField]
        private float _duration;

        [ToggleGroup("_smoothChange", "Smooth change")] [SerializeField]
        private AnimationCurve _curve;

        [ToggleGroup("_smoothChange", "Smooth change")] [SerializeField]
        private bool _useOwnStartColor;

        [HideIf("$_useOwnStartColor")] [ToggleGroup("_smoothChange", "Smooth change")] [SerializeField]
        private Color _startColor = Color.white;


        private AsyncTween _tween = new();

        public override void Set()
        {
            _tween ??= new AsyncTween();
            SetColor(_color);
        }

        private void SetColor(Color color)
        {
            for (var i = 0; i < _objects.Length; i++)
            {
                SetColor(i, color);
            }
        }

        private void SetColor(int index, Color color)
        {
            var @object = _objects[index];
            switch (@object)
            {
                case SpriteRenderer sprRend:
                    if (_smoothChange && Application.isPlaying)
                    {
                        var startColor = _useOwnStartColor ? sprRend.color : _startColor;
                        var valueToTween = 0f;
                        _tween.Set(() => valueToTween, x => valueToTween = x, 1, _duration)
                            .OnUpdate((f) => sprRend.color = Color.Lerp(startColor, color, _curve.Evaluate(f)))
                            .Run();
                    }
                    else
                        sprRend.color = color;

                    break;
                case Graphic img:
                    if (_smoothChange && Application.isPlaying)
                    {
                        var startColor = _useOwnStartColor ? img.color : _startColor;
                        var valueToTween = 0f;
                        _tween.Set(() => valueToTween, x => valueToTween = x, 1, _duration)
                            .OnUpdate((f) => img.color = Color.Lerp(startColor, color, _curve.Evaluate(f)))
                            .Run();
                    }
                    else
                        img.color = color;

                    break;
                case Outline outline:
                    if (_smoothChange && Application.isPlaying)
                    {
                        var startColor = _useOwnStartColor ? outline.effectColor : _startColor;
                        var valueToTween = 0f;
                        _tween.Set(() => valueToTween, x => valueToTween = x, 1, _duration)
                            .OnUpdate((f) => outline.effectColor = Color.Lerp(startColor, color, _curve.Evaluate(f)))
                            .Run();
                    }
                    else
                        outline.effectColor = color;

                    break;
            }
        }

        public override void DefaultState()
        {
            SetColor(Color.white);
        }

#if UNITY_EDITOR
        public override StateItem Clone()
        {
            var clone = new ColorsSwitch
            {
                _color = _color,
                _duration = _duration,
                _objects = _objects.ToArray(),
                _smoothChange = _smoothChange,
                _curve = _curve,
                _useOwnStartColor = _useOwnStartColor,
                _startColor = _startColor
            };
            return clone;
        }

        [ShowIf("@_objects != null && _objects.Length > 0")]
        [Button("Get from...")]
        [GUIColor("@Color.green")]
        [HorizontalGroup("h1")]
        private void SetCurrent()
        {
            switch (_objects.FirstOrDefault())
            {
                case SpriteRenderer sprRend:
                    _color = sprRend.color;
                    break;
                case Graphic img:
                    _color = img.color;
                    break;
                case Outline outline:
                    _color = outline.effectColor;
                    break;
            }
        }

        public override string DropDownItemName => "Switch Colors";
        public override string DropDownGroupName => "Graphics";

        private void CheckObject()
        {
            for (var i = 0; i < _objects.Length; i++)
            {
                var @object = _objects[i];
                switch (@object)
                {
                    case Renderer sprRen:
                    case Text text:
                    case Image img:
                    case Outline outline:
                        break;
                    default:
                        var go = (@object as GameObject)?.gameObject;
                        if (go == null)
                            go = (@object as Component)?.gameObject;
                        if (go == null)
                        {
                            _objects[i] = null;
                            return;
                        }

                        Object cmp = go.GetComponent<Renderer>();
                        if (cmp == null)
                            cmp = go.GetComponent<Graphic>();

                        if (cmp != null)
                            _objects[i] = cmp;
                        break;
                }
            }
        }

#endif
    }
}