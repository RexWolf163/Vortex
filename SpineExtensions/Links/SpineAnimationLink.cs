using System;
using UnityEngine;

namespace Vortex.SpineExtensions.Links
{
    /// <summary>
    /// Модель ссылки на Spine-анимацию: имя анимации, трек запуска и зацикленность. Строго данные для линковки —
    /// запуск по ним делает держатель (код, который читает поля). Пустое имя — анимация не задана.
    ///
    /// С атрибутом <see cref="MasterLinkAttribute"/> на поле в инспекторе имя выбирается из анимаций скелета, а
    /// кнопки Play / Stop запускают её для теста в Play Mode (отладка только в редакторе, на данные не влияет)
    /// </summary>
    [Serializable]
    public class SpineAnimationLink
    {
        [SerializeField, Tooltip("Имя анимации скелета. Пусто — анимация не задана.")]
        private string animation;

        [SerializeField, Min(0), Tooltip("Трек запуска.")]
        private int track;

        [SerializeField, Tooltip("Запуск в цикле.")]
        private bool loop;

        /// <summary>Имя анимации; пусто — не задана</summary>
        public string Animation => animation;

        /// <summary>Трек запуска</summary>
        public int Track => track;

        /// <summary>Запуск в цикле</summary>
        public bool Loop => loop;

        /// <summary>Анимация не задана</summary>
        public bool IsEmpty => string.IsNullOrEmpty(animation);

        public SpineAnimationLink()
        {
        }

        public SpineAnimationLink(string animation, int track, bool loop = false)
        {
            this.animation = animation;
            this.track = track;
            this.loop = loop;
        }
    }
}
