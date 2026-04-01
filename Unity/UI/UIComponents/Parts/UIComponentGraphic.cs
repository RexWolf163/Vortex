using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Vortex.Unity.UI.UIComponents.Parts
{
    public class UIComponentGraphic : UIComponentPart
    {
        [SerializeField, OnValueChanged("ChangeGraphic")]
        private Object graphic;

#if UNITY_EDITOR
        [OnInspectorInit]
        private void Search()
        {
            if (graphic != null)
                return;
            graphic = GetComponent<Graphic>();
            ChangeGraphic();
        }

        private void ChangeGraphic()
        {
            if (graphic == null)
                return;
            switch (graphic)
            {
                case SpriteRenderer sprRend:
                case Image img:
                    return;
            }

            graphic = null;
        }
#endif
        public void PutData(Sprite sprite)
        {
            switch (graphic)
            {
                case SpriteRenderer sprRend:
                    sprRend.sprite = sprite;
                    break;
                case Image img:
                    img.sprite = sprite;
                    break;
            }
        }

        public void PutData(Texture2D texture)
        {
            var newSprite = Sprite.Create(
                texture,
                new Rect(0, 0, texture.width, texture.height),
                new Vector2(0.5f, 0.5f)
            );

            PutData(newSprite);
        }

        private void OnDestroy()
        {
            switch (graphic)
            {
                case SpriteRenderer sprRend:
                    sprRend.sprite = null;
                    break;
                case Image img:
                    img.sprite = null;
                    break;
            }
        }

        public Sprite GetValue()
        {
            switch (graphic)
            {
                case SpriteRenderer sprRend:
                    return sprRend.sprite;
                case Image img:
                    return img.sprite;
            }

            return null;
        }
    }
}