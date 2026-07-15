using System;

namespace TimetableGenerator.Core.Domain;

public sealed record ClassroomLocation
{
    public BuildingName BuildingName { get; }

    public RoomIdentifier RoomIdentifier { get; }

    public ClassroomLocation(BuildingName buildingName, RoomIdentifier roomIdentifier)
    {
        if (buildingName == null)
        {
            throw new ArgumentNullException(nameof(buildingName));
        }

        if (roomIdentifier == null)
        {
            throw new ArgumentNullException(nameof(roomIdentifier));
        }

        BuildingName = buildingName;
        RoomIdentifier = roomIdentifier;
    }

    public string ToDisplayText()
    {
        return BuildingName + " " + RoomIdentifier;
    }

    public override string ToString()
    {
        return ToDisplayText();
    }
}
