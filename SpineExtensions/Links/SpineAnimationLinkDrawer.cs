#if UNITY_EDITOR
using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using Sirenix.OdinInspector.Editor;
using Spine;
using Spine.Unity;
using UnityEditor;
using UnityEngine;
using Vortex.Unity.EditorTools.Elements;
using Object = UnityEngine.Object;

namespace Vortex.SpineExtensions.Links
{
    /// <summary>
    /// Odin-drawer <see cref="SpineAnimationLink"/> — одна строка: имя анимации | трек | Loop | Play/Stop.
    /// С <see cref="MasterLinkAttribute"/> имя выбирается из анимаций скелета мастер-поля, а Play / Stop запускают
    /// анимацию на её треке в Play Mode (Stop — пустая анимация на треке запуска). Пока на запомненном треке стоит
    /// именно запущенная отсюда запись — показывается Stop, иначе Play. Без атрибута имя — текстовое поле, кнопок нет
    /// </summary>
    public sealed class SpineAnimationLinkDrawer : OdinValueDrawer<SpineAnimationLink>
    {
        private const string Placeholder = "[NONE]";

        /// <summary>Отладочный запуск: трек и запись; null — не запускалось</summary>
        private TrackEntry _debugEntry;

        private int _debugTrack;

        protected override void DrawPropertyLayout(GUIContent label)
        {
            var animationProperty = Property.Children["animation"];
            var trackProperty = Property.Children["track"];
            var loopProperty = Property.Children["loop"];

            var master = Property.GetAttribute<MasterLinkAttribute>();
            Object skeleton = null;
            string error = null;
            if (master != null)
                error = ResolveSkeleton(master.FieldName, out skeleton);

            EditorGUILayout.BeginHorizontal();
            if (label != null)
                EditorGUILayout.PrefixLabel(label);

            var animationName = (string)animationProperty.ValueEntry.WeakSmartValue;
            if (skeleton is IHasSkeletonDataAsset hasData && hasData.SkeletonDataAsset != null)
            {
                var names = hasData.SkeletonDataAsset.GetSkeletonData(true).Animations.Select(a => a.Name).ToArray();
                var rect = GUILayoutUtility.GetRect(GUIContent.none, EditorStyles.popup, GUILayout.MinWidth(80f));
                SearchablePopup.Draw(rect, names, Array.IndexOf(names, animationName), Placeholder, index =>
                {
                    var picked = index >= 0 && index < names.Length ? names[index] : string.Empty;
                    Property.Tree.DelayActionUntilRepaint(() => animationProperty.ValueEntry.WeakSmartValue = picked);
                });
            }
            else
            {
                var text = EditorGUILayout.TextField(animationName ?? string.Empty, GUILayout.MinWidth(80f));
                if (text != animationName)
                    animationProperty.ValueEntry.WeakSmartValue = text;
            }

            var track = (int)trackProperty.ValueEntry.WeakSmartValue;
            var newTrack = Mathf.Max(0, EditorGUILayout.IntField(track, GUILayout.Width(36f)));
            if (newTrack != track)
                trackProperty.ValueEntry.WeakSmartValue = newTrack;

            var loop = (bool)loopProperty.ValueEntry.WeakSmartValue;
            var newLoop = EditorGUILayout.ToggleLeft("Loop", loop, GUILayout.Width(48f));
            if (newLoop != loop)
                loopProperty.ValueEntry.WeakSmartValue = newLoop;

            if (skeleton is IAnimationStateComponent stateComponent)
                DrawDebugButton(stateComponent, animationName, newTrack, newLoop);

            EditorGUILayout.EndHorizontal();

            if (error != null)
                EditorGUILayout.HelpBox(error, MessageType.Error);
        }

        /// <summary>Play / Stop для теста; вне Play Mode кнопка неактивна</summary>
        private void DrawDebugButton(IAnimationStateComponent stateComponent, string animationName, int track,
            bool loop)
        {
            var state = Application.isPlaying ? stateComponent.AnimationState : null;
            var running = state != null && _debugEntry != null && state.GetCurrent(_debugTrack) == _debugEntry;
            using (new EditorGUI.DisabledScope(state == null || (!running && string.IsNullOrEmpty(animationName))))
            {
                if (!GUILayout.Button(running ? "Stop" : "Play", GUILayout.Width(50f)))
                    return;
            }

            if (running)
            {
                state.SetEmptyAnimation(_debugTrack, 0f);
                _debugEntry = null;
                return;
            }

            _debugTrack = track;
            _debugEntry = state.SetAnimation(track, animationName, loop);
        }

        /// <summary>
        /// Скелет из мастер-поля объекта-владельца ссылки (элемент массива — владелец массива).
        /// Возвращает текст ошибки конфигурации; null — ошибки нет (пустое поле — не ошибка)
        /// </summary>
        private string ResolveSkeleton(string fieldName, out Object skeleton)
        {
            skeleton = null;
            var owner = ResolveOwner();
            if (owner == null)
                return null;

            var field = FindField(owner.GetType(), fieldName);
            if (field == null)
                return $"[MasterLink] Поле «{fieldName}» не найдено в {owner.GetType().Name}.";

            skeleton = field.GetValue(owner) as Object;
            if (skeleton == null)
                return null;
            if (skeleton is IAnimationStateComponent && skeleton is IHasSkeletonDataAsset)
                return null;

            var type = skeleton.GetType().Name;
            skeleton = null;
            return $"[MasterLink] Поле «{fieldName}» ({type}) — не скелет Spine " +
                   "(нужны IAnimationStateComponent и IHasSkeletonDataAsset).";
        }

        private object ResolveOwner()
        {
            var parent = Property.ParentValueProperty;
            while (parent != null && parent.ValueEntry?.WeakSmartValue is IList)
                parent = parent.ParentValueProperty;
            if (parent != null)
                return parent.ValueEntry?.WeakSmartValue;
            var targets = Property.Tree.WeakTargets;
            return targets.Count > 0 ? targets[0] : null;
        }

        private static FieldInfo FindField(Type type, string name)
        {
            for (; type != null; type = type.BaseType)
            {
                var field = type.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (field != null)
                    return field;
            }

            return null;
        }
    }
}
#endif
