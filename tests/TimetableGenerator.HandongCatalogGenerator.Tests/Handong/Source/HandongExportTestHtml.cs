using System;
using System.Collections.Generic;
using System.Text;

namespace TimetableGenerator.HandongCatalogGenerator.Tests.Handong.Source;

internal static class HandongExportTestHtml
{
    private static readonly IReadOnlyList<string> HEADER_CELLS = new string[]
    {
        "<td>구<br>분</td>",
        "<td>과목코드</td>",
        "<td>분<br>반</td>",
        "<td>과목명<br>(CourseName)</td>",
        "<td>학<br>점</td>",
        "<td>개설정보</td>",
        "<td>시간<br>(Period)</td>",
        "<td>강의실</td>",
        "<td>정원</td>",
        "<td>인원</td>",
        "<td>영어</td>",
        "<td>교양<br>실무</td>",
        "<td>성적<br>유형</td>",
        "<td>PF<br>병행</td>",
        "<td>강의<br>계획서</td>",
        "<td>비고</td>",
    };

    private static readonly IReadOnlyList<string> OFFERING_CELLS = new string[]
    {
        "<td>교선필</td>",
        "<td>GCS10001</td>",
        "<td>01</td>",
        "<td>소프트웨어 입문<br>(Introduction to Programming)</td>",
        "<td>2</td>",
        "<td>GLS&nbsp;주간<br><font color=\"blue\">테스트 담당자</font></td>",
        "<td>화5,금5<br>Tue5,Fri5<br><br><br></td>",
        "<td>HDH 403&nbsp;</td>",
        "<td>45</td>",
        "<td>&nbsp;</td>",
        "<td>0%</td>",
        "<td>프로그래밍과정&nbsp;</td>",
        "<td>A+&nbsp;</td>",
        "<td>Y&nbsp;</td>",
        "<td>&nbsp;</td>",
        "<td><a href=\"javascript:senditpop('course-popup.php?" +
            "kang_gwamok_code=GCS10001&amp;kang_bunban=01&amp;" +
            "kang_yy=2026&amp;kang_hakgi=2')\">조회</a></td>",
    };

    public static string Create()
    {
        return createDocument(HEADER_CELLS, OFFERING_CELLS);
    }

    public static string CreateWithCourseCodeHeader(string courseCodeHeaderCell)
    {
        if (courseCodeHeaderCell == null)
        {
            throw new ArgumentNullException(nameof(courseCodeHeaderCell));
        }

        List<string> headerCells = new List<string>(HEADER_CELLS);
        headerCells[1] = courseCodeHeaderCell;
        return createDocument(headerCells, OFFERING_CELLS);
    }

    public static string CreateWithOfferingColumnCount(int offeringColumnCount)
    {
        if (offeringColumnCount < 0 || offeringColumnCount > OFFERING_CELLS.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(offeringColumnCount));
        }

        List<string> offeringCells = new List<string>(offeringColumnCount);
        for (int columnIndex = 0;
            columnIndex < offeringColumnCount;
            ++columnIndex)
        {
            offeringCells.Add(OFFERING_CELLS[columnIndex]);
        }

        return createDocument(HEADER_CELLS, offeringCells);
    }

    private static string createDocument(
        IReadOnlyList<string> headerCells,
        IReadOnlyList<string> offeringCells)
    {
        StringBuilder documentBuilder = new StringBuilder();
        documentBuilder.AppendLine("<html>");
        documentBuilder.AppendLine("<head>");
        documentBuilder.AppendLine(
            "<meta http-equiv=\"Content-Type\" " +
            "content=\"text/html; charset=ks_c_5601-1987\">");
        documentBuilder.AppendLine("<title>교직원정보시스템</title>");
        documentBuilder.AppendLine("</head>");
        documentBuilder.AppendLine("<body>");
        documentBuilder.AppendLine(
            "Warning: ociexecute(): ORA-00923 in " +
            "/srv/example/app/config.php on line 409");
        documentBuilder.AppendLine("<table>");
        appendRow(documentBuilder, headerCells);
        appendRow(documentBuilder, offeringCells);
        documentBuilder.AppendLine("</table>");
        documentBuilder.AppendLine("</body>");
        documentBuilder.AppendLine("</html>");
        return documentBuilder.ToString();
    }

    private static void appendRow(StringBuilder documentBuilder, IReadOnlyList<string> cells)
    {
        documentBuilder.AppendLine("<tr>");
        foreach (string cell in cells)
        {
            documentBuilder.AppendLine(cell);
        }

        documentBuilder.AppendLine("</tr>");
    }
}
