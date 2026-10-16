using System;
using System.Text.RegularExpressions;

// docs/standards/multiplayer.md: rules classes take chance and time from their callers, and only the shared city's
// link names Going Public's network.
internal static partial class Program
{
    private static readonly Regex Nondeterministic = new Regex(
        @"\bnew\s+(?:System\.)?Random\s*\(\s*\)|\bDateTime\.(?:Now|UtcNow|Today)\b|\bDateTimeOffset\.(?:Now|UtcNow)\b|\bEnvironment\.TickCount|\bGuid\.NewGuid\b|\bUnityEngine\.Random\b|\bStopwatch\.GetTimestamp\b",
        RegexOptions.Compiled);
    private static readonly Regex Network = new Regex(@"\b(?:MPServer|MPClient|MPLink|SendEnvelope|MessageEnvelope)\b", RegexOptions.Compiled);

    private static void MultiplayerChecks()
    {
        foreach (SourceFile f in Scripts)
        {
            if (RulesFile.IsMatch(f.Rel))
            {
                // RLO-MP-004: the same inputs give the same answer on every machine.
                Examine("RLO-MP-004", f.Rel);
                for (int i = 0; i < f.Code.Length; i++)
                {
                    if (Nondeterministic.IsMatch(f.Code[i]))
                    {
                        Hit("RLO-MP-004", f.Rel, i + 1, f.Code[i]);
                    }
                }
            }
            // RLO-MP-006: Going Public's connection is reached in the shared city's link and the session's reader only.
            Examine("RLO-MP-006", f.Rel);
            for (int i = 0; i < f.Bare.Length; i++)
            {
                if (Network.IsMatch(f.Bare[i]))
                {
                    Hit("RLO-MP-006", f.Rel, i + 1, f.Bare[i]);
                }
            }
        }
    }
}
