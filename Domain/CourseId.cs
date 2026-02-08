using System;

namespace TimetableGenerator
{
    public readonly struct CourseId : IEquatable<CourseId>
    {
        public int Value { get; }

        public CourseId(int value)
        {
            Value = value;
        }

        public bool Equals(CourseId other)
        {
            return Value == other.Value;
        }

        public override bool Equals(object obj)
        {
            return obj is CourseId other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Value;
        }

        public static bool operator ==(CourseId left, CourseId right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(CourseId left, CourseId right)
        {
            return !left.Equals(right);
        }

        public override string ToString()
        {
            return Value.ToString();
        }
    }
}
