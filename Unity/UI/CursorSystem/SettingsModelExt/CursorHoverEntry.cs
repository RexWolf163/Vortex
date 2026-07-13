using System;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Vortex.Unity.UI.CursorSystem
{
    /// <summary>
    /// Набор курсора: <see cref="Default"/> (без нажатий), <see cref="Action"/> (LMB),
    /// <see cref="AltAction"/> (RMB). Используется и как базовый набор пакета
    /// (<see cref="CursorPack.CursorDefault"/>), и как hover-вариант в <see cref="CursorPack.CursorOnHover"/>
    /// (на который <see cref="MouseHoverListener"/> ссылается по индексу).
    ///
    /// Разрешение (см. CursorController.ResolveHover): LMB → Action, RMB → AltAction, иначе → Default.
    /// Незаполненное action-поле откатывается на <see cref="Default"/>; пустой <see cref="Default"/>
    /// у hover-варианта — на Default базового набора.
    /// </summary>
    [Serializable]
    public class CursorHoverEntry
    {
        [FoldoutGroup("$name")]
        [Tooltip("Имя набора — ярлык для выпадашки MouseHoverListener и читаемый ключ.")]
        [SerializeField]
        private string name;

        [FoldoutGroup("$name")]
        [Tooltip("Курсор без нажатий. У hover-варианта null = Default базового набора.")]
        [SerializeField]
        private Sprite cursorDefault;

        [FoldoutGroup("$name")]
        [Tooltip("Курсор при основном действии (LMB). null = откат на Default.")]
        [SerializeField]
        private Sprite cursorAction;

        [FoldoutGroup("$name")] [Tooltip("Курсор при альт-действии (RMB). null = откат на Default.")] [SerializeField]
        private Sprite cursorAltAction;

        [FoldoutGroup("$name")]
        [Tooltip("Скрыть системный курсор для этого набора (вместо спрайта) — под кастомный курсор.")]
        [SerializeField]
        private bool hideCursor;

        public string Name => name;
        public Sprite Default => cursorDefault;
        public Sprite Action => cursorAction;
        public Sprite AltAction => cursorAltAction;
        public bool HideCursor => hideCursor;
    }
}