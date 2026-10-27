using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

// docs/standards/performance.md: what a frame method may do, where the whole scene may be searched, the physics and
// message calls that allocate or search by name, and the cost line every component's summary carries. A frame method
// is Update, LateUpdate or FixedUpdate, and the OnUpdate, OnLateUpdate or OnFixedUpdate body each calls inside its
// guard (docs/standards/architecture.md, RLO-ARC-007). What a frame method calls is not followed: review covers that.
internal static partial class Program
{
    private static readonly Regex FrameMethod = new Regex(@"\bvoid\s+(Update|LateUpdate|FixedUpdate|OnUpdate|OnLateUpdate|OnFixedUpdate)\s*\(\s*\)", RegexOptions.Compiled);
    private static readonly Regex EngineFrameMethod = new Regex(@"^(Update|LateUpdate|FixedUpdate)$", RegexOptions.Compiled);
    private static readonly Regex HotSpotOpening = new Regex(@"^\s*using\s+var\s+\w+\s*=\s*HotSpots\.Time\(", RegexOptions.Compiled);

    private static readonly (string What, Regex Pattern)[] Allocations =
    {
        ("a new collection", new Regex(@"\bnew\s+(?:System\.Collections\.Generic\.|System\.Text\.)?(?:List|Dictionary|HashSet|Queue|Stack|SortedList|SortedDictionary|LinkedList)\s*<|\bnew\s+(?:System\.Text\.)?StringBuilder\s*\(", RegexOptions.Compiled)),
        ("a new array", new Regex(@"\bnew\s+[A-Za-z_][\w.]*\s*\[\s*[^\]\s]|\bnew\s*(?:[A-Za-z_][\w.]*\s*)?\[\s*\]\s*\{", RegexOptions.Compiled)),
        ("a string built", new Regex("\\$@?\"|@\\$\"|\"\\s*\\+|\\+\\s*\"|\\bstring\\.(?:Format|Concat|Join)\\s*\\(|\\.ToString\\s*\\(", RegexOptions.Compiled)),
        ("a lambda or closure", new Regex(@"=>", RegexOptions.Compiled))
    };

    private static readonly (string What, Regex Pattern)[] Searches =
    {
        ("a search of the scene", new Regex(@"\b(?:FindObjectOfType|FindObjectsOfType|FindObjectsByType|FindFirstObjectByType|FindAnyObjectByType)\b|\bGameObject\.Find\w*\s*\(", RegexOptions.Compiled)),
        ("a component looked up", new Regex(@"\bGetComponents?(?:InChildren|InParent)?\s*(?:<|\(\s*typeof)", RegexOptions.Compiled)),
        ("a child found by name", new Regex("\\.Find\\s*\\(\\s*\"", RegexOptions.Compiled)),
        ("a physics query", new Regex(@"\bPhysics(?:2D)?\.(?:Raycast|SphereCast|CapsuleCast|BoxCast|Linecast|Overlap|CheckSphere|CheckBox|CheckCapsule|ComputePenetration|ClosestPoint)\w*\s*\(", RegexOptions.Compiled))
    };

    private static readonly Regex SceneSearch = new Regex(@"\b(?:FindObjectOfType|FindObjectsOfType|FindObjectsByType|FindFirstObjectByType|FindAnyObjectByType)\b|\bGameObject\.Find(?:WithTag|GameObjectsWithTag)?\s*\(", RegexOptions.Compiled);
    private static readonly Regex AllocatingPhysics = new Regex(@"\bPhysics(?:2D)?\.(?:RaycastAll|SphereCastAll|CapsuleCastAll|BoxCastAll|OverlapSphere|OverlapBox|OverlapCapsule|OverlapCircleAll|OverlapAreaAll)\s*\(", RegexOptions.Compiled);
    private static readonly Regex ByName = new Regex(
        "\\bInvokeRepeating\\s*\\(|(?<![.?\\w])(?:Invoke|CancelInvoke)\\s*\\(\\s*\"|\\b(?:SendMessage|SendMessageUpwards|BroadcastMessage|StartCoroutine|StopCoroutine)\\s*\\(\\s*\"",
        RegexOptions.Compiled);
    private static readonly Regex Component = new Regex(@":\s*(?:UnityEngine\.)?MonoBehaviour\b", RegexOptions.Compiled);
    private static readonly Regex CostWords = new Regex(@"\bframe|\bcost|\bidle\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static void PerformanceChecks()
    {
        var components = new Dictionary<string, List<(SourceFile File, SourceType Type)>>(StringComparer.Ordinal);
        foreach (SourceFile f in Scripts)
        {
            FrameMethods(f);
            Examine("RLO-PERF-005", f.Rel);
            Examine("RLO-PERF-006", f.Rel);
            Examine("RLO-PERF-010", f.Rel);
            bool finder = f.Rel.EndsWith("/Core/SceneFinds.cs", StringComparison.Ordinal);
            for (int i = 0; i < f.Code.Length; i++)
            {
                string code = f.Code[i];
                if (!finder && SceneSearch.IsMatch(code))
                {
                    Hit("RLO-PERF-005", f.Rel, i + 1, code);
                }
                if (AllocatingPhysics.IsMatch(code))
                {
                    Hit("RLO-PERF-006", f.Rel, i + 1, code);
                }
                if (ByName.IsMatch(code))
                {
                    Hit("RLO-PERF-010", f.Rel, i + 1, code);
                }
            }
            foreach (SourceType t in f.Types)
            {
                if (t.Kind == "class" && Component.IsMatch(t.Header))
                {
                    string key = t.Namespace + "." + t.Name;
                    if (!components.TryGetValue(key, out List<(SourceFile, SourceType)> parts))
                    {
                        parts = new List<(SourceFile, SourceType)>(1);
                        components[key] = parts;
                    }
                    parts.Add((f, t));
                }
            }
        }
        TickPathChecks();
        GameFontChecks();
        MapTreeChecks();
        BizPaceChecks();
        SliceChecks();
        DayChecks();
        MapChecks();
        // RLO-PERF-012: a component's summary (on any part of it) says what it costs a frame.
        foreach (KeyValuePair<string, List<(SourceFile File, SourceType Type)>> kv in components)
        {
            (SourceFile file, SourceType type) = kv.Value[0];
            Examine("RLO-PERF-012", file.Rel);
            bool said = false;
            foreach ((SourceFile File, SourceType Type) part in kv.Value)
            {
                said |= CostWords.IsMatch(part.Type.Summary);
            }
            if (!said)
            {
                Hit("RLO-PERF-012", file.Rel, type.Line + 1, type.Name + (type.Summary.Length == 0 ? ": no summary" : ": the summary does not say its cost a frame"));
            }
        }
    }

    // RLO-PERF-002 to RLO-PERF-004 and RLO-PERF-006: the bodies of the frame methods.
    private static void FrameMethods(SourceFile f)
    {
        Examine("RLO-PERF-002", f.Rel);
        Examine("RLO-PERF-003", f.Rel);
        Examine("RLO-PERF-004", f.Rel);
        foreach (Match m in FrameMethod.Matches(f.CodeText))
        {
            int line = f.LineAt(m.Index);
            if (f.Context[line] != "type")
            {
                continue;
            }
            string between = f.CodeText.Substring(m.Index + m.Length, Math.Max(0, f.CodeText.IndexOf('{', m.Index) - m.Index - m.Length)).Trim();
            if (between.Length > 0 || !f.Block(m.Index, out int open, out int close))
            {
                continue;   // abstract, expression-bodied or not a method body
            }
            string name = m.Groups[1].Value;
            string body = f.CodeText.Substring(open + 1, close - open - 1);
            if (EngineFrameMethod.IsMatch(name) && !HotSpotOpening.IsMatch(body))
            {
                Hit("RLO-PERF-002", f.Rel, line + 1, name + " does not open with using var hotSpot = HotSpots.Time(...)");
            }
            int bodyLine = f.LineAt(open);
            string[] lines = body.Split('\n');
            for (int k = 0; k < lines.Length; k++)
            {
                foreach ((string what, Regex pattern) in Allocations)
                {
                    if (pattern.IsMatch(lines[k]))
                    {
                        Hit("RLO-PERF-003", f.Rel, bodyLine + k + 1, name + ": " + what + ": " + lines[k]);
                    }
                }
                foreach ((string what, Regex pattern) in Searches)
                {
                    if (pattern.IsMatch(lines[k]))
                    {
                        Hit("RLO-PERF-004", f.Rel, bodyLine + k + 1, name + ": " + what + ": " + lines[k]);
                    }
                }
            }
        }
    }
}
