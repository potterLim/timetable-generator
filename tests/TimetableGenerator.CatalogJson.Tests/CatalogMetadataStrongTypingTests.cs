using System;
using System.Collections.Generic;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TimetableGenerator.Domain.Catalogs;

namespace TimetableGenerator.CatalogJson.Tests;

[TestClass]
public sealed class CatalogMetadataStrongTypingTests
{
    [TestMethod]
    public void MetadataConstructorsRequireSemanticValueObjects()
    {
        assertConstructorParameterTypes(
            typeof(CatalogDocumentCounts),
            new Type[]
            {
                typeof(CatalogCourseCount),
                typeof(CatalogOfferingCount),
                typeof(CatalogScheduledOfferingCount),
                typeof(CatalogMeetingNotProvidedCount),
            });
        assertConstructorParameterTypes(
            typeof(CatalogIndexCounts),
            new Type[]
            {
                typeof(CatalogCourseCount),
                typeof(CatalogOfferingCount),
            });
        assertConstructorParameterTypes(
            typeof(CatalogDataQualityMetadata),
            new Type[]
            {
                typeof(EScheduleNormalizationSource),
                typeof(CatalogSourceEnglishScheduleMismatchCount),
                typeof(CatalogRoomNotProvidedCount),
                typeof(CatalogEnrollmentNotProvidedCount),
                typeof(CatalogInstructorUnconfirmedCount),
                typeof(CatalogMultiInstructorDisplayCount),
                typeof(CatalogSourceRemarkLookupOnlyCount),
                typeof(IEnumerable<CatalogManualReview>),
            });
        assertConstructorParameterTypes(
            typeof(CatalogSourceMetadata),
            new Type[]
            {
                typeof(InstitutionId),
                typeof(CatalogSourceLogicalFileName),
                typeof(CatalogFileExtension),
                typeof(CatalogMediaType),
                typeof(CatalogCharset),
                typeof(CatalogDecoderName),
                typeof(CatalogFileSize),
                typeof(Sha256Digest),
            });
        assertConstructorParameterTypes(
            typeof(CatalogFileDescriptor),
            new Type[]
            {
                typeof(CatalogRelativePath),
                typeof(CatalogMediaType),
                typeof(CatalogCharset),
                typeof(CatalogContentEncoding),
                typeof(CatalogFileSize),
                typeof(Sha256Digest),
            });
        assertConstructorParameterTypes(
            typeof(CatalogConverterMetadata),
            new Type[]
            {
                typeof(CatalogConverterId),
                typeof(CatalogConverterVersion),
            });
        assertConstructorParameterTypes(
            typeof(CatalogManualReview),
            new Type[]
            {
                typeof(CourseId),
                typeof(EManualReviewField),
                typeof(EManualReviewReason),
                typeof(CatalogManualReviewSourceValue),
            });
    }

    [TestMethod]
    public void MetadataPropertiesPreserveSemanticValueObjects()
    {
        assertPropertyType(
            typeof(CatalogDocumentCounts),
            nameof(CatalogDocumentCounts.CourseCount),
            typeof(CatalogCourseCount));
        assertPropertyType(
            typeof(CatalogDocumentCounts),
            nameof(CatalogDocumentCounts.OfferingCount),
            typeof(CatalogOfferingCount));
        assertPropertyType(
            typeof(CatalogDataQualityMetadata),
            nameof(CatalogDataQualityMetadata.RoomNotProvidedCount),
            typeof(CatalogRoomNotProvidedCount));
        assertPropertyType(
            typeof(CatalogSourceMetadata),
            nameof(CatalogSourceMetadata.DeclaredCharset),
            typeof(CatalogCharset));
        assertPropertyType(
            typeof(CatalogFileDescriptor),
            nameof(CatalogFileDescriptor.ContentEncoding),
            typeof(CatalogContentEncoding));
        assertPropertyType(
            typeof(CatalogConverterMetadata),
            nameof(CatalogConverterMetadata.Version),
            typeof(CatalogConverterVersion));
        assertPropertyType(
            typeof(CatalogManualReview),
            nameof(CatalogManualReview.SourceValue),
            typeof(CatalogManualReviewSourceValue));
    }

    [TestMethod]
    public void CountValueObjectsRejectOutOfRangeValues()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new CatalogCourseCount(0));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new CatalogOfferingCount(0));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(
            () => new CatalogScheduledOfferingCount(-1));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(
            () => new CatalogMeetingNotProvidedCount(-1));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(
            () => new CatalogSourceEnglishScheduleMismatchCount(-1));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(
            () => new CatalogRoomNotProvidedCount(-1));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(
            () => new CatalogEnrollmentNotProvidedCount(-1));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(
            () => new CatalogInstructorUnconfirmedCount(-1));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(
            () => new CatalogMultiInstructorDisplayCount(-1));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(
            () => new CatalogSourceRemarkLookupOnlyCount(-1));
    }

    [TestMethod]
    public void CountAggregatesRejectDefaultPositiveCountValues()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(
            () => new CatalogDocumentCounts(
                default(CatalogCourseCount),
                new CatalogOfferingCount(2),
                new CatalogScheduledOfferingCount(1),
                new CatalogMeetingNotProvidedCount(1)));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(
            () => new CatalogIndexCounts(
                new CatalogCourseCount(1),
                default(CatalogOfferingCount)));
    }

    [TestMethod]
    public void TextValueObjectsRejectBlankValues()
    {
        Assert.ThrowsExactly<ArgumentException>(() => new CatalogSourceLogicalFileName(" "));
        Assert.ThrowsExactly<ArgumentException>(() => new CatalogFileExtension(" "));
        Assert.ThrowsExactly<ArgumentException>(() => new CatalogMediaType(" "));
        Assert.ThrowsExactly<ArgumentException>(() => new CatalogCharset(" "));
        Assert.ThrowsExactly<ArgumentException>(() => new CatalogDecoderName(" "));
        Assert.ThrowsExactly<ArgumentException>(() => new CatalogContentEncoding(" "));
        Assert.ThrowsExactly<ArgumentException>(() => new CatalogConverterId(" "));
        Assert.ThrowsExactly<ArgumentException>(() => new CatalogManualReviewSourceValue(" "));
    }

    [TestMethod]
    public void ConverterVersionAndManualReviewSourcePreserveParsedMeaning()
    {
        Version parsedVersion = new Version(1, 2, 3);
        CatalogConverterVersion converterVersion = new CatalogConverterVersion(parsedVersion);
        CatalogManualReviewSourceValue sourceValue = new CatalogManualReviewSourceValue(
            "  source text  ");

        Assert.AreEqual(parsedVersion, converterVersion.Value);
        Assert.AreEqual("  source text  ", sourceValue.Value);
    }

    private static void assertConstructorParameterTypes(Type modelType, Type[] expectedTypes)
    {
        ConstructorInfo[] constructors = modelType.GetConstructors();
        Assert.HasCount(1, constructors);

        ParameterInfo[] parameters = constructors[0].GetParameters();
        Assert.HasCount(expectedTypes.Length, parameters);
        for (int parameterIndex = 0; parameterIndex < parameters.Length; ++parameterIndex)
        {
            Assert.AreEqual(expectedTypes[parameterIndex], parameters[parameterIndex].ParameterType);
        }
    }

    private static void assertPropertyType(
        Type modelType,
        string propertyName,
        Type expectedType)
    {
        PropertyInfo? propertyOrNull = modelType.GetProperty(propertyName);
        if (propertyOrNull == null)
        {
            Assert.Fail("Expected property was not found: " + modelType.Name + "." + propertyName);
            return;
        }

        Assert.AreEqual(expectedType, propertyOrNull.PropertyType);
    }
}
