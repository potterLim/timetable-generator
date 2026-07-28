using System;
using System.IO;
using System.Text;
using TimetableGenerator.HandongCatalogGenerator.Domain;

namespace TimetableGenerator.HandongCatalogGenerator.Tests.Handong.Source;

internal sealed class TemporaryHandongSourceFile : IDisposable
{
    private const int CP949_CODE_PAGE = 949;
    private const string TEST_DIRECTORY_PREFIX = "HandongCatalogGeneratorTests-";

    private static readonly Encoding CP949_ENCODING = createCp949Encoding();

    private readonly string mDirectoryPath;

    public CatalogSourceFilePath FilePath { get; }

    public TemporaryHandongSourceFile(string sourceHtml)
        : this(encodeCp949(sourceHtml))
    {
    }

    public TemporaryHandongSourceFile(byte[] sourceBytes)
    {
        if (sourceBytes == null)
        {
            throw new ArgumentNullException(nameof(sourceBytes));
        }

        mDirectoryPath = Path.Combine(Path.GetTempPath(), TEST_DIRECTORY_PREFIX + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(mDirectoryPath);

        string sourceFilePath = Path.Combine(mDirectoryPath, "source.xls");
        File.WriteAllBytes(sourceFilePath, sourceBytes);
        FilePath = new CatalogSourceFilePath(sourceFilePath);
    }

    public void Dispose()
    {
        if (Directory.Exists(mDirectoryPath))
        {
            Directory.Delete(mDirectoryPath, true);
        }
    }

    private static Encoding createCp949Encoding()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        return Encoding.GetEncoding(CP949_CODE_PAGE, EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback);
    }

    private static byte[] encodeCp949(string sourceHtml)
    {
        if (sourceHtml == null)
        {
            throw new ArgumentNullException(nameof(sourceHtml));
        }

        return CP949_ENCODING.GetBytes(sourceHtml);
    }
}
