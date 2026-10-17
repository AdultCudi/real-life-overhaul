using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using RealLifeOverhaul.Leisure;

// Leisure's reaches in metres (Leisure/LeisureReach.cs) against the square of a flat distance, which CitySpots.Flat gives;
// and over the mod's sources, no square of a flat distance compared with plain metres.
internal static partial class Program
{
    private static void LeisureReaches()
    {
        // The chess table is put out 30 to 70 m off, out of view, and stays while the player is no farther than 140 m.
        bool kept = true;
        for (float d = LeisureReach.ChessFrom; d <= LeisureReach.ChessTo; d += 2.5f)
        {
            kept &= !LeisureReach.Beyond(Square(d * 0.6f, d * 0.8f), LeisureReach.ChessKept);
        }
        Check(kept, "leisure: a chess table put out 30 to 70 m off stays out while the player stays there");
        Check(!LeisureReach.Beyond(Square(0f, 139f), LeisureReach.ChessKept) && LeisureReach.Beyond(Square(0f, 141f), LeisureReach.ChessKept),
            "leisure: the chess table is taken in once the player is 140 m off");

        // The test button's table 3.5 m ahead: offered from where the player stands if the stools face them, and
        // playable from there whichever way the pavement turns the table.
        float facing = LeisureReach.ChessTestAhead - LeisureReach.StoolOut;
        float across = LeisureReach.ChessTestAhead;
        Check(LeisureReach.Within(Square(0f, facing), LeisureReach.ChessOffered), "leisure: the test table's stool, 2.9 m off, is offered at once");
        Check(LeisureReach.Within(Square(LeisureReach.StoolOut, across), LeisureReach.ChessStarts), "leisure: a game can start from 3.6 m off the stool");
        Check(!LeisureReach.Within(Square(0f, 4.1f), LeisureReach.ChessStarts), "leisure: a game is refused from 4.1 m off the stool");

        // An activity begun at the edge of its reach is not ended at once as walked away from.
        float chessFarthest = LeisureReach.ChessStarts + LeisureReach.StoolOut;
        Check(!LeisureReach.Beyond(Square(chessFarthest, 0f), LeisureReach.ChessLeash), "leisure: chess begun 4 m from the stool is inside its 7 m leash");
        Check(!LeisureReach.Beyond(Square(LeisureReach.BarOffered, 0f), LeisureReach.BarLeash), "leisure: a drink begun 2.8 m from the bar is inside its 4.5 m leash");
        Check(!LeisureReach.Beyond(Square(1.5f, 1.5f), LeisureReach.DanceLeash) && LeisureReach.Beyond(Square(3.1f, 0f), LeisureReach.DanceLeash),
            "leisure: a dance ends once the player is 3 m from where it began, not 2");
        Check(!LeisureReach.Beyond(Square(0f, 2.5f), LeisureReach.StandingLeash) && !LeisureReach.Beyond(Square(0f, 5.5f), LeisureReach.Leash)
              && LeisureReach.Beyond(Square(0f, 6.5f), LeisureReach.Leash), "leisure: the standing leash is 3 m and the usual one 6 m");

        // The last step: the stool and the bar's place are taken from within what the body is brought on.
        Check(LeisureReach.ChessSeated <= LeisureReach.Settles && LeisureReach.BarStepUp <= LeisureReach.Settles,
            "leisure: the chess stool and the bar are sat or stood at from within the last step allowed");
        Check(LeisureReach.Within(Square(0.45f, 0f), LeisureReach.ChessSeated) && !LeisureReach.Within(Square(0.6f, 0f), LeisureReach.ChessSeated)
              && !LeisureReach.Beyond(Square(0.55f, 0f), LeisureReach.Settles) && LeisureReach.Beyond(Square(0.7f, 0f), LeisureReach.Settles),
            "leisure: seated from half a metre, brought on at most 0.6 m");

        // Night life: a door found within 75 m keeps its two people until the player is 120 m off.
        Check(!LeisureReach.Beyond(Square(45f, 60f), LeisureReach.NightKept) && LeisureReach.Beyond(Square(0f, 121f), LeisureReach.NightKept),
            "leisure: the night's two outside a door 75 m off stay, and go at 120 m");
        Check(!LeisureReach.Within(Square(0.8f, 0f), LeisureReach.NightApart) && LeisureReach.Within(Square(0.7f, 0f), LeisureReach.NightApart),
            "leisure: the night's two stand at least 0.75 m apart");

        // The rest of what is offered where.
        Check(LeisureReach.Within(Square(0f, 5f), LeisureReach.ApiPlace) && !LeisureReach.Within(Square(0f, 6.5f), LeisureReach.ApiPlace),
            "leisure: a place another feature names is taken within 6 m");
        Check(LeisureReach.Within(Square(0f, 2.5f), LeisureReach.RackOffered) && LeisureReach.Within(Square(2f, 0f), LeisureReach.RackAway)
              && !LeisureReach.Within(Square(3f, 0f), LeisureReach.RackAway), "leisure: the rack is offered from 2.5 m and stood 2.5 m or more from the player");
        Check(LeisureReach.FishingLandmark == 320f && LeisureReach.ChessKept > LeisureReach.ChessTo && LeisureReach.NightKept > LeisureReach.NightNear,
            "leisure: fishing within 320 m of a waterside landmark; what is put out stays farther than it was put");

        FlatSquares();
    }

