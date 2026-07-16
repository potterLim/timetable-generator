using TimetableGenerator.Infrastructure.Catalogs;

namespace TimetableGenerator.Desktop.Product;

internal static class ProductCatalogSynchronizationDefaults
{
    private const long MAXIMUM_CATALOG_BYTES = 32L * 1_024L * 1_024L;
    private const long MAXIMUM_INDEX_BYTES = 1L * 1_024L * 1_024L;

    public static CatalogSynchronizationLimits CreateLimits()
    {
        return new CatalogSynchronizationLimits(
            new CatalogResourceByteLimit(MAXIMUM_INDEX_BYTES),
            new CatalogResourceByteLimit(MAXIMUM_CATALOG_BYTES));
    }
}
