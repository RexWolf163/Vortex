using System;
using System.Collections.Generic;
using System.Linq;
using Naninovel;
using Naninovel.UI;
using Sirenix.OdinInspector;
using UnityEngine;
using Vortex.Core.Extensions.LogicExtensions;
using Vortex.Unity.AppSystem.System.TimeSystem;
using Vortex.Unity.EditorTools.Attributes;
using Vortex.Unity.UI.Attributes;
using Vortex.Unity.UI.StateSwitcher;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Vortex.NaniExtensions.Misc
{
    [RequireComponent(typeof(UIStateSwitcher))]
    public class DialogBubbleSwitcher : MonoBehaviour
    {
        [StateSwitcher(typeof(CharacterLookDirection))] [SerializeField]
        private UIStateSwitcher switcher;

        [SerializeField] private RectTransform rect;
        [SerializeField, AutoLink] private RectTransform bubble;

        [SerializeField, AutoLink] private RevealableTextPrinterPanel panel;

        [SerializeField, ValueDropdown("NaniVariables")]
        private string actorNameVar = "ActorsNames";

        private ICharacterManager _manager;

        private Camera _camera;
        private Camera _uiCamera;

        /// <summary>
        /// Костыль для обработки ситуации когда спрайт рендерер не успел сформировать Bounds
        /// </summary>
        private float _lastY;

        private void OnEnable()
        {
            switcher.ResetStates();
        }

        private void OnDisable()
        {
            TimeController.RemoveCall(this);
        }

        public void Refresh(string authorId)
        {
            if (bubble.parent as RectTransform == null)
            {
                switcher.Set(panel.Appearance);
                return;
            }

            if (authorId == null)
                return;
            if (_manager is null)
                GetCache();
            if (_manager is null)
                return;

            TimeController.Call(() => CallRefresh(authorId), 0, this);
        }

        private void CallRefresh(string authorId)
        {
            if (_camera == null || _uiCamera == null)
            {
                var service = Engine.GetServiceOrErr<ICameraManager>();
                _camera = service.Camera;
                _uiCamera = service.UICamera;
            }

            try
            {
                var actor = _manager.Actors.FirstOrDefault(a => a.Id == authorId);
                if (actor is null)
                    authorId = CheckPseudonym(authorId);
                actor = _manager.Actors.FirstOrDefault(a => a.Id == authorId);
                if (actor is null)
                    return;

                var position = actor.Position.x < 0.5f ? CharacterLookDirection.Right : CharacterLookDirection.Left;
                switcher.Set(position);
                SetPosition(actor);
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
        }

        private void SetPosition(ICharacterActor actor)
        {
            var bounds = Rect.zero;
            var worldPos = Vector3.zero;
            switch (actor)
            {
                case SpriteCharacter spriteChar:
                    var spriteRenderer = spriteChar?.GameObject.GetComponent<TransitionalSpriteRenderer>();
                    worldPos = actor.Position;

                    if (spriteRenderer == null)
                    {
                        bubble.localPosition = Vector3.zero;
                        return;
                    }

                    bounds = spriteRenderer.Bounds;
                    var point = (bounds.yMax - bounds.yMin) * 0.7f + bounds.yMin;
                    var topY = point * spriteChar.GameObject.transform.lossyScale.y;
                    worldPos.y += topY;
                    break;

                case GenericCharacter genericChar:
                    var targetHandler = genericChar.Transform.GetComponentInChildren<BubblePositionTarget>();
                    if (targetHandler == null)
                    {
                        Debug.LogError(
                            $"[DialogBubbleSwitcher] Не найден компонент BubblePositionTarget для {actor.Id}!");
                        worldPos = genericChar.Transform.position;
                        break;
                    }

                    worldPos = targetHandler.GetPosition();
                    break;
            }


            var screenPos = RectTransformUtility.WorldToScreenPoint(_camera, worldPos);

            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                bubble.parent as RectTransform,
                screenPos,
                _uiCamera,
                out var localPos
            );

            bubble.localPosition = localPos;
        }

        private string CheckPseudonym(string authorId)
        {
            var variablesManager = Engine.GetServiceOrErr<ICustomVariableManager>();
            var actorNames = variablesManager.GetVariableValue(actorNameVar);
            var arNames = actorNames.String.Split(";").Select(s => s.Split(":"));
            var associations = new Dictionary<string, string>();
            foreach (var arName in arNames)
            {
                if (arName[0].IsNullOrWhitespace())
                    continue;
                var name = arName[0];
                var temp = arName[1].Split(",");
                foreach (var pseudoName in temp)
                    associations.Add(pseudoName, name);
            }

            if (associations.Keys.Contains(authorId))
                authorId = associations[authorId];

            return authorId;
        }

        private void GetCache()
        {
            _manager = Engine.GetServiceOrErr<ICharacterManager>();
        }

#if UNITY_EDITOR
        private List<string> NaniVariables()
        {
            var result = new List<string>();
            var guids = AssetDatabase.FindAssets("t:CustomVariablesConfiguration");

            if (guids.Length == 0)
                return result;

            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var config = AssetDatabase.LoadAssetAtPath<CustomVariablesConfiguration>(path);
                if (config != null && config.PredefinedVariables != null)
                    result.AddRange(config.PredefinedVariables.Select(customVar => customVar.Name));
            }

            return result;
        }
#endif
    }
}