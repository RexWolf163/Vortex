#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Vortex.Unity.EditorTools.Abstraction
{
    /// <summary>
    /// Интерфейс для PropertyDrawer которые работают несколько синхронно на одно поле
    /// </summary>
    public interface IMultiDrawerAttribute
    {
        /// <summary>
        /// Подготовка к отрисовке.
        /// Здесь необходимо переключить флаги, если есть необходимость
        /// </summary>
        /// <param name="data"></param>
        /// <param name="attribute"></param>
        public void PreRender(PropertyData data, PropertyAttribute attribute);

        /// <summary>
        /// Отрисовка метки
        /// </summary>
        /// <returns>FALSE если не претендует на поле</returns>
        public void RenderLabel(PropertyData data, PropertyAttribute attribute);

        /// <summary>
        /// Отрисовка собственно поля
        /// </summary>
        /// <returns>FALSE если не претендует на поле</returns>
        public void RenderField(PropertyData data, PropertyAttribute attribute);

        /// <summary>
        /// Отрисовка всякой информации над полем 
        /// </summary>
        /// <param name="data"></param>
        /// <param name="attribute"></param>
        /// <param name="calculation">Признак, что нужна только высота без рендеринга</param>
        /// <returns>Высота которую занял данный drawer</returns>
        public float RenderTopper(PropertyData data, PropertyAttribute attribute, bool calculation);
    }
}
#endif