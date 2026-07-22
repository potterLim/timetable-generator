using System;
using System.Globalization;
using System.Text;
using TimetableGenerator.CatalogJson;
using TimetableGenerator.Domain.Catalogs;
using TimetableGenerator.Infrastructure.Catalogs;

namespace TimetableGenerator.Infrastructure.Tests.Catalogs;

internal static class CatalogSynchronizationTestDocuments
{
    public const string VALID_RELATIVE_PATH = "handong-global-university/2026-2/catalog-r0001.json";

    public static byte[] CreateValidCatalogBytes()
    {
        string json = """
            {
              "documentType": "courseCatalog",
              "schemaVersion": 1,
              "catalogId": "handong-global-university:2026-2:r0001",
              "revision": 1,
              "institution": {
                "id": "handong-global-university",
                "name": {
                  "ko": "한동대학교",
                  "en": "Handong Global University"
                }
              },
              "term": {
                "id": "2026-2",
                "academicYear": 2026,
                "semester": 2
              },
              "source": {
                "providerId": "handong-global-university",
                "logicalFileName": "hgu-2026-2-source.xls",
                "declaredExtension": "xls",
                "detectedMediaType": "text/html",
                "declaredCharset": "ks_c_5601-1987",
                "decodedWith": "windows-949",
                "sizeBytes": 100,
                "sha256": "0000000000000000000000000000000000000000000000000000000000000000"
              },
              "converter": {
                "id": "handong-course-catalog-importer",
                "version": "1.0.0"
              },
              "counts": {
                "courses": 1,
                "offerings": 2,
                "scheduledOfferings": 1,
                "meetingNotProvided": 1
              },
              "dataQuality": {
                "scheduleNormalizationSource": "koreanPeriodText",
                "sourceEnglishScheduleMismatch": 0,
                "roomNotProvided": 1,
                "enrollmentNotProvided": 1,
                "instructorUnconfirmed": 1,
                "multiInstructorDisplay": 1,
                "sourceRemarkLookupOnly": 1,
                "manualReview": []
              },
              "courses": [
                {
                  "courseId": "handong-global-university:CSE00001",
                  "code": "CSE00001",
                  "name": {
                    "ko": "자료구조",
                    "en": "Data Structures"
                  },
                  "credits": 3.0
                }
              ],
              "offerings": [
                {
                  "offeringId": "handong-global-university:2026-2:CSE00001:01",
                  "courseId": "handong-global-university:CSE00001",
                  "sectionCode": "01",
                  "requirementType": "majorRequired",
                  "offeringUnitName": "전산전자공학부",
                  "instructionSession": "daytime",
                  "instructorAssignment": {
                    "status": "confirmed",
                    "displayText": "홍길동 외 1명",
                    "additionalInstructorCount": 1
                  },
                  "schedule": {
                    "status": "scheduled",
                    "sourceTextKo": "월1",
                    "slots": [
                      {
                        "day": "monday",
                        "period": 1
                      }
                    ]
                  },
                  "location": {
                    "status": "assigned",
                    "displayText": "오석관 301"
                  },
                  "seatCapacity": 30,
                  "currentEnrollment": 20,
                  "englishInstructionPercentage": 50,
                  "generalEducationCategory": "전문교양",
                  "grading": {
                    "type": "letter",
                    "passFailOptionAvailable": true
                  },
                  "details": {
                    "syllabusUrl": null,
                    "remarksAvailable": true
                  },
                  "sourceRecordNumber": 2
                },
                {
                  "offeringId": "handong-global-university:2026-2:CSE00001:02",
                  "courseId": "handong-global-university:CSE00001",
                  "sectionCode": "02",
                  "requirementType": "freeElective",
                  "offeringUnitName": "전산전자공학부",
                  "instructionSession": "evening",
                  "instructorAssignment": {
                    "status": "unconfirmed",
                    "displayText": null,
                    "additionalInstructorCount": null
                  },
                  "schedule": {
                    "status": "notProvided",
                    "sourceTextKo": null,
                    "slots": []
                  },
                  "location": {
                    "status": "notProvided",
                    "displayText": null
                  },
                  "seatCapacity": 0,
                  "currentEnrollment": null,
                  "englishInstructionPercentage": 0,
                  "generalEducationCategory": null,
                  "grading": {
                    "type": "passFail",
                    "passFailOptionAvailable": false
                  },
                  "details": {
                    "syllabusUrl": null,
                    "remarksAvailable": false
                  },
                  "sourceRecordNumber": 3
                }
              ]
            }
            """;
        return Encoding.UTF8.GetBytes(json);
    }

