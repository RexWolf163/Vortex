using System;
using UnityEngine;

namespace Vortex.Unity.UI.CursorSystem
{
    /// <summary>
    /// Привязка набора курсоров к диапазону разрешений.
    /// Набор применяется, пока вертикальное разрешение экрана не превышает
    /// <see cref="MaxScreenHeight"/>. Выбор делает CursorController:
    /// берётся набор с минимальным подходящим порогом; если текущее разрешение
    /// больше всех порогов — самый крупный набор
    /// </summary>
    [Serializable]
    public class CursorResolutionPack
    {
        [Tooltip("Верхняя граница вертикального разрешения (Screen.height), " +
                 "до которой включительно применяется этот набор")]
        [SerializeField] private int maxScreenHeight = 1080;

        [SerializeField] private CursorPack pack = new();

        public int MaxScreenHeight => maxScreenHeight;
        public CursorPack Pack => pack;
    }
}
