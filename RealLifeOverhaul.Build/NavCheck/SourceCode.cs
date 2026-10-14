using System.Text;

// The text of a C# source with its comments blanked out, for the checks that read the mod's sources (FrontSignCheck):
// kept here when the build check went to ContractCheck, which it was written for.
internal static partial class Program
{
    // The text of a C# file with its comments blanked out (strings and characters kept, so that "//" in one is not a comment).
    private static string Code(string text)
    {
        var code = new StringBuilder(text.Length);
        int i = 0;
        while (i < text.Length)
        {
            char c = text[i];
            if (c == '/' && i + 1 < text.Length && text[i + 1] == '/')
            {
                while (i < text.Length && text[i] != '\n')
                {
                    i++;
                }
                continue;
            }
            if (c == '/' && i + 1 < text.Length && text[i + 1] == '*')
            {
                i += 2;
                while (i + 1 < text.Length && !(text[i] == '*' && text[i + 1] == '/'))
                {
                    code.Append(text[i] == '\n' ? '\n' : ' ');
                    i++;
                }
                i += 2;
                continue;
            }
            if (c == '"' || c == '\'')
            {
                bool verbatim = c == '"' && i > 0 && text[i - 1] == '@';
                code.Append(c);
                i++;
                while (i < text.Length && text[i] != c && (verbatim || text[i] != '\n'))
                {
                    if (!verbatim && text[i] == '\\' && i + 1 < text.Length)
                    {
                        code.Append(text[i]).Append(text[i + 1]);
                        i += 2;
                        continue;
                    }
                    if (verbatim && text[i] == '"' && i + 1 < text.Length && text[i + 1] == '"')
                    {
                        code.Append("\"\"");
                        i += 2;
                        continue;
                    }
                    code.Append(text[i]);
                    i++;
                }
                if (i < text.Length)
                {
                    code.Append(text[i]);
                    i++;
                }
                continue;
            }
            code.Append(c);
            i++;
        }
        return code.ToString();
    }
}
