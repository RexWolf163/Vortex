using UnityEngine;

namespace Vortex.Unity.UI.VirtualCursorSystem
{
    /// <summary>
    /// Бутстрап пакета: грузит конфиг скинов и инициализирует контроллер + параметры проекции.
    /// Вешается на объект в сцене/префабе загрузки (рядом с EventSystem/UI-корнем).
    /// </summary>
    public class VirtualCursorBootstrap : MonoBehaviour
    {
        [SerializeField, Tooltip("Конфиг скинов курсора (SO).")]
        private CursorSkinSettings settings;

        [SerializeField, Tooltip("Маска слоёв для screen→world проекции.")]
        private LayerMask projectionMask = ~0;

        [SerializeField, Tooltip("Дистанция raycast проекции.")]
        private float projectionDistance = 1000f;

        // TODO(android-cursor): нет платформенного гейта. На Android новая система (в отличие от старой,
        // где рендер через ОС-курсор и на тач-экране невидим) поднимет UGUI-Image-курсор — он будет виден.
        // При доработке решить: (а) прятать визуал по источнику Point на уровне рендера (предпочтительно —
        // работает и для десктоп-тачскрина, и для Android + BT-мышь), либо (б) не поднимать бутстрап/рендерер
        // на чисто тач-платформах. См. пометки в UiImageCursorRenderer и TouchPointerDriver.
        //
        // TODO(android-cursor): преимущество новой системы — заложить поддержку подключённых устройств на Android,
        // чего старая система не умеет (она завязана на ОС-курсор: Cursor.SetCursor на Android не работает, а
        // GamepadCursorDriver старой системы требует ОС-мышь, которой на Android нет).
        //   • Android + МЫШЬ: рисовать СВОЙ UGUI-Image-курсор (независим от ОС-указателя), т.е. курсор можно
        //     показать/скрыть/стилизовать самим — MousePointerDriver + скрытие по источнику Point для касаний.
        //   • Android + ГЕЙМПАД: включать GamepadCursorDriver новой системы БЕЗ зависимости от ОС-мыши
        //     (двигает виртуальный курсор напрямую через ReportPointer), чтобы меню было навигабельно падом.
        //   Т.е. на тач-платформе визуал по умолчанию скрыт, но «оживает» при появлении мыши/геймпада.
        private void Awake()
        {
            VirtualCursorController.Init(settings);
            VirtualCursorController.ConfigureProjection(projectionMask, projectionDistance);
        }
    }
}
