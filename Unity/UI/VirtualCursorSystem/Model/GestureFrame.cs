using System;
using UnityEngine;

namespace Vortex.Unity.UI.VirtualCursorSystem.Model
{
    /// <summary>Фаза жеста мультикасания.</summary>
    public enum GesturePhase
    {
        /// <summary>Жеста нет.</summary>
        None,

        /// <summary>Жест начался: на экране набралось нужное число пальцев. Дельты нейтральные.</summary>
        Began,

        /// <summary>Жест продолжается: дельты за кадр.</summary>
        Changed,

        /// <summary>Жест закончился (пальцы подняты или их число изменилось). Дельты нейтральные.</summary>
        Ended,
    }

    /// <summary>
    /// Кадр жеста мультикасания: состояние пальцев и их изменение за кадр. Импульс, а не состояние: дельты
    /// (<see cref="Pan"/>, <see cref="Scale"/>, <see cref="Rotation"/>) относятся к кадру публикации.
    /// Нейтральные дельты — <c>Pan = 0</c>, <c>Scale = 1</c>, <c>Rotation = 0</c>. Value-equality — для реактивного дедупа.
    /// </summary>
    public readonly struct GestureFrame : IEquatable<GestureFrame>
    {
        /// <summary>Нет жеста.</summary>
        public static readonly GestureFrame None = new(0, GesturePhase.None, Vector2.zero, Vector2.zero, 0f, 1f, 0f);

        /// <summary>Число пальцев жеста (0 — жеста нет).</summary>
        public readonly int Fingers;

        /// <summary>Фаза жеста.</summary>
        public readonly GesturePhase Phase;

        /// <summary>Центр пальцев (центроид), пиксели экрана.</summary>
        public readonly Vector2 Center;

        /// <summary>Сдвиг центра за кадр, пиксели — панорамирование.</summary>
        public readonly Vector2 Pan;

        /// <summary>Среднее расстояние пальцев до центра, пиксели — размах.</summary>
        public readonly float Spread;

        /// <summary>Отношение размаха к размаху прошлого кадра: больше 1 — расширение, меньше 1 — сужение.</summary>
        public readonly float Scale;

        /// <summary>Поворот пальцев вокруг центра за кадр, градусы; положительный — против часовой стрелки.</summary>
        public readonly float Rotation;

        /// <summary>Кадр жеста из готовых значений.</summary>
        public GestureFrame(int fingers, GesturePhase phase, Vector2 center, Vector2 pan, float spread, float scale,
            float rotation)
        {
            Fingers = fingers;
            Phase = phase;
            Center = center;
            Pan = pan;
            Spread = spread;
            Scale = scale;
            Rotation = rotation;
        }

        /// <summary>Равенство по всем полям.</summary>
        public bool Equals(GestureFrame other) =>
            Fingers == other.Fingers && Phase == other.Phase && Center == other.Center && Pan == other.Pan &&
            Spread.Equals(other.Spread) && Scale.Equals(other.Scale) && Rotation.Equals(other.Rotation);

        /// <inheritdoc/>
        public override bool Equals(object obj) => obj is GestureFrame f && Equals(f);

        /// <inheritdoc/>
        public override int GetHashCode() => HashCode.Combine(Fingers, (int)Phase, Center, Pan, Spread, Scale, Rotation);
    }
}
