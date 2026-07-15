using System;
using System.IO;
using System.Text;
using TimetableGenerator.Infrastructure.Csv;

namespace TimetableGeneratorCore.Tests;

internal sealed class TemporaryCsvFile : IDisposable
{
    private readonly string mDirectoryPath;

    public CsvInputFilePath FilePath { get; }

    public TemporaryCsvFile(string fileContent)
    {
        if (fileContent == null)
        {
            throw new ArgumentNullException(nameof(fileContent));
        }

        mDirectoryPath = Path.Combine(
            Path.GetTempPath(),
            "TimetableGeneratorTests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(mDirectoryPath);

        string csvFilePath = Path.Combine(mDirectoryPath, "courses.csv");
        UTF8Encoding strictUtf8Encoding = new UTF8Encoding(false, true);
        File.WriteAllText(csvFilePath, fileContent, strictUtf8Encoding);
        FilePath = new CsvInputFilePath(csvFilePath);
    }

    public void Dispose()
    {
        if (Directory.Exists(mDirectoryPath))
        {
            Directory.Delete(mDirectoryPath, true);
        }
    }
}
