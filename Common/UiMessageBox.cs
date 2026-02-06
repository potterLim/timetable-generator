using System;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace TimetableGenerator
{
    // Message formatting helpers used to keep UI messages consistent and readable.
    public static class UiMessageBox
    {
        private const string LABEL_LINE_NUMBER = "라인 번호";
        private const string LABEL_FILE = "파일";
        private const string LABEL_LOCATION = "저장 위치";
        private const string LABEL_DETAILS = "자세한 정보";

        public static void ShowInfo(string message, string title)
        {
            MessageBox.Show(message, title, MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        public static void ShowWarning(string message, string title)
        {
            MessageBox.Show(message, title, MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        public static void ShowError(string message, string title)
        {
            MessageBox.Show(message, title, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        public static string BuildLocationMessage(string mainMessage, string path)
        {
            string normalized = NormalizePath(path);

            string[] details = new[]
            {
                BuildLabelValue(LABEL_LOCATION, normalized)
            };

            return BuildDetailMessage(mainMessage, details, null, null);
        }

        public static string BuildFilePathMessage(string mainMessage, string path)
        {
            string normalized = NormalizePath(path);

            string[] details = new[]
            {
                BuildLabelValue(LABEL_FILE, normalized)
            };

            return BuildDetailMessage(mainMessage, details, null, null);
        }

        public static string BuildErrorMessage(string mainMessage, string errorMessage)
        {
            string detail = errorMessage ?? string.Empty;

            string[] details = new[]
            {
                BuildLabelValue(LABEL_DETAILS, detail)
            };

            return BuildDetailMessage(mainMessage, details, null, null);
        }

        public static string BuildLineNumberDetail(int lineNumber)
        {
            return BuildLabelValue(LABEL_LINE_NUMBER, lineNumber.ToString());
        }

        public static string BuildParagraphMessage(params string[] paragraphs)
        {
            if (paragraphs == null || paragraphs.Length == 0)
            {
                return string.Empty;
            }

            StringBuilder messageBuilder = new StringBuilder();

            bool hasWrittenParagraph = false;
            for (int i = 0; i < paragraphs.Length; ++i)
            {
                string paragraph = paragraphs[i];
                if (string.IsNullOrWhiteSpace(paragraph))
                {
                    continue;
                }

                if (hasWrittenParagraph)
                {
                    messageBuilder.Append(Environment.NewLine);
                    messageBuilder.Append(Environment.NewLine);
                }

                messageBuilder.Append(paragraph.Trim());
                hasWrittenParagraph = true;
            }

            return messageBuilder.ToString();
        }

        // Format:
        // [mainMessage]
        //
        // [detail line 1]
        // [detail line 2]
        //
        // [rawTitle]:
        // [rawValue]
        public static string BuildDetailMessage(string mainMessage, string[] details, string rawTitle, string rawValue)
        {
            StringBuilder messageBuilder = new StringBuilder();

            if (!string.IsNullOrWhiteSpace(mainMessage))
            {
                messageBuilder.Append(mainMessage.Trim());
            }

            appendDetailsBlock(messageBuilder, details);
            appendRawBlock(messageBuilder, rawTitle, rawValue);

            return messageBuilder.ToString();
        }

        // Normalizes a path for display only (not for IO).
        public static string NormalizePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return string.Empty;
            }

            string trimmed = path.Trim();
            return trimmed.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }

        public static string BuildLabelValue(string label, string value)
        {
            string safeLabel = label ?? string.Empty;
            string safeValue = value ?? string.Empty;
            return safeLabel + ": " + safeValue;
        }

        private static void appendDetailsBlock(StringBuilder messageBuilder, string[] details)
        {
            if (details == null || details.Length == 0)
            {
                return;
            }

            bool hasWrittenDetail = false;

            for (int i = 0; i < details.Length; ++i)
            {
                string line = details[i];

                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                if (!hasWrittenDetail)
                {
                    messageBuilder.Append(Environment.NewLine);
                    messageBuilder.Append(Environment.NewLine);
                }
                else
                {
                    messageBuilder.Append(Environment.NewLine);
                }

                messageBuilder.Append(line.Trim());
                hasWrittenDetail = true;
            }
        }

        private static void appendRawBlock(StringBuilder messageBuilder, string rawTitle, string rawValue)
        {
            if (string.IsNullOrWhiteSpace(rawTitle))
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(rawValue))
            {
                return;
            }

            messageBuilder.Append(Environment.NewLine);
            messageBuilder.Append(Environment.NewLine);
            messageBuilder.Append(rawTitle.Trim());
            messageBuilder.Append(":");
            messageBuilder.Append(Environment.NewLine);
            messageBuilder.Append(rawValue);
        }
    }
}
