namespace TimetableGenerator
{
    // Represents a classroom location in "Building Room" format.
    // Example: "OH 101"
    public sealed class ClassroomLocation
    {
        public string Building { get; private set; }
        public string Room { get; private set; }

        public ClassroomLocation(string building, string room)
        {
            Building = building;
            Room = room;
        }

        public string ToDisplayText()
        {
            return $"{Building} {Room}";
        }

        // Expected format: "{건물명} {호실}"
        // There must be at least one whitespace separating building and room.
        public static bool TryParse(string raw, out ClassroomLocation location)
        {
            location = null;

            if (string.IsNullOrWhiteSpace(raw))
            {
                return false;
            }

            string trimmed = raw.Trim();

            int spaceIndex = trimmed.IndexOf(' ');
            if (spaceIndex <= 0 || spaceIndex >= trimmed.Length - 1)
            {
                return false;
            }

            string building = trimmed.Substring(0, spaceIndex).Trim();
            string room = trimmed.Substring(spaceIndex + 1).Trim();

            if (string.IsNullOrWhiteSpace(building) || string.IsNullOrWhiteSpace(room))
            {
                return false;
            }

            location = new ClassroomLocation(building, room);
            return true;
        }
    }
}
