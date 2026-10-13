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

        public override void SetDirty() => Dirty(graphic);

        // Маска — системные методы инспектора UIComponent (блок «Maskable»).
        /// <summary>Maskable графики-<see cref="Image"/>; <see cref="SpriteRenderer"/> или графики нет — <c>null</c>.</summary>
        internal bool? GetMaskable() => graphic is MaskableGraphic maskableGraphic ? maskableGraphic.maskable : null;

        /// <summary>Выставить maskable графике-<see cref="Image"/>; иначе — ничего. Изменение записывается в Undo.</summary>
        internal void SetMaskable(bool maskable)
        {
            if (graphic is not MaskableGraphic maskableGraphic)
                return;
            UnityEditor.Undo.RecordObject(maskableGraphic, "Maskable");
            maskableGraphic.maskable = maskable;
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