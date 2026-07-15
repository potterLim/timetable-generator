using System;

namespace TimetableGenerator.Core.Domain;

public readonly record struct ClassroomAssignment
{
    private readonly ClassroomLocation mClassroomLocationOrNull;

    public static ClassroomAssignment Unassigned
    {
        get
        {
            return default(ClassroomAssignment);
        }
    }

    public bool IsAssigned
    {
        get
        {
            return mClassroomLocationOrNull != null;
        }
    }

    private ClassroomAssignment(ClassroomLocation classroomLocation)
    {
        mClassroomLocationOrNull = classroomLocation;
    }

    public static ClassroomAssignment CreateAssigned(ClassroomLocation classroomLocation)
    {
        if (classroomLocation == null)
        {
            throw new ArgumentNullException(nameof(classroomLocation));
        }

        return new ClassroomAssignment(classroomLocation);
    }

    public ClassroomLocation GetClassroomLocation()
    {
        if (IsAssigned == false)
        {
            throw new InvalidOperationException("The course offering does not have an assigned classroom.");
        }

        return mClassroomLocationOrNull;
    }
}
