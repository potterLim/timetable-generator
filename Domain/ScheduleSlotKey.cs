using System;

namespace TimetableGenerator
{
    public readonly struct ScheduleSlotKey : IEquatable<ScheduleSlotKey>
    {
        public EDay Day { get; }
        public Period Period { get; }

        public ScheduleSlotKey(EDay day, Period period)
        {
            Day = day;
            Period = period;
        }

        public bool Equals(ScheduleSlotKey other)
        {
            return Day == other.Day && Period.Equals(other.Period);
        }

        public override bool Equals(object obj)
        {
            return obj is ScheduleSlotKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                hash = (hash * 31) + Day.GetHashCode();
                hash = (hash * 31) + Period.GetHashCode();
                return hash;
            }
        }

        public static bool operator ==(ScheduleSlotKey left, ScheduleSlotKey right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(ScheduleSlotKey left, ScheduleSlotKey right)
        {
            return !left.Equals(right);
        }

        public override string ToString()
        {
            return $"{Day}:{Period.Value}";
        }
    }
}