    // The square of a flat distance, as CitySpots.Flat gives it.
    private static float Square(float x, float z) => x * x + z * z;

    // Over every source of the mod: a call to CitySpots.Flat or CivicCity.Flat compared straight away is compared with a
    // square (a product), another such call, or a value whose name or declaration says it is one.
    private static void FlatSquares()
    {
        string root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\..\Assets\Mods\RealLifeOverhaul\Scripts"));
        var call = new Regex(@"\b(?:CitySpots|CivicCity)\.Flat\(");
        int compared = 0;
        var plain = new List<string>();
        foreach (string file in Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories))
        {
            string code = Code(File.ReadAllText(file));
            foreach (Match m in call.Matches(code))
            {
                int end = Closing(code, m.Index + m.Length - 1);
                string after = ComparedAfter(code, end);
                string before = ComparedBefore(code, m.Index);
                foreach (string other in new[] { after, before })
                {
                    if (other == null)
                    {
                        continue;
                    }
                    compared++;
                    if (!SquaredOperand(code, other))
                    {
                        plain.Add(Path.GetFileName(file) + ":" + (Count(code, m.Index) + 1) + " against '" + other + "'");
                    }
                }
            }
        }
        Check(compared > 100 && plain.Count == 0, "sources: every square of a flat distance (" + compared + " compared) is compared with a square"
                                                  + (plain.Count > 0 ? "; not: " + string.Join(", ", plain) : string.Empty));
    }

    private static int Closing(string code, int open)
    {
        int depth = 0;
        for (int i = open; i < code.Length; i++)
        {
            depth += code[i] == '(' ? 1 : code[i] == ')' ? -1 : 0;
            if (depth == 0)
            {
                return i;
            }
        }
        return code.Length - 1;
    }

    private static readonly string[] Comparisons = { "<=", ">=", "<", ">" };

    // The operand after a comparison that follows the call, up to the end of that operand; null if none follows.
    private static string ComparedAfter(string code, int end)
    {
        int i = end + 1;
        while (i < code.Length && char.IsWhiteSpace(code[i]))
        {
            i++;
        }
        foreach (string op in Comparisons)
        {
            if (string.CompareOrdinal(code, i, op, 0, op.Length) == 0 && (i + op.Length >= code.Length || "<>=".IndexOf(code[i + op.Length]) < 0))
            {
                int from = i + op.Length, depth = 0, j = from;
                for (; j < code.Length; j++)
                {
                    char c = code[j];
                    if (c == '(' || c == '[')
                    {
                        depth++;
                    }
                    else if (c == ')' || c == ']')
                    {
                        if (depth-- == 0)
                        {
                            break;
                        }
                    }
                    else if (depth == 0 && (c == ';' || c == ',' || c == '?' || c == ':' || (c == '&' && code[j + 1] == '&') || (c == '|' && code[j + 1] == '|')))
                    {
                        break;
                    }
                }
                return code.Substring(from, j - from).Trim();
            }
        }
        return null;
    }

    // The operand before a comparison that precedes the call; null if none does.
    private static string ComparedBefore(string code, int start)
    {
        int i = start - 1;
        while (i >= 0 && char.IsWhiteSpace(code[i]))
        {
            i--;
        }
        foreach (string op in Comparisons)
        {
            int at = i - op.Length + 1;
            if (at > 0 && string.CompareOrdinal(code, at, op, 0, op.Length) == 0 && "<>=".IndexOf(code[at - 1]) < 0)
            {
                int to = at - 1, depth = 0, j = to;
                for (; j >= 0; j--)
                {
                    char c = code[j];
                    if (c == ')' || c == ']')
                    {
                        depth++;
                    }
                    else if (c == '(' || c == '[')
                    {
                        if (depth-- == 0)
                        {
                            break;
                        }
                    }
                    else if (depth == 0 && (c == ';' || c == ',' || c == '?' || c == ':' || c == '=' || c == '&' || c == '|'))
                    {
                        break;
                    }
                }
                return code.Substring(j + 1, to - j).Trim();
            }
        }
        return null;
    }

    // A square: a product, another flat square, a name that says so, or a name declared from a product in the same file.
    private static bool SquaredOperand(string code, string operand)
    {
        if (operand.Contains("*") || operand.Contains("Flat(") || operand.IndexOf("sqr", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return true;
        }
        Match name = Regex.Match(operand, @"^[A-Za-z_][A-Za-z0-9_]*$");
        return name.Success && Regex.IsMatch(code, @"\b" + operand + @"\s*=\s*[^;=]*\*");
    }

    private static int Count(string code, int upTo)
    {
        int lines = 0;
        for (int i = 0; i < upTo; i++)
        {
            lines += code[i] == '\n' ? 1 : 0;
        }
        return lines;
    }
}
