using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using TimetableGenerator.Infrastructure.Csv;

namespace TimetableGenerator.UI.Product;

internal static class CourseImportDiagnosticTextFormatter
{
    private const int MAXIMUM_VISIBLE_DIAGNOSTIC_COUNT = 5;

    internal static string formatDiagnostics(
        IReadOnlyList<CourseImportDiagnostic> diagnostics,
        EDiagnosticCollectionCompletion collectionCompletion)
    {
        if (diagnostics == null)
        {
            throw new ArgumentNullException(nameof(diagnostics));
        }

        if (diagnostics.Count == 0)
        {
            throw new ArgumentException("At least one import diagnostic is required.", nameof(diagnostics));
        }

        StringBuilder messageBuilder = new StringBuilder();
        int visibleDiagnosticCount = Math.Min(
            diagnostics.Count,
            MAXIMUM_VISIBLE_DIAGNOSTIC_COUNT);

        for (int diagnosticIndex = 0;
            diagnosticIndex < visibleDiagnosticCount;
            ++diagnosticIndex)
        {
            if (diagnosticIndex > 0)
            {
                messageBuilder.AppendLine();
            }

            CourseImportDiagnostic diagnostic = diagnostics[diagnosticIndex];
            messageBuilder.Append("• ");
            messageBuilder.Append(formatSourcePosition(diagnostic));
            messageBuilder.Append(findUserMessage(diagnostic.ErrorCode));
        }

        int hiddenDiagnosticCount = diagnostics.Count - visibleDiagnosticCount;
        bool hasMoreDiagnostics = hiddenDiagnosticCount > 0 ||
            collectionCompletion == EDiagnosticCollectionCompletion.MaximumCountReached;
        if (hasMoreDiagnostics)
        {
            messageBuilder.AppendLine();
            if (hiddenDiagnosticCount > 0)
            {
                messageBuilder.Append("• 그 밖의 문제 ");
                messageBuilder.Append(hiddenDiagnosticCount.ToString(CultureInfo.CurrentCulture));
                messageBuilder.Append("개가 더 있습니다.");
            }
            else
            {
                messageBuilder.Append("• 표시 한도보다 많은 문제가 있습니다.");
            }
        }

        return messageBuilder.ToString();
    }

    private static string formatSourcePosition(CourseImportDiagnostic diagnostic)
    {
        if (diagnostic == null)
        {
            throw new ArgumentNullException(nameof(diagnostic));
        }

        if (diagnostic.SourcePosition.HasRowNumber == false)
        {
            return string.Empty;
        }

        CsvRowNumber rowNumber = diagnostic.SourcePosition.GetRowNumber();
        return rowNumber.Value.ToString(CultureInfo.CurrentCulture) + "행 · " +
            findColumnDisplayName(diagnostic.Column) + ": ";
    }

    private static string findColumnDisplayName(ECsvColumn column)
    {
        switch (column)
        {
            case ECsvColumn.Header:
                return "헤더";
            case ECsvColumn.Record:
                return "행";
            case ECsvColumn.CourseChoiceGroupId:
                return "CourseId";
            case ECsvColumn.CourseSectionCode:
                return "Section";
            case ECsvColumn.CourseName:
                return "Name";
            case ECsvColumn.ScheduleSlots:
                return "TimeSlots";
            case ECsvColumn.ClassroomLocation:
                return "Classroom";
            case ECsvColumn.File:
                return "파일";
            default:
                Debug.Fail("Unexpected CSV column: " + column);
                return "데이터";
        }
    }

    private static string findUserMessage(ECourseImportErrorCode errorCode)
    {
        switch (errorCode)
        {
            case ECourseImportErrorCode.InvalidInputFilePath:
                return "CSV 파일 경로가 올바르지 않습니다.";
            case ECourseImportErrorCode.FileNotFound:
                return "선택한 파일을 찾을 수 없습니다.";
            case ECourseImportErrorCode.FileAccessDenied:
                return "파일을 읽을 권한이 없습니다.";
            case ECourseImportErrorCode.FileReadFailed:
                return "파일을 읽는 동안 문제가 발생했습니다.";
            case ECourseImportErrorCode.InvalidUtf8Encoding:
                return "파일을 UTF-8 형식으로 저장해 주세요.";
            case ECourseImportErrorCode.MissingHeader:
                return "CSV 헤더가 없습니다.";
            case ECourseImportErrorCode.InvalidHeader:
                return "헤더를 CourseId,Section,Name,TimeSlots[,Classroom] 순서로 작성해 주세요.";
            case ECourseImportErrorCode.MalformedCsvRecord:
                return "쉼표와 따옴표 구성이 올바르지 않습니다.";
            case ECourseImportErrorCode.InvalidColumnCount:
                return "헤더와 데이터 열 개수가 다릅니다.";
            case ECourseImportErrorCode.InvalidCourseChoiceGroupId:
                return "CourseId에는 1 이상의 정수를 입력해 주세요.";
            case ECourseImportErrorCode.InvalidCourseSectionCode:
                return "분반 코드를 입력해 주세요.";
            case ECourseImportErrorCode.InvalidCourseName:
                return "과목명을 입력해 주세요.";
            case ECourseImportErrorCode.EmptyScheduleSlot:
                return "수업 시간을 하나 이상 입력해 주세요.";
            case ECourseImportErrorCode.InvalidScheduleSlot:
                return "‘월요일1교시’ 형식으로 입력해 주세요.";
            case ECourseImportErrorCode.DuplicateScheduleSlot:
                return "같은 수업 시간이 중복되어 있습니다.";
            case ECourseImportErrorCode.InvalidClassroomLocation:
                return "강의실을 ‘건물명 호수’ 형식으로 입력해 주세요.";
            case ECourseImportErrorCode.NoCourseOfferings:
                return "과목 데이터가 없습니다.";
            default:
                Debug.Fail("Unexpected course import error code: " + errorCode);
                return "CSV 데이터를 확인해 주세요.";
        }
    }
}
