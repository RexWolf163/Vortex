using System;
using System.Linq;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Vortex.Unity.UI.StateSwitcher.Items
{
    [Serializable]
    public class SpritesSwitch : StateItem
    {
        [SerializeField] [PreviewField(60f, ObjectFieldAlignment.Left)] [HorizontalGroup("h1", 70f)] [HideLabel]
        private Sprite _sprite;

        [SerializeField] [HorizontalGroup("h1")] [HideLabel] [OnValueChanged("CheckObject")]
        private Object[] _images = { };


        public override void Set()
        {
            SetSprite(_sprite);
        }

        private void SetSprite(Sprite sprite)
        {
            for (var i = 0; i < _images.Length; i++)
            {
                SetSprite(i, sprite);
            }
        }

        private void SetSprite(int index, Sprite sprite)
        {
            var image = _images[index];
            switch (image)
            {
                case SpriteRenderer sprRend:
                    sprRend.sprite = sprite;
                    break;
                case Image img:
                    img.sprite = sprite;
                    break;
            }
        }

        public override void DefaultState()
        {
            SetSprite(null);
        }


#if UNITY_EDITOR
        public override StateItem Clone()
        {
            var clone = new SpritesSwitch
            {
                _sprite = _sprite,
                _images = _images.ToArray()
            };
            return clone;
        }

        [ShowIf("@_images != null && _images.Length > 0")]
        [Button("Get from...")]
        [GUIColor("@Color.green")]
        [HorizontalGroup("h1")]
        private void SetCurrent()
        {
            if (_images != null && _images.Length > 0)
                switch (_images.FirstOrDefault())
                {
                    case SpriteRenderer sprRend:
                        _sprite = sprRend.sprite;
                        break;
                    case Image img:
                        _sprite = img.sprite;
                        break;
                }
        }

        public override string DropDownItemName => "Switch Sprites";
        public override string DropDownGroupName => "Graphics";

        private void CheckObject()
        {
            for (var i = 0; i < _images.Length; i++)
            {
                var image = _images[i];
                switch (image)
                {
                    case SpriteRenderer sprRen:
                    case Image img:
                        break;
                    default:
                        var go = (image as GameObject)?.gameObject;
                        if (go == null)
                            go = (image as Component)?.gameObject;
                        if (go == null)
                        {
                            _images[i] = null;
                            return;
                        }

                        Object cmp = go.GetComponent<SpriteRenderer>();
                        if (cmp == null)
                            cmp = go.GetComponent<Image>();

                        if (cmp != null)
                            _images[i] = cmp;
                        break;
                }
            }
        }
#endif
    }
}