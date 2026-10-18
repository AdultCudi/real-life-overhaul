using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Flare
{
    /// <summary>The Flare app's profile screen: an account's head, its posts, and the buttons that fit it.</summary>
    public sealed partial class FlareApp
    {
        private void WriteProfile(FlareFile file, long now)
        {
            string key = _at.Key ?? "me";
            FlareRow head = Row(CellProfile);
            head.Key = key;
            head.Text = AboutOf(file, key);
            int shown = 0;
            for (int i = file.Posts.Count - 1; i >= 0 && shown < FlareRules.FeedRows; i--)
            {
                FlarePost p = file.Posts[i];
                if (p.At > now || FlareLiveRules.KeyOf(p) != key || Hidden(file, p))
                {
                    continue;
                }
                bool reply = p.ReplyTo >= 0;
                bool picture = FlarePictures.KeyOf(p) != null;
                if ((_at.Tab == 0 && reply) || (_at.Tab == 1 && !reply) || (_at.Tab == 2 && !picture))
                {
                    continue;
                }
                Row(CellPost).Post = p;
                shown++;
            }
            if (shown == 0)
            {
                Note(FlareText.T(_at.Tab == 2 ? "reallife_flare_empty_pictures" : _at.Tab == 1 ? "reallife_flare_empty_replies" : "reallife_flare_empty_profile"));
            }
        }

        private string AboutOf(FlareFile file, string key)
        {
            int n;
            if (key == "me")
            {
                return FlareText.Fill("reallife_flare_about_me", "tier", FlareText.Tier(file.Tier), "reputation", FlareText.Reputation(file.Reputation));
            }
            if (key.StartsWith("i:", StringComparison.Ordinal) && int.TryParse(key.Substring(2), out n))
            {
                FlareInfluencer inf = file.Influencer(n);
                if (inf != null)
                {
                    string about = FlareText.Fill("reallife_flare_profile_what", "niche", FlareText.Niche(inf.Niche), "district", FlareGame.DistrictName(inf.District),
                        "style", FlareText.Style(inf.Style));
                    if (_director.CurrentDay - inf.ScandalDay <= FlareCast.MonthDays)
                    {
                        about += " " + FlareText.T("reallife_flare_profile_scandal");
                    }
                    if (inf.Person >= 0 && _director.PersonName(inf.Person) != null)
                    {
                        about += " " + FlareText.Fill(inf.Star ? "reallife_flare_profile_star_person" : "reallife_flare_profile_friend", "name", _director.PersonName(inf.Person));
                    }
                    return about;
                }
            }
            if (key.StartsWith("o:", StringComparison.Ordinal))
            {
                return FlareText.T("reallife_flare_about_official");
            }
            if (key.StartsWith("p:", StringComparison.Ordinal))
            {
                return FlareText.T("reallife_flare_about_player");
            }
            if (key.StartsWith("b:", StringComparison.Ordinal))
            {
                return FlareText.T("reallife_flare_account_brand_about");
            }
            if (key.StartsWith("c:", StringComparison.Ordinal))
            {
                return FlareText.T("reallife_flare_account_candidate_about");
            }
            if (key.StartsWith("k:", StringComparison.Ordinal))
            {
                return FlareText.T("reallife_flare_about_business");
            }
            if (key.StartsWith("t:", StringComparison.Ordinal))
            {
                return FlareText.T("reallife_flare_about_tycoon");
            }
            if (key.Length > 1 && key[0] == 'c' && int.TryParse(key.Substring(1), out n) && n >= 0 && n < _director.Citizens.Count)
            {
                return FlareText.Fill("reallife_flare_about_citizen", "district", FlareText.District(_director.Citizens[n].District));
            }
            if (key.Length > 1 && key[0] == 'f' && int.TryParse(key.Substring(1), out n))
            {
                FlarePerson person;
                if (_director.Person(n, out person))
                {
                    return FlareText.Fill("reallife_flare_about_friend", "job", person.Job, "district", person.District);
                }
            }
            return string.Empty;
        }

        private string StatsOf(string key)
        {
            FlareFile file = _director.File;
            if (file == null)
            {
                return string.Empty;
            }
            int n;
            if (key == "me")
            {
                return FlareText.Fill("reallife_flare_stats_me", "followers", FlareText.N(file.Followers), "friends", FlareRules.Count(file.Following).ToString(),
                    "posts", FlareText.N(file.TotalPosts), "likes", FlareText.N(file.TotalLikes), "following", file.Follows.Count.ToString());
            }
            if (key.StartsWith("i:", StringComparison.Ordinal) && int.TryParse(key.Substring(2), out n) && file.Influencer(n) != null)
            {
                FlareInfluencer inf = file.Influencer(n);
                return FlareText.Fill("reallife_flare_profile_figures", "followers", FlareText.N(inf.Followers), "engaged", (inf.Engagement / 100f).ToString("0.0") + "%",
                    "reliable", inf.Reliability.ToString()) + "  ·  " + FlareText.Pair("reallife_flare_profile_price", FlareText.Money(Prices.At(FlareCast.Price(inf))));
            }
            int posts = 0;
            for (int i = 0; i < file.Posts.Count; i++)
            {
                posts += FlareLiveRules.KeyOf(file.Posts[i]) == key ? 1 : 0;
            }
            return FlareText.Fill("reallife_flare_stats_other", "posts", posts.ToString());
        }

        private readonly List<int> _profileActions = new List<int>(4);
        private const int ActFollow = 0, ActMessage = 1, ActMute = 2, ActHide = 3, ActHire = 4;

        // The buttons of a profile's head: follow, message, mute, hide, hire, as fit the account.
        private void ProfileActions(string key)
        {
            _profileActions.Clear();
            if (key == null || key == "me")
            {
                return;
            }
            if (Followable(key))
            {
                _profileActions.Add(ActFollow);
            }
            if (key.StartsWith("p:", StringComparison.Ordinal) || key.StartsWith("o:", StringComparison.Ordinal) || key.StartsWith("i:", StringComparison.Ordinal)
                || (key.Length > 1 && key[0] == 'c' && key[1] != ':'))
            {
                _profileActions.Add(ActMessage);
            }
            if (key.StartsWith("p:", StringComparison.Ordinal))
            {
                _profileActions.Add(ActMute);
                _profileActions.Add(ActHide);
            }
            if (key.StartsWith("i:", StringComparison.Ordinal) && FlareGame.Businesses().Count > 0)
            {
                _profileActions.Add(ActHire);
            }
        }

        private bool ProfileButtonOf(string key, int i, out string label, out bool filled)
        {
            ProfileActions(key);
            label = null;
            filled = false;
            if (i >= _profileActions.Count)
            {
                return false;
            }
            FlareFile file = _director.File;
            string player = key.Length > 2 ? key.Substring(2) : null;
            switch (_profileActions[i])
            {
                case ActFollow:
                    bool following = file != null && FlareCast.Followed(file, key);
                    label = FlareText.T(following ? "reallife_flare_follow_on" : "reallife_flare_follow");
                    filled = !following;
                    return true;
                case ActMessage:
                    label = FlareText.T("reallife_flare_message");
                    return true;
                case ActMute:
                    label = FlareText.T(file != null && file.Net.IsMuted(player) ? "reallife_flare_unmute" : "reallife_flare_mute");
                    return true;
                case ActHide:
                    label = FlareText.T(file != null && file.Net.IsHidden(player) ? "reallife_flare_unhide" : "reallife_flare_hide");
                    return true;
                default:
                    label = FlareText.T("reallife_flare_shop_hire");
                    return true;
            }
        }

        private void ProfileButton(FlareRow row, int i)
        {
            if (row == null)
            {
                return;
            }
            string key = row.Key;
            ProfileActions(key);
            FlareFile file = _director.File;
            if (file == null || i >= _profileActions.Count)
            {
                return;
            }
            string player = key.Length > 2 ? key.Substring(2) : null;
            switch (_profileActions[i])
            {
                case ActFollow: _director.Follow(key, !FlareCast.Followed(file, key)); break;
                case ActMessage: Open(FlareScreen.Conversation, 0, -1, key); break;
                case ActMute: _director.Mute(player, !file.Net.IsMuted(player)); break;
                case ActHide: _director.Hide(player, !file.Net.IsHidden(player)); break;
                default:
                    int id;
                    int.TryParse(key.Substring(2), out id);
                    List<BuildingRegistration> shops = FlareGame.Businesses();
                    if (shops.Count > 0)
                    {
                        OpenHire(FlareGame.Place(shops[0]), FlareGame.Name(shops[0]), id);
                    }
                    break;
            }
        }
    }
}
