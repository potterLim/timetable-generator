using System;
using System.IO;
using TimetableGenerator.HandongCatalogGenerator.Domain;

namespace TimetableGenerator.HandongCatalogGenerator.Tests.Application;

internal sealed class TemporaryCatalogOutputRoot : IDisposable
{
    private readonly string mDirectoryPath;

    public CatalogOutputRootPath OutputRootPath { get; }

    public TemporaryCatalogOutputRoot()
    {
        mDirectoryPath = Path.Combine(Path.GetTempPath(), "HandongCatalogOutputTests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(mDirectoryPath);
        OutputRootPath = new CatalogOutputRootPath(mDirectoryPath);
    }

    public void Dispose()
    {
        if (Directory.Exists(mDirectoryPath))
        {
            Directory.Delete(mDirectoryPath, true);
        }
    }
}
