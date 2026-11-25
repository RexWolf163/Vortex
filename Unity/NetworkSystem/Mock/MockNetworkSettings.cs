#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;
using Vortex.Unity.CoreAssetsSystem;

namespace Vortex.Unity.NetworkSystem.Mock
{
    /// <summary>
    /// Настройки мок-драйвера: общие поля <see cref="NetworkSettingsAsset"/> (ведутся отдельно от ассета UnityWebRequest —
    /// тестовая среда может отличаться) + реакция по умолчанию и правила. Существует только в редакторе и
    /// development-сборках.
    /// </summary>
    [CreateAssetMenu(fileName = "MockNetworkSettings", menuName = "Vortex/Settings/Network (Mock)")]
    public class MockNetworkSettings : NetworkSettingsAsset, ICoreAsset
    {
        [BoxGroup("Мок")]
        [SerializeField, Tooltip("Ответ, когда ни одно правило не подошло. В лог пишется предупреждение «нет правила».")]
        private MockReaction defaultReaction = new() { statusCode = 404 };

        [BoxGroup("Мок")]
        [SerializeField, Tooltip("Сверху вниз до первого совпадения маски и метода.")]
        private List<MockRule> rules = new();

        public MockReaction DefaultReaction => defaultReaction;
        public IReadOnlyList<MockRule> Rules => rules;
    }
}
#endif
