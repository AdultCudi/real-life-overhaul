using UnityEngine;

namespace RealLifeOverhaul.Flare
{
    /// <summary>The Flare app, what a card shows: a post's author, words, context, picture and case, a notification's who and snippet.</summary>
    public sealed partial class FlareApp
    {
        private void AuthorOf(FlarePost p, out string name, out string handle, out string key)
        {
            key = FlareLiveRules.KeyOf(p);
            FlareFile file = _director.File;
            switch (p.Author)
            {
                case FlareAuthor.Player:
                    name = _director.MyName;
                    handle = _director.MyHandle;
                    return;
                case FlareAuthor.Friend:
                    name = _director.PersonName(p.Who) ?? "?";
                    handle = FlareText.HandleOf(name);
                    return;
                case FlareAuthor.Citizen:
                    if (p.Who >= 0 && p.Who < _director.Citizens.Count)
                    {
                        name = _director.Citizens[p.Who].First + " " + _director.Citizens[p.Who].Last;
                        handle = _director.Citizens[p.Who].Handle;
                        return;
                    }
                    name = "?";
                    handle = string.Empty;
                    return;
                case FlareAuthor.Official:
                    name = FlareText.OfficialName(p.Who);
                    handle = FlareText.OfficialHandle(p.Who);
                    return;
                case FlareAuthor.Influencer:
                    FlareInfluencer inf = file != null ? file.Influencer(p.Who) : null;
                    string known = inf != null && inf.Person >= 0 ? _director.PersonName(inf.Person) : null;
                    name = inf == null ? "?" : known != null ? known : inf.Handle.TrimStart('@');
                    handle = inf != null ? inf.Handle : string.Empty;
                    return;
                case FlareAuthor.Network:
                    name = p.AuthorName ?? p.AuthorId ?? "?";
                    handle = FlareText.HandleOf(p.AuthorId);
                    return;
                case FlareAuthor.Ledger:
                    name = FlareText.T("reallife_flare_ledger_name");
                    handle = "@cityledger";
                    return;
                default:
                    name = p.Name ?? "?";
                    handle = FlareText.HandleOf(name);
                    return;
            }
        }

        private string WordsOf(FlarePost p, string author)
        {
            return _director.WordsFor(p, author);   // RLO Texts: the model's words when written in time, else the written line
        }

        private string ContextOf(FlarePost p)
        {
            FlareFile file = _director.File;
            if (p.ReplyTo >= 0 && file != null && !(_at.Screen == FlareScreen.Thread))
            {
                FlarePost parent = file.Post(p.ReplyTo);
                if (parent != null)
                {
                    string name, handle, key;
                    AuthorOf(parent, out name, out handle, out key);
                    return FlareText.Fill("reallife_flare_replying_to", "handle", handle);
                }
            }
            if (p.Author == FlareAuthor.Player && p.Tag >= 0 && _director.PersonName(p.Tag) != null)
            {
                return FlareText.Fill("reallife_flare_with_friend", "friend", _director.PersonName(p.Tag));
            }
            if (p.Mention != null && p.Mention.Length > 2)
            {
                return FlareText.Fill("reallife_flare_calling", "account", _director.AccountName(p.Mention));
            }
            if (p.Author == FlareAuthor.Player && p.Viral)
            {
                return FlareText.T("reallife_flare_post_viral");
            }
            return null;
        }

        private static FlareGlyph GlyphOf(FlarePost p)
        {
            switch (p.Source)
            {
                case FlareSource.Report: return FlareGlyph.Flag;
                case FlareSource.Review: return FlareGlyph.StarFull;
                case FlareSource.Announce:
                case FlareSource.Opened:
                case FlareSource.RecordDay:
                case FlareSource.Interior: return FlareGlyph.Store;
                case FlareSource.Invite: return FlareGlyph.Calendar;
                case FlareSource.Photo:
                case FlareSource.Selfie: return FlareGlyph.Picture;
                case FlareSource.Outing: return FlareGlyph.Person;
                case FlareSource.Voted:
                case FlareSource.Endorsed:
                case FlareSource.Campaign: return FlareGlyph.Check;
                case FlareSource.Issue: return FlareGlyph.Flag;
                default: return FlareGlyph.HeartFull;
            }
        }

        private Texture2D PictureOf(FlarePost p)
        {
            FlareFile file = _director.File;
            if (file == null || Hidden(file, p))
            {
                return null;
            }
            return _pictures.Of(FlarePictures.KeyOf(p) ?? _director.CityPhotoKey(p));   // the player's own picture, else a photo of the city's
        }

