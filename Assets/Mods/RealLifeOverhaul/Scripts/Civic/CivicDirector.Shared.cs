using System;
using System.Globalization;
using System.Text;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>Civic life in a Going Public session: the host's notices and a guest's requests, and the host's city applied on a guest.</summary>
    public sealed partial class CivicDirector
    {
        private readonly StringBuilder _notices = new StringBuilder(256);   // the host: toasts and stories since the last update
        private Action<NotificationType, string, string[], float> _heard;
        private Action<string, string> _reported;
        private const char Gap = '\u001f';   // between the fields of one notice (never in a text)
        private const string Mod = "reallife";                       // apart, so the locale check does not take a prefix for a key
        private const string CivicKeys = Mod + "_civic_";             // the only keys a host's notice may name
        private const string PollsKeys = Mod + "_civic_polls_open";   // the polls opening: each guest says its own

        // The host hears the city's toasts and stories while its hours run or a guest's request is applied.
        private void Listen(bool on)
        {
            CivicText.Heard = on ? _heard : null;
            CivicBridges.Reported = on ? _reported : null;
        }

        private void Heard(NotificationType type, string key, string[] pairs, float seconds)
        {
            if (key.StartsWith(PollsKeys, StringComparison.Ordinal) || _notices.Length > 8192)
            {
                return;   // each guest says its own polling place
            }
            _notices.Append('T').Append(Gap).Append((int)type).Append(Gap).Append(seconds.ToString("0.#", CultureInfo.InvariantCulture)).Append(Gap).Append(key);
            for (int i = 0; pairs != null && i < pairs.Length; i++)
            {
                _notices.Append(Gap).Append(Clean(pairs[i]));
            }
            _notices.Append('\n');
        }

        private void Reported(string headline, string body)
        {
            if (_notices.Length <= 8192)
            {
                _notices.Append('R').Append(Gap).Append(headline).Append(Gap).Append(body).Append('\n');
            }
        }

        private static string Clean(string text)
        {
            return string.IsNullOrEmpty(text) ? string.Empty : text.Replace(Gap, ' ').Replace('\n', ' ');
        }

        private string TakeNotices()
        {
            if (_notices.Length == 0)
            {
                return null;
            }
            string text = _notices.ToString();
            _notices.Length = 0;
            return text;
        }

        // A guest: what the host's player was told of the city, told here too.
        private bool ShowNotices(string text, bool whole)
        {
            if (whole)
            {
                return true;
            }
            string[] lines = text.Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                string[] f = lines[i].Split(Gap);
                if (f.Length >= 4 && f[0] == "T" && int.TryParse(f[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int type)
                    && float.TryParse(f[2], NumberStyles.Float, CultureInfo.InvariantCulture, out float seconds) && f[3].StartsWith(CivicKeys, StringComparison.Ordinal))
                {
                    string[] pairs = null;
                    if (f.Length > 4)
                    {
                        pairs = new string[f.Length - 4];
                        Array.Copy(f, 4, pairs, 0, pairs.Length);
                    }
                    CivicText.Show((NotificationType)type, f[3], pairs, Mathf.Clamp(seconds, 4f, 20f));
                }
                else if (f.Length >= 3 && f[0] == "R" && f[1].StartsWith(CivicKeys, StringComparison.Ordinal) && f[2].StartsWith(CivicKeys, StringComparison.Ordinal))
                {
                    CivicBridges.Report(f[1], f[2]);
                }
            }
            return true;
        }

        // A guest: the host's government. A counted election settles the player's own part as the host's count would.
        private bool ApplyGov(string text, bool whole)
        {
            GovState s = Government.State;
            GovChange change;
            if (!CivicShare.ApplyGov(s, text, out change))
            {
                return false;
            }
            if (change.LeversMoved != 0)
            {
                OnLevers();
            }
            if (change.Counted && s.Winner >= 0 && s.Winner < s.CandidateCount)
            {
                int per = Mathf.Max(1, CivicCatalogue.FavourPerNow);
                if (s.Donated[s.Winner] >= per)
                {
                    s.Favours = Mathf.Min(2, s.Favours + s.Donated[s.Winner] / per);
                }
                if (s.Voted == s.Winner)
                {
                    ModState.AddModifier(ModState.MoodUp);
                }
            }
            if (change.Phase)
            {
                Government.CampaignChanged?.Invoke();
            }
            return true;
        }

        // A guest: the host's building sites; its own refit stays its own.
        private bool ApplyWorks(string text, bool whole)
        {
            if (!CivicShare.ApplyWorks(Works.State, text))
            {
                return false;
            }
            Works.Shared = true;
            Works.Earshots();
            Works.SitesChanged?.Invoke();
            return true;
        }

        // The host: a guest's requests, one a line; what they say of the city is heard by every guest with the next update.
        private void HostRequests(string text, string who, bool works)
        {
            if (string.IsNullOrEmpty(text))
            {
                return;
            }
            int day = TimeHelper.CurrentDay, hour = TimeHelper.CurrentHour;
            string[] lines = text.Split('\n');
            int done = 0;
            Listen(true);
            try
            {
                for (int i = 0; i < lines.Length && i < 64; i++)
                {
                    if (lines[i].Length > 0 && (works ? Works.HostRequest(lines[i], day) : Government.HostRequest(lines[i], who, day, hour)))
                    {
                        done++;
                    }
                }
            }
            finally
            {
                Listen(false);
            }
            if (done > 0)
            {
                SharedCity.Changed(works ? 'W' : 'G');
            }
            if (Meta.Dev.Verbose)
            {
                Logger?.Info("Civic check (the host): " + done + " of " + lines.Length + " request line(s) of " + who + " applied to the city's " + (works ? "sites" : "government") + ": PASS");
            }
        }

        // A test that would change the city's government or sites: not while they are the host's.
        private bool HostOwns(string test, char part)
        {
            if (!SharedCity.Follows(part))
            {
                return false;
            }
            TestRefusals.TellKey(Logger, test, "reallife_gp_city_test_host");
            return true;
        }
    }
}