    public static byte[] CreateCatalogBytesWithKoreanName(string koreanName)
    {
        return Replace(CreateValidCatalogBytes(), "자료구조", koreanName);
    }

    public static byte[] CreateValidIndexBytes(byte[] catalogBytes)
    {
        if (catalogBytes == null)
        {
            throw new ArgumentNullException(nameof(catalogBytes));
        }

        return CreateIndexBytes(
            VALID_RELATIVE_PATH,
            new CatalogFileSize(catalogBytes.LongLength),
            Sha256Digest.Compute(catalogBytes));
    }

    public static byte[] CreateIndexBytes(
        string relativePath,
        CatalogFileSize fileSize,
        Sha256Digest sha256)
    {
        string json = $$"""
            {
              "documentType": "courseCatalogIndex",
              "schemaVersion": 1,
              "defaultCatalogId": "handong-global-university:2026-2:r0001",
              "catalogs": [
                {
                  "catalogId": "handong-global-university:2026-2:r0001",
                  "catalogSchemaVersion": 1,
                  "institution": {
                    "id": "handong-global-university",
                    "name": {
                      "ko": "한동대학교",
                      "en": "Handong Global University"
                    }
                  },
                  "term": {
                    "id": "2026-2",
                    "academicYear": 2026,
                    "semester": 2
                  },
                  "revision": 1,
                  "file": {
                    "relativePath": "{{relativePath}}",
                    "mediaType": "application/json",
                    "charset": "utf-8",
                    "contentEncoding": "identity",
                    "sizeBytes": {{fileSize.Value}},
                    "sha256": "{{sha256.HexValue}}"
                  },
                  "counts": {
                    "courses": 1,
                    "offerings": 2
                  }
                }
              ]
            }
            """;
        return Encoding.UTF8.GetBytes(json);
    }

    public static VerifiedCatalogPackage CreateVerifiedPackage()
    {
        byte[] catalogBytes = CreateValidCatalogBytes();
        byte[] indexBytes = CreateValidIndexBytes(catalogBytes);
        return VerifiedCatalogPackage.ReadAndVerify(indexBytes, catalogBytes);
    }

    public static VerifiedCatalogPackage CreateVerifiedPackageWithKoreanName(string koreanName)
    {
        byte[] catalogBytes = CreateCatalogBytesWithKoreanName(koreanName);
        byte[] indexBytes = CreateValidIndexBytes(catalogBytes);
        return VerifiedCatalogPackage.ReadAndVerify(indexBytes, catalogBytes);
    }

    public static VerifiedCatalogPackage CreateVerifiedPackageWithRevision(
        CatalogRevision revision,
        string koreanName)
    {
        if (revision.IsValid == false)
        {
            throw new ArgumentException("Test catalog packages require a valid revision.", nameof(revision));
        }

        byte[] catalogBytes = CreateCatalogBytesWithKoreanName(koreanName);
        catalogBytes = Replace(catalogBytes, "r0001", revision.FileComponent);
        catalogBytes = Replace(
            catalogBytes,
            "\"revision\": 1",
            "\"revision\": " + revision.Value.ToString(CultureInfo.InvariantCulture));

        byte[] indexBytes = CreateValidIndexBytes(catalogBytes);
        indexBytes = Replace(indexBytes, "r0001", revision.FileComponent);
        indexBytes = Replace(
            indexBytes,
            "\"revision\": 1",
            "\"revision\": " + revision.Value.ToString(CultureInfo.InvariantCulture));
        return VerifiedCatalogPackage.ReadAndVerify(indexBytes, catalogBytes);
    }

    public static byte[] Replace(byte[] sourceBytes, string oldValue, string newValue)
    {
        string sourceJson = Encoding.UTF8.GetString(sourceBytes);
        string replacedJson = sourceJson.Replace(oldValue, newValue, StringComparison.Ordinal);
        if (string.Equals(sourceJson, replacedJson, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("The requested JSON test mutation did not match.");
        }

        return Encoding.UTF8.GetBytes(replacedJson);
    }
}
