using UnityEngine;

namespace Vortex.Unity.Components.Misc
{
    /// <summary>
    /// Контейнер для хранения невыгружаемых объектов
    /// </summary>
    public class NotDestroyableSystemContainer : MonoBehaviour
    {
        /// <summary>
        /// Ключ-идентификатор.
        /// Контейнер не срабатывает если уже имеется его копия
        /// Для защиты от ошибок при запуске из редактора
        /// </summary>
        [SerializeField] private string key;

        private string Key => key;

        void Awake()
        {
            if (key is null or "")
                Debug.LogError($"[NotDestroyableSystemContainer: {name}] Key is empty");

            //Удаляет себя если есть другие контейнеры с тем же ключом
            var list = FindObjectsByType<NotDestroyableSystemContainer>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

            foreach (var item in list)
            {
                if (item.Key != key || item == this)
                    continue;
                Destroy(gameObject);
                return;
            }

            DontDestroyOnLoad(this);
        }
    }
}