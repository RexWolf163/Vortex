using System;

namespace Vortex.Unity.InputBusSystem
{
    public class InputSubscriber : IEquatable<InputSubscriber>
    {
        public InputSubscriber(object owner, Action onPerformed, Action onCanceled)
        {
            Owner = owner;
            OnPerformed = onPerformed;
            OnCanceled = onCanceled;
        }

        public readonly object Owner;
        public Action OnPerformed { get; private set; }
        public Action OnCanceled { get; private set; }

        public bool Equals(InputSubscriber other)
        {
            if (other is null) return false;
            if (ReferenceEquals(this, other)) return true;
            return Equals(Owner, other.Owner);
        }

        public override bool Equals(object obj)
        {
            if (obj is null) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != GetType()) return false;
            return Equals((InputSubscriber)obj);
        }

        public override int GetHashCode()
        {
            return (Owner != null ? Owner.GetHashCode() : 0);
        }
    }
}