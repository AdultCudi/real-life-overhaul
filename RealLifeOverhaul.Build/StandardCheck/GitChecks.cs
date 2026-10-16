using System;
using System.Text.RegularExpressions;

// docs/standards/git-and-review.md: the branch's name, and the subject of every commit made on it since it left main.
internal static partial class Program
{
    private static readonly Regex BranchName = new Regex(@"^(?:fix|feature|perf|release|integrate|test|build)/[a-z0-9]+(?:[-.][a-z0-9]+)*$", RegexOptions.Compiled);
    private static readonly Regex TypePrefix = new Regex(@"^(?:feat|fix|chore|docs|refactor|test|style|perf|build|ci|wip)(?:\([^)]*\))?!?:", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex Squash = new Regex(@"^(?:fixup!|squash!|amend!|WIP\b)", RegexOptions.Compiled);
    private static readonly Regex CodeWord = new Regex(@"^[\w./-]*[_./][\w./-]*", RegexOptions.Compiled);

    private static void GitChecks()
    {
        string branch = Git("rev-parse --abbrev-ref HEAD");
        if (branch == null)
        {
            Notes.Add("RLO-GIT-001 and RLO-GIT-002 not checked: no git history");
            return;
        }
        branch = branch.Trim();
        if (branch != "HEAD" && branch != "main")
        {
            // RLO-GIT-001: fix/<name> or feature/<name> (and the maintainer's own kinds), lower-case words and hyphens.
            Examine("RLO-GIT-001", "branch " + branch);
            if (!BranchName.IsMatch(branch))
            {
                Hit("RLO-GIT-001", "branch " + branch, 0, branch);
            }
        }
        string from = MergeBase();
        string log = from == null ? null : Git("log --no-merges --format=%h%x09%s " + from + "..HEAD");
        if (log == null)
        {
            return;
        }
        foreach (string line in log.Split('\n'))
        {
            int tab = line.IndexOf('\t');
            if (tab < 0)
            {
                continue;
            }
            string hash = line.Substring(0, tab);
            string subject = line.Substring(tab + 1).TrimEnd('\r');
            string what = "commit " + hash;
            // RLO-GIT-002: one plain sentence, no full stop, no type prefix, begun with a capital, a number or a code name.
            Examine("RLO-GIT-002", what);
            bool begins = subject.Length > 0 && (char.IsUpper(subject[0]) || char.IsDigit(subject[0]) || CodeWord.IsMatch(subject));
            if (subject.EndsWith(".", StringComparison.Ordinal) || TypePrefix.IsMatch(subject) || Squash.IsMatch(subject) || !begins)
            {
                Hit("RLO-GIT-002", what, 0, subject);
            }
        }
    }
}