        private string CaseLineOf(FlarePost p)
        {
            FlareFile file = _director.File;
            if (file == null || p.Source != FlareSource.Report)
            {
                return null;
            }
            FlareCase c = null;
            for (int i = 0; i < file.Net.Cases.Count && c == null; i++)
            {
                c = file.Net.Cases[i].Post == p.Id ? file.Net.Cases[i] : null;
            }
            if (c == null && p.Case >= 0)
            {
                c = file.Net.Case(p.Case);
            }
            if (c == null)
            {
                return null;
            }
            string line;
            switch (c.State)
            {
                case FlareCaseState.Open:
                    line = FlareText.Fill("reallife_flare_case_open", "time", FlareText.Clock((int)(c.ReplyAt % 1440)));
                    break;
                case FlareCaseState.Queued:
                    line = FlareText.T("reallife_flare_case_queued");
                    break;
                case FlareCaseState.Done:
                    line = FlareText.T(c.Order == FlareOrder.Answered ? "reallife_flare_case_answered" : "reallife_flare_case_done");
                    break;
                default:
                    switch (c.Order)
                    {
                        case FlareOrder.Works:
                            FlarePost answer = c.Answer >= 0 ? file.Post(c.Answer) : null;
                            line = FlareText.Fill("reallife_flare_case_works", "address", answer != null && answer.Name != null ? answer.Name : FlareText.District(c.District),
                                "days", answer != null && answer.When > 0 ? answer.When.ToString() : "?");
                            break;
                        case FlareOrder.Patrol:
                            line = FlareText.Fill("reallife_flare_case_patrol", "district", FlareText.District(c.District), "day", c.Until.ToString());
                            break;
                        case FlareOrder.Cleanup:
                            line = FlareText.Fill("reallife_flare_case_cleanup", "district", FlareText.District(c.District), "day", c.Until.ToString());
                            break;
                        default:
                            line = FlareText.Fill("reallife_flare_case_inspection", "day", c.Until.ToString());
                            break;
                    }
                    break;
            }
            if (c.Repeats > 0)
            {
                line += "  ·  " + FlareText.Fill("reallife_flare_case_repeats", "count", c.Repeats.ToString());
            }
            return line;
        }

        private int RepliesOf(FlarePost p, long now)
        {
            FlareFile file = _director.File;
            int n = FlareLiveRules.Shown(p.Replies, p, now);
            for (int i = 0; file != null && i < file.Posts.Count; i++)
            {
                n += file.Posts[i].ReplyTo == p.Id && file.Posts[i].At <= now ? 1 : 0;
            }
            return n;
        }

        // A notification's who: a friend, the partner, one of the staff, the boss, one of the city's people, an account.
        private string ReactorName(string who)
        {
            if (string.IsNullOrEmpty(who))
            {
                return "?";
            }
            int n;
            if (who[0] == 'p' && who.Length > 1 && who[1] != ':' && int.TryParse(who.Substring(1), out n))
            {
                return _director.PersonName(n) ?? "?";
            }
            if (who == "b")
            {
                return FlareText.T("reallife_flare_your_boss");
            }
            return _director.AccountName(who);
        }

        private string SnippetOf(FlareNotice n)
        {
            FlareFile file = _director.File;
            if (n.Dm >= 0 && file != null)
            {
                for (int i = 0; i < file.Net.Dms.Count; i++)
                {
                    if (file.Net.Dms[i].Id == n.Dm)
                    {
                        return Cut(_director.DmWords(file.Net.Dms[i]) ?? FlareText.DmWords(file.Net.Dms[i], file.Net.Dms[i].Name));
                    }
                }
            }
            FlarePost p = n.Post >= 0 && file != null ? file.Post(n.Post) : null;
            if (p == null)
            {
                return null;
            }
            string name, handle, key;
            AuthorOf(p, out name, out handle, out key);
            return Cut(p.Text ?? WordsOf(p, name));
        }

        private static string Cut(string text)
        {
            return text != null && text.Length > 160 ? text.Substring(0, 157) + "..." : text;
        }

        /// <summary>Whether an account can be followed: any but the player and their friends.</summary>
        private static bool Followable(string key)
        {
            return key != null && key != "me" && !(key.Length > 1 && key[0] == 'f' && char.IsDigit(key[1]));
        }
    }
}
