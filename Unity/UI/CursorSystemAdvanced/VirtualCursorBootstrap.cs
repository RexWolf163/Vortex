using UnityEngine;

namespace Vortex.Unity.UI.CursorSystemAdvanced
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

        private void Awake()
        {
            VirtualCursorController.Init(settings);
            VirtualCursorController.ConfigureProjection(projectionMask, projectionDistance);
        }
    }
}
