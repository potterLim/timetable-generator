using System;
using System.Security.Cryptography;
using System.Text;

using TimetableGenerator.Desktop.Presentation.Models;
using TimetableGenerator.Domain.Catalogs;

namespace TimetableGenerator.Desktop.Presentation.Catalog;

internal static class CourseAccentAssigner
{
    private static readonly ECourseAccent[] ACCENTS =
    {
        ECourseAccent.Blue,
        ECourseAccent.Purple,
        ECourseAccent.Green,
    };

    public static ECourseAccent FindAccent(CourseId courseId)
    {
        if (courseId == null)
        {
            throw new ArgumentNullException(nameof(courseId));
        }

        byte[] courseIdBytes = Encoding.UTF8.GetBytes(courseId.Value);
        byte[] digestBytes = SHA256.HashData(courseIdBytes);
        int accentIndex = digestBytes[0] % ACCENTS.Length;
        return ACCENTS[accentIndex];
    }
}
