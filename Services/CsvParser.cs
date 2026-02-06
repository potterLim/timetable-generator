using System.Collections.Generic;
using System.Text;

namespace TimetableGenerator
{
    public static class CsvParser
    {
        public static bool TryParseLine(string line, out List<string> fields)
        {
            fields = null;

            if (line == null)
            {
                return false;
            }

            List<string> result = new List<string>();
            StringBuilder token = new StringBuilder();

            bool inQuotes = false;
            int i = 0;

            while (i < line.Length)
            {
                char c = line[i];

                if (inQuotes)
                {
                    if (c == '"')
                    {
                        if (i + 1 < line.Length && line[i + 1] == '"')
                        {
                            token.Append('"');
                            i += 2;
                            continue;
                        }

                        inQuotes = false;
                        ++i;
                        continue;
                    }

                    token.Append(c);
                    ++i;
                    continue;
                }

                if (c == ',')
                {
                    result.Add(token.ToString());
                    token.Clear();
                    ++i;
                    continue;
                }

                if (c == '"')
                {
                    inQuotes = true;
                    ++i;
                    continue;
                }

                token.Append(c);
                ++i;
            }

            if (inQuotes)
            {
                return false;
            }

            result.Add(token.ToString());
            fields = result;
            return true;
        }
    }
}
