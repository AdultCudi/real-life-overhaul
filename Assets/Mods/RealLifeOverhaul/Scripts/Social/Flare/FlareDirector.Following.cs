using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Flare
{
    /// <summary>Flare's director in a Going Public session, on a guest: the host's feed, cases and watches applied here.</summary>
    public sealed partial class FlareDirector
    {
        // The host's part on a guest: whole (the host's shared posts, cases and watches) or what changed.
        private bool GuestApply(string text, bool whole)
        {
            if (!Running || Save == null || string.IsNullOrEmpty(text))
            {
                return false;
            }
            string[] lines = text.Split('\n');
            int start = 0;
            HashSet<int> present = null;
            if (whole)
            {
                int hostDay, seed;
                string hostKey;
                if (!FlareNetShare.ReadHead(lines[0], out hostDay, out seed, out hostKey))
                {
                    return false;
                }
                if (FlareNetShare.NewHost(_file, hostKey))
                {
                    Logger?.Info("Flare: a host or session other than the last (" + hostKey + "): the posts of the last session go, and this player's own are theirs alone again.");
                }
                int dropped = FlareNetShare.DropOwnCity(_file, Save.Day);
                if (dropped > 0)
                {
                    Logger?.Info("Flare: following the host's city: " + dropped + " post(s) of this player's own city of today give way to the host's.");
                }
                if (seed != 0 && seed != _file.Net.CitySeed)
                {
                    _file.Net.CitySeed = seed;   // the host's city's people from now on; their names stay theirs after the session
                    EnsureCitizens(_file);
                }
                present = new HashSet<int>();
                _file.Net.Cases.Clear();
                start = 1;
            }
            int posts = 0, fresh = 0;
            for (int i = start; i < lines.Length; i++)
            {
                string line = lines[i].TrimEnd('\r');
                if (line.Length == 0)
                {
                    continue;
                }
                try
                {
                    string[] f = line[0] == 'X' ? line.Split(new[] { '|' }, 3) : line.Split('|');
                    switch (f[0])
                    {
                        case "P":
                            int net = HostPost(f, out bool added);
                            posts += net > 0 ? 1 : 0;
                            fresh += added ? 1 : 0;
                            if (net > 0)
                            {
                                present?.Add(net);
                            }
                            break;
                        case "C":
                            HostCase(f);
                            break;
                        case "W-":
                            _file.Net.Watches.Clear();
                            break;
                        case "W":
                            FlareWatch w;
                            if (FlareNetShare.ReadWatch(f, out w))
                            {
                                _file.Net.Watches.Add(w);
                            }
                            break;
                        case "X":
                            if (f.Length >= 3)
                            {
                                string id = FlareNetShare.Unesc(f[1]);
                                if (id != null && f[2].Length <= FlareSnap.MostBytes * 2)
                                {
                                    KeepPicture(id, f[2]);
                                }
                            }
                            break;
                        case "D":
                            string from, name, words;
                            long at;
                            if (FlareNetShare.ReadDm(f, out from, out name, out at, out words))
                            {
                                DmIn(from, name, at, words);
                            }
                            break;
                        case "V":
                            string who, goer;
                            long when;
                            int invite;
                            if (FlareNetShare.ReadGoing(f, out who, out goer, out when, out invite) && !_file.Net.IsHidden(who))
                            {
                                FlarePost p = ByNet(invite);
                                string going = "event=" + (p != null ? FlareText.InviteName(p) : "?");
                                FlareLiveRules.Dm(_file.Net, "p:" + who, goer != null ? Filter(goer, who) : who, Math.Min(when, Now), false, null, "reallife_flare_dm_going", going);
                            }
                            break;
                    }
                }
                catch (Exception e)
                {
                    Logger?.Warn("Flare: a line of the host's feed could not be read and is left out (" + e.Message + ").");
                }
            }
            if (present != null)
            {
                for (int i = _file.Posts.Count - 1; i >= 0; i--)
                {
                    if (_file.Posts[i].Shared && !present.Contains(_file.Posts[i].NetId))
                    {
                        _file.Posts.RemoveAt(i);   // the host no longer has it
                    }
                }
            }
            FlareRules.Prune(_file);
            if (whole || fresh > 0)
            {
                Logger?.Info("Flare: the host's feed " + (whole ? "whole: " : "updated: ") + posts + " post(s), " + fresh + " new here, " + _file.Net.Cases.Count + " case(s), "
                             + _file.Net.Watches.Count + " watch(es).");
            }
            Recount();
            Changed();
            return true;
        }

        // One of the host's shared posts: this guest's own coming back (by its token), one it has, or a new one. Its session number; 0 for none.
        private int HostPost(string[] f, out bool added)
        {
            added = false;
            FlarePost p;
            int replyNet;
            if (!FlareNetShare.ReadPost(f, out p, out replyNet))
            {
                return 0;
            }
            string me = MyId;
            if (p.Author == FlareAuthor.Network && p.AuthorId == me && me.Length > 0)
            {
                FlarePost own = p.Token > 0 ? _file.Post(p.Token) : null;
                if (own != null && own.Author == FlareAuthor.Player)
                {
                    own.NetId = p.NetId;
                    own.Pending = false;
                    own.PlayerLikes = p.PlayerLikes;
                    own.Reposts = Math.Max(own.Reposts, p.Reposts);
                    return p.NetId;
                }
                FlarePost mine = ByNet(p.NetId);
                if (mine != null)
                {
                    return p.NetId;   // already matched
                }
                p.Author = FlareAuthor.Player;   // a post of this player's from before this machine knew it (a reload)
                p.Shared = false;
            }
            FlarePost known = ByNet(p.NetId);
            if (known != null)
            {
                known.Likes = p.Likes;
                known.Replies = p.Replies;
                known.Reposts = p.Reposts;
                known.PlayerLikes = p.PlayerLikes;
                return p.NetId;
            }
            FlarePost parent = replyNet > 0 ? ByNet(replyNet) : null;
            if (replyNet > 0 && parent == null)
            {
                return 0;   // a reply to a post this guest does not have
            }
            p.Id = _file.NextId++;
            p.ReplyTo = parent != null ? parent.Id : -1;
            if (p.Author == FlareAuthor.Network)
            {
                string author = p.AuthorId;
                FlareNetShare.CleanPost(p, text => Filter(text, author));   // another player's words, names and tags
                p.AuthorName = p.AuthorName ?? p.AuthorId;
            }
            if (p.Source == FlareSource.Answer && p.Case >= 0)
            {
                p.Case = -p.Case - 1;   // a case of the host's: its number is never this save's own
            }
            _file.Posts.Add(p);
            added = true;
            if (!_file.Net.Applied.Contains(p.NetId))
            {
                _file.Net.Applied.Add(p.NetId);
                while (_file.Net.Applied.Count > FlareNet.AppliedMost)
                {
                    _file.Net.Applied.RemoveAt(0);
                }
                if (FlareNetShare.AppliesOnArrival(p, Save.Day))
                {
                    SessionEffect(p);   // today's only: on joining, the earlier days' reviews have had their days
                }
                if (p.Source == FlareSource.Answer && (p.Arg == (int)FlareAnswer.Nothing || p.Arg == (int)FlareAnswer.Warned) && parent != null && parent.Author == FlareAuthor.Player
                    && Settings.CityFaultsFalseCost)
                {
                    int was = _file.Reputation;
                    _file.Reputation = FlareLiveRules.FalseReport(_file.Reputation);   // the host's City Office found nothing where this player reported: their reputation here
                    Logger?.Info("Flare: the host's City Office found nothing where this player reported; their reputation on Flare " + was + " to " + _file.Reputation + ".");
                }
            }
            return p.NetId;
        }

        // Another player's review of a business that is this player's own (or the city's): its customers move here.
        private void SessionEffect(FlarePost p)
        {
            BuildingRegistration reg = p.Place != null ? FlareLiveGame.AnyBusiness(p.Place) : null;
            if (reg == null || GoingPublic.Foreign(reg) || p.Source != FlareSource.Review)
            {
                return;
            }
            BusinessEffect(p, false);
        }

        // One of the host's cases, as the host has it; an inspection of this player's business is applied here.
        private void HostCase(string[] f)
        {
            FlareCase c;
            int postNet, answerNet;
            if (!FlareNetShare.ReadCase(f, out c, out postNet, out answerNet))
            {
                return;
            }
            FlarePost post = ByNet(postNet), answer = ByNet(answerNet);
            c.Post = post != null ? post.Id : -1;
            c.Answer = answer != null ? answer.Id : -1;
            c.Reporter = post != null && post.Author == FlareAuthor.Player ? "me" : "host";
            for (int i = 0; i < _file.Net.Cases.Count; i++)
            {
                if (_file.Net.Cases[i].Id == c.Id)
                {
                    _file.Net.Cases[i] = c;
                    c = null;
                    break;
                }
            }
            if (c != null)
            {
                _file.Net.Cases.Add(c);
                while (_file.Net.Cases.Count > FlareNet.CasesMost)
                {
                    _file.Net.Cases.RemoveAt(0);
                }
            }
            FlareCase now = c ?? _file.Net.Case(int.Parse(f[1]));
            if (now != null && now.Order == FlareOrder.Inspection && now.State == FlareCaseState.Acting && !_file.Net.Applied.Contains(-1000000 - now.Id))
            {
                BuildingRegistration reg = now.Place != null ? FlareLiveGame.AnyBusiness(now.Place) : null;
                if (reg != null && GoingPublic.Mine(reg))
                {
                    _file.Net.Applied.Add(-1000000 - now.Id);
                    Inspected(now, Save.Day);
                }
            }
        }
    }
}
