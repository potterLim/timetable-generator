using System;

namespace TimetableGenerator
{
    public readonly struct Period : IEquatable<Period>
    {
        public int Value { get; }

        public Period(int value)
        {
            Value = value;
        }

        public bool IsValid()
        {
            return Value > 0;
        }

        public bool Equals(Period other)
        {
            return Value == other.Value;
        }

        public override bool Equals(object obj)
        {
            return obj is Period other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Value.GetHashCode();
        }

        public static bool operator ==(Period left, Period right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(Period left, Period right)
        {
            return !left.Equals(right);
        }

        public override string ToString()
        {
            return Value.ToString();
        }
    }
}
