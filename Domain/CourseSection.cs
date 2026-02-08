using System;

namespace TimetableGenerator
{
    public readonly struct CourseSection : IEquatable<CourseSection>
    {
        private const string DEFAULT_SECTION = "00";

        public string Value { get; }

        public CourseSection(string value)
        {
            Value = value ?? string.Empty;
        }

        public bool IsDefault()
        {
            return string.Equals(Value, DEFAULT_SECTION, StringComparison.Ordinal);
        }

        public bool Equals(CourseSection other)
        {
            return string.Equals(Value, other.Value, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is CourseSection other && Equals(other);
        }

        public override int GetHashCode()
        {
            return (Value ?? string.Empty).GetHashCode();
        }

        public static bool operator ==(CourseSection left, CourseSection right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(CourseSection left, CourseSection right)
        {
            return !left.Equals(right);
        }

        public override string ToString()
        {
            return Value ?? string.Empty;
        }
    }
}
