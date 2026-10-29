using System;

namespace Vortex.SpineExtensions.Links
{
    /// <summary>
    /// Связь поля <see cref="SpineAnimationLink"/> со скелетом: <see cref="FieldName"/> — имя поля того же объекта со
    /// ссылкой на скелет (<c>SkeletonGraphic</c> или <c>SkeletonAnimation</c> — любой компонент с
    /// <c>IAnimationStateComponent</c> и <c>IHasSkeletonDataAsset</c>). В инспекторе имя анимации выбирается из
    /// анимаций этого скелета, появляются кнопки Play / Stop для теста в Play Mode
    /// </summary>
    [AttributeUsage(AttributeTargets.Field)]
    public class MasterLinkAttribute : Attribute
    {
        /// <summary>Имя поля со ссылкой на скелет</summary>
        public string FieldName { get; }

        public MasterLinkAttribute(string fieldName) => FieldName = fieldName;
    }
}
