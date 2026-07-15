using System.Threading;

namespace TimetableGenerator.Infrastructure.Csv;

public interface ICourseCsvImporter
{
    CourseImportResult ImportCourses(
        CsvInputFilePath inputFilePath,
        CourseCsvImportOptions options,
        CancellationToken cancellationToken);
}
