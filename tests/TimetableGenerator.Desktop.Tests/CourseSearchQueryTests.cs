using TimetableGenerator.Desktop.Presentation.Models;

using Xunit;

namespace TimetableGenerator.Desktop.Tests;

public sealed class CourseSearchQueryTests
{
    [Fact]
    public void ExactMatchingIgnoresCaseAndEveryUnicodeWhitespacePosition()
    {
        CourseSearchQuery query = CourseSearchQuery.Create("  cOmPuTeR\u00A0\u2003aRcHiTeCtUrE  ");

        Assert.True(query.IsExactMatch("Computer Architecture"));
        Assert.True(query.IsExactMatch("ComputerArchitecture"));
        Assert.True(query.IsExactMatch("  Computer   Architecture  "));
    }

    [Fact]
    public void ExactMatchingSupportsKoreanTitlesAndCourseCodes()
    {
        CourseSearchQuery koreanTitleQuery = CourseSearchQuery.Create("컴 퓨 터\u202F구 조");
        CourseSearchQuery courseCodeQuery = CourseSearchQuery.Create("e c e 2 0 0 2 1");

        Assert.True(koreanTitleQuery.IsExactMatch("컴퓨터구조"));
        Assert.True(courseCodeQuery.IsExactMatch("ECE20021"));
    }

    [Fact]
    public void SearchKeepsOrderSpellingAndPunctuationSignificant()
    {
        CourseSearchQuery misspelledQuery = CourseSearchQuery.Create("Computor");
        CourseSearchQuery reorderedQuery = CourseSearchQuery.Create("Architecture Computer");
        CourseSearchQuery punctuationQuery = CourseSearchQuery.Create("Computer-Architecture");

        Assert.False(misspelledQuery.IsContainedIn("Computer Architecture"));
        Assert.False(reorderedQuery.IsContainedIn("Computer Architecture"));
        Assert.False(punctuationQuery.IsContainedIn("Computer Architecture"));
    }

    [Fact]
    public void UnicodeWhitespaceOnlyQueryIsEmpty()
    {
        CourseSearchQuery query = CourseSearchQuery.Create(" \t\r\n\u00A0\u2003\u202F ");

        Assert.True(query.IsEmpty);
    }
}
