#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using System.Text;
using Sirenix.OdinInspector;
using UnityEngine;
using Vortex.Core.MaskSystem;
using Vortex.Core.NetworkSystem;
using Vortex.Unity.MaskSystem;

namespace Vortex.Unity.NetworkSystem.Mock
{
    /// <summary>Вид реакции мока.</summary>
    public enum MockReactionKind
    {
        /// <summary>HTTP-ответ.</summary>
        Response,

        /// <summary>Транспортная ошибка (итог Network).</summary>
        TransportError,

        /// <summary>Не отвечать: попытку прерывает таймаут ядра.</summary>
        NoResponse
    }

    /// <summary>Реакция мока на одну попытку.</summary>
    [Serializable]
    public class MockReaction
    {
        public MockReactionKind kind = MockReactionKind.Response;

        [ShowIf(nameof(IsResponse)), Range(100, 599)]
        public int statusCode = 200;

        [ShowIf(nameof(IsResponse))]
        public List<NetworkHeader> headers = new();

        [ShowIf(nameof(IsResponse)), Tooltip("Тело ответа. Если задан — приоритетнее текста.")]
        public TextAsset bodyAsset;

        [ShowIf(nameof(IsResponse)), MultiLineProperty(5), HideIf(nameof(HasBodyAsset))]
        public string bodyText;

        [Min(0f), SuffixLabel("с", true), Tooltip("Задержка до ответа.")]
        public float delay = 0.1f;

        [Min(0f), SuffixLabel("с", true), Tooltip("Длительность передачи: прогресс плавно идёт 0 → 1.")]
        public float transferDuration;

        private bool IsResponse => kind == MockReactionKind.Response;
        private bool HasBodyAsset => bodyAsset != null;

        /// <summary>Тело в байтах (UTF-8 для текста).</summary>
        public byte[] GetBody() =>
            bodyAsset != null ? bodyAsset.bytes
            : string.IsNullOrEmpty(bodyText) ? null
            : Encoding.UTF8.GetBytes(bodyText);
    }

    /// <summary>Правило мока: какие запросы ловит и чем отвечает по попыткам.</summary>
    [Serializable]
    public class MockRule
    {
        [Tooltip("Идентификатор для панели мока. Непустой и уникальный.")]
        public string id;

        public bool enabled = true;

        [SerializeReference, MaskSelector]
        public Mask mask;

        [Tooltip("Ловить запросы любым методом.")]
        public bool anyMethod = true;

        [HideIf(nameof(anyMethod))]
        public HttpMethod method = HttpMethod.Get;

        [Tooltip("Попытка n запроса получает реакцию n; после конца списка — последнюю.")]
        public List<MockReaction> reactions = new() { new MockReaction() };

        [Range(0, 100), SuffixLabel("%", true), Tooltip("Доля попыток, подменяемых транспортной ошибкой.")]
        public int failurePercent;

        internal MockReaction ReactionFor(int attemptNumber) =>
            reactions[Math.Min(Math.Max(attemptNumber, 1), reactions.Count) - 1];
    }
}
#endif
