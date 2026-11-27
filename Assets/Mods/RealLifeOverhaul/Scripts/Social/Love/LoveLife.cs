using System;
using System.Collections.Generic;
using System.Globalization;
using BigAmbitions.Neighborhoods;
using Entities;
using Helpers;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>How a talk about the other went, ready for the dialog to say and the card to show.</summary>
    internal sealed class LoveReport
    {
        public string Line;
        public Dictionary<string, string> Data;
        public string CardTitle, CardBody;
        public bool Left, Held, Forgiven;
    }

    /// <summary>
    /// The second relationship's life in the game: a public outing rolled against being found out, the signs before it, what happens
    /// when somebody has found out and what each answer costs, who hears of it (the other person, Mom, the in-laws, the paper), and
    /// the daily round. The rules are LoveRules; this is the part that reads and changes the game. COST: a roll on an outing, a short
    /// round once a day.
    /// </summary>
    internal static class LoveLife
    {
        private const int ConfrontWaitDays = 4;   // days they wait to be answered before they give up
        private const int DateOfferDays = 5;      // days an offer to date stands

        private static int V(int a, int b) => LoveState.Seed ^ (a * 31 + b);

        private static string Pick(string[] pool, int a, int b) => LoveCatalogue.Pick(pool, LoveState.Seed, a, b);

        // ---------------------------------------------------------------- an outing, rolled

        /// <summary>An outing with somebody went to its end. If it was with one of the two the player is seeing, the other may have noticed.</summary>
        public static void OutingDone(SocialPerson who, PlanKind kind, in Venue venue)
        {
            if (!LoveHooks.SeeingOn || !LoveState.Loaded || who == null || !LoveRules.Public(kind))
            {
                return;
            }
            LoveFile f = LoveState.File;
            if (f.Second < 0)
            {
                return;
            }
            SocialPerson first = LoveState.FirstPerson, second = LoveState.SecondPerson;
            if (first == null || second == null)
            {
                LoveState.Tidy();
                return;
            }
            int side = who == first ? 0 : who == second ? 1 : -1;
            if (side < 0)
            {
                return;
            }
            SocialPerson watcher = side == 0 ? second : first;
            int wSide = 1 - side;
            int day = SocialLife.Day;
            int minute = (int)(SocialLife.Now % 1440f);
            int district = VenueDistrict(venue);
            int close = LoveRules.Closeness(f, watcher.Index, day, minute, district);
            f.RememberOuting(who.Index, day, minute, district);
            if (f.Knows[wSide] != 0)
            {
                return;   // they know already: there is nothing to find out
            }
            int districts = Math.Max(1, SocialPlaces.Districts);
            int mutual = Mutual(who, watcher, out SocialPerson friend);
            bool tagged = f.TagDay[side] > 0 && day - f.TagDay[side] <= 3;
            var facts = new CatchFacts
            {
                Kind = kind, SameDistrict = district >= 0 && district == Math.Abs(watcher.District) % districts, Free = watcher.FreeAt(SocialLife.Hour, SocialLife.Weekend(day)),
                Mutual = mutual, Tagged = tagged, Close = close, Envious = watcher.Has(SocialTrait.Envious), Careful = watcher.Has(SocialTrait.Careful),
                Punctual = watcher.Has(SocialTrait.Punctual), Suspicion = f.Suspicion[wSide], Around = f.AroundUntil >= day, Denied = f.DeniedDay[wSide] > 0,
                LivesTogether = watcher.MovedIn, Hour = SocialLife.Hour
            };
            int chance = LoveRules.Chance(facts);
            int roll = LoveRules.Roll(LoveState.Seed, day, minute * 7 + who.Index, LoveRules.SaltCatch);
            CatchVerdict verdict = LoveRules.Verdict(roll, chance);
            SocialDirector.Logger?.Info("Love check (catch): " + who.Name + " (" + kind + ") with " + watcher.Name + " watching: chance " + chance + " per mille, roll " + roll
                                        + " is " + verdict + (close > 1 ? ", two plans on one day" : string.Empty) + (tagged ? ", a post tagged them" : string.Empty)
                                        + (mutual > 0 ? ", " + mutual + " friend(s) of both" : string.Empty) + ": PASS");
            if (verdict == CatchVerdict.Nothing)
            {
                return;
            }
            string place = venue.Valid && !string.IsNullOrEmpty(venue.Name) ? venue.Name : SocialLife.T("reallife_love_somewhere");
            Dictionary<string, string> data = SocialLife.Data(watcher, "other", SocialLife.First(who));
            data["place"] = place;
            data["outing"] = SocialLife.T(SocialCatalogue.Outing(kind).Name);
            if (verdict == CatchVerdict.Sign)
            {
                Sign(f, wSide, watcher, who, friend, data, tagged, close, day, minute);
                return;
            }
            Catch(watcher, who, tagged ? CatchHow.Post : mutual > 0 && LoveRules.Roll(LoveState.Seed, day, minute, LoveRules.SaltLine) < 450 ? CatchHow.Friend : CatchHow.Seen);
        }

        // The neighbourhood of a venue in the order of the game's list; -1 when it has no building or the game does not say.
        private static int VenueDistrict(in Venue venue)
        {
            try
            {
                if (venue.Building == null || venue.Building.buildingRegistration == null)
                {
                    return -1;
                }
                string name = GameRegistrations.District(venue.Building.buildingRegistration);
                var all = NeighborhoodHelper.Neighborhoods;
                return name != null && all != null ? all.IndexOf(name) : -1;
            }
            catch (Exception)
            {
                return -1;
            }
        }

        // Friends of both who could have been there: the old friend, a flatmate, anybody who came to the player through a friend.
        private static int Mutual(SocialPerson a, SocialPerson b, out SocialPerson one)
        {
            int n = 0;
            one = null;
            for (int i = 0; i < SocialState.Count && n < 3; i++)
            {
                SocialPerson p = SocialState.Get(i);
                if (p == null || !p.Met || p == a || p == b || p.IsFamily || p.Together || p.Stage < SocialStage.Friend)
                {
                    continue;
                }
                bool flatmate = SocialLinks.LivesWith != null && SocialLinks.LivesWith(p.Index);
                if (p.Origin == SocialOrigin.OldFriend || flatmate || p.Remembers(MemoryCode.Introduced))
                {
                    one = one ?? p;
                    n++;
                }
            }
            return n;
        }

        private static void Sign(LoveFile f, int wSide, SocialPerson watcher, SocialPerson who, SocialPerson friend, Dictionary<string, string> data, bool tagged, int close,
            int day, int minute)
        {
            f.Suspicion[wSide] = Math.Min(100, f.Suspicion[wSide] + LoveRules.SuspicionPerSign);
            f.SignDay[wSide] = day;
            SocialPerson sender = watcher;
            string[] pool = LoveCatalogue.SignSeen;
            if (tagged)
            {
                pool = LoveCatalogue.SignPost;
            }
            else if (friend != null && LoveRules.Roll(LoveState.Seed, day, minute, LoveRules.SaltLine) < 500)
            {
                sender = friend;
                pool = LoveCatalogue.SignFriend;
                data = SocialLife.Data(friend, "other", SocialLife.First(who));
                data["partner"] = SocialLife.First(watcher);
                data["place"] = data.ContainsKey("place") ? data["place"] : string.Empty;
            }
            if (close >= 2)
            {
                // The comedy of it: two plans, one afternoon, a few streets apart.
                Notifications.Show(NotificationType.Warning, Pick(LoveCatalogue.SignClose, day, minute), data, 11f);
            }
            SocialLife.SendText(sender, Pick(pool, day, minute + 1), data, true, true);
            if (f.Suspicion[wSide] <= LoveRules.SuspicionPerSign)
            {
                Notifications.Show(NotificationType.Info, "reallife_love_suspect_toast", SocialLife.Data(watcher), 9f);
            }
        }

        /// <summary>Somebody has found out: they text, and wait to be answered; where they live with the player they wait at home.</summary>
        public static void Catch(SocialPerson watcher, SocialPerson other, CatchHow how)
        {
            LoveFile f = LoveState.File;
            if (f.ConfrontWho >= 0 || watcher == null || other == null)
            {
                return;
            }
            int day = SocialLife.Day;
            f.ConfrontWho = watcher.Index;
            f.ConfrontOf = other.Index;
            f.ConfrontDay = day;
            f.ConfrontHow = how;
            watcher.Remember(MemoryCode.CaughtOut, day);
            bool home = watcher.MovedIn;
            Dictionary<string, string> data = SocialLife.Data(watcher, "other", SocialLife.First(other));
            SocialLife.SendText(watcher, Pick(home ? LoveCatalogue.CaughtHome : LoveCatalogue.CaughtPool(how), day, watcher.Index), data, true, true);
            SocialPerson target = watcher;
            SocialLife.Toast(NotificationType.Error, home ? "reallife_love_caught_home_toast" : "reallife_love_caught_toast", data, 14f, Guard.Wrap(() => SocialPhone.Call(target)));
            if (home && LoveLinks.AskHome != null)
            {
                LoveLinks.AskHome();   // Family: their prompt at home says they want to talk
            }
            SocialDirector.Logger?.Info("Love: " + watcher.Name + " found out about " + other.Name + " (" + how + "); " + (home ? "waiting at home" : "waiting to be called") + ".");
        }

        // ---------------------------------------------------------------- the talk, and what each answer does

        /// <summary>What the player said to somebody who found out: the roll, the figures, who leaves, who hears; settled now.</summary>
        public static LoveReport Resolve(SocialPerson w, LoveAnswer answer)
        {
            LoveFile f = LoveState.File;
            int day = SocialLife.Day;
            SocialPerson other = SocialState.Get(f.ConfrontOf);
            CatchHow how = f.ConfrontHow;
            SocialPerson first = LoveState.FirstPerson;
            int side = Math.Max(0, first != null ? f.SideOf(w.Index, first.Index) : 0);
            bool deniedBefore = f.DeniedDay[side] > 0;
            int forgivenBefore = f.ForgivenWho == w.Index ? f.Forgiven : 0;
            bool bound = LoveLinks.Bound != null && LoveLinks.Bound(w);
            int roll = LoveRules.Roll(LoveState.Seed, day, w.Index * 1000 + (other != null ? other.Index : 0), LoveRules.SaltForgive);
            bool forgiven = false, held = false;
            int chance = 0;
            if (answer == LoveAnswer.Deny)
            {
                chance = LoveRules.DenyChance(w, how, f.Suspicion[side], deniedBefore);
                held = roll < chance;
            }
            else if (answer != LoveAnswer.EndThis)
            {
                chance = LoveRules.ForgiveChance(w, answer, how, forgivenBefore, deniedBefore, bound);
                forgiven = roll < chance;
            }
            LoveResult r = LoveRules.Result(answer, forgiven, held, deniedBefore, forgivenBefore);
            SocialDirector.Logger?.Info("Love check (found out): " + w.Name + " answered with " + answer + ": chance " + chance + " per mille, roll " + roll + ", stays " + r.Stays
                                        + ", held " + r.Held + ", figures " + r.Affection.ToString("F0", CultureInfo.InvariantCulture) + " and " + r.Trust.ToString("F0",
                                                CultureInfo.InvariantCulture) + ": PASS");

            f.ConfrontWho = -1;
            f.ConfrontOf = -1;
            f.ConfrontDay = 0;
            LoveLinks.AskDone?.Invoke();
            SocialRules.Change(w, 0f, r.Affection, r.Trust);   // the stage goes by their decision below, not by the figures' own rule
            SocialLife.Contacted(w);
            bool left = !r.Stays || answer == LoveAnswer.EndThis;
            if (r.Held)
            {
                f.DeniedDay[side] = day;
                f.Suspicion[side] = 15;
                w.Remember(MemoryCode.Denied, day);
            }
            else if (r.Stays)
            {
                f.Forgiven = f.ForgivenWho == w.Index ? f.Forgiven + 1 : 1;
                f.ForgivenWho = w.Index;
                f.Suspicion[side] = 40;
                w.Remember(MemoryCode.Forgave, day);
            }
            var report = new LoveReport { Held = r.Held, Forgiven = r.Stays && !r.Held, Left = left, Data = SocialLife.Data(w, "other", other != null ? SocialLife.First(other) : string.Empty) };
            report.Line = answer == LoveAnswer.Confess ? Pick(forgiven ? LoveCatalogue.Forgave : LoveCatalogue.Refused, day, w.Index)
                : answer == LoveAnswer.Deny ? Pick(held ? LoveCatalogue.DenyHolds : deniedBefore ? LoveCatalogue.DenyDouble : LoveCatalogue.DenyFails, day, w.Index)
                : answer == LoveAnswer.EndOther ? Pick(forgiven ? LoveCatalogue.EndedOther : LoveCatalogue.Refused, day, w.Index)
                : Pick(LoveCatalogue.EndedThis, day, w.Index);

            string otherCard = null;
            if (answer == LoveAnswer.EndOther && other != null)
            {
                float oa = -12f, ot = -12f;
                SocialRules.Change(other, 0f, oa, ot);
                End(other, true);
                otherCard = SocialLife.T("reallife_love_card_ended", SocialLife.Data(other)) + "   " + Signed(oa) + " / " + Signed(ot);
            }
            if (left)
            {
                LeaveW(w, answer == LoveAnswer.EndThis);
            }
            if (!(answer == LoveAnswer.EndOther) && other != null && other.Together && !held)
            {
                int learn = LoveRules.Roll(LoveState.Seed, day, other.Index, LoveRules.SaltLearn);
                if (learn < LoveRules.LearnsChance(how, other.Has(SocialTrait.Envious)))
                {
                    LoveRules.LearnsFigures(out float la, out float lt);
                    SocialRules.Change(other, 0f, la, lt);
                    Dictionary<string, string> od = SocialLife.Data(other, "other", SocialLife.First(w));
                    SocialLife.SendText(other, Pick(LoveCatalogue.OtherHears, day, other.Index), od, true, true);
                    other.Remember(MemoryCode.CaughtOut, day);
                    otherCard = SocialLife.T("reallife_love_card_learns", SocialLife.Data(other)) + "   " + Signed(la) + " / " + Signed(lt);
                    SocialLife.Restage(other);   // below the line, they end it themselves
                }
            }
            ModState.AddModifier(left ? ModState.MoodDownBig : ModState.MoodDown);
            if (!r.Held)
            {
                MomHears(w, other, left);
                if (bound && LoveLinks.InLawsHear != null)
                {
                    LoveLinks.InLawsHear(Pick(LoveCatalogue.InLaws, day, w.Index));
                }
                bool publicly = how == CatchHow.Seen || how == CatchHow.Post || LoveRules.Roll(LoveState.Seed, day, w.Index, LoveRules.SaltScandal) < 600;
                if (publicly)
                {
                    Scandal(false, bound);
                }
            }
            LoveState.Tidy();

            var card = new System.Text.StringBuilder(256);
            card.Append(SocialLife.T("reallife_social_card_affection")).Append("   ").Append(Signed(r.Affection)).Append('\n');
            card.Append(SocialLife.T("reallife_social_card_trust")).Append("   ").Append(Signed(r.Trust)).Append('\n');
            card.Append(SocialLife.T(r.Held ? "reallife_love_card_held" : left ? "reallife_love_card_leaves" : "reallife_love_card_stays", report.Data));
            if (r.Double)
            {
                card.Append('\n').Append(SocialLife.T(deniedBefore ? "reallife_love_card_double_lie" : "reallife_love_card_double_chance"));
            }
            if (otherCard != null)
            {
                card.Append('\n').Append(otherCard);
            }
            card.Append('\n').Append(SocialLife.T("reallife_social_card_mood")).Append("   ").Append(SocialLife.T("reallife_social_card_mood_down"));
            report.CardTitle = SocialLife.T("reallife_love_card_title", SocialLife.Data(w));
            report.CardBody = card.ToString();
            return report;
        }

        /// <summary>They waited and nobody answered: they are done.</summary>
        public static void Lapse(SocialPerson w)
        {
            LoveFile f = LoveState.File;
            int day = SocialLife.Day;
            Dictionary<string, string> data = SocialLife.Data(w, "other", SocialLife.First(SocialState.Get(f.ConfrontOf) ?? w));
            f.ConfrontWho = -1;
            f.ConfrontOf = -1;
            f.ConfrontDay = 0;
            LoveLinks.AskDone?.Invoke();
            SocialRules.Change(w, 0f, -20f, -25f);
            SocialLife.SendText(w, Pick(LoveCatalogue.Lapsed, day, w.Index), data, true, true);
            LeaveW(w, false);
            ModState.AddModifier(ModState.MoodDownBig);
            LoveState.Tidy();
            SocialDirector.Logger?.Info("Love: " + w.Name + " gave up waiting to be answered.");
        }

        // Somebody leaves: Family's separation for a partner who lives with the player, is engaged or married (never a second flow of ours), a break-up for the rest.
        private static void LeaveW(SocialPerson w, bool byPlayer)
        {
            if (LoveLinks.Bound != null && LoveLinks.Bound(w) && LoveLinks.Separate != null)
            {
                LoveLinks.Separate(!byPlayer);
                return;
            }
            End(w, byPlayer);
        }

        /// <summary>A relationship ended by one of them, with the words each says, outside Family's flows.</summary>
        public static void End(SocialPerson p, bool byPlayer)
        {
            if (p == null || !p.Together)
            {
                return;
            }
            p.Stage = SocialStage.Acquaintance;
            p.StageDay = SocialLife.Day;
            p.MovedIn = false;
            p.Dates = 0;
            SocialLife.Remember(p, MemoryCode.BreakUp);
            if (SocialState.Plan.Active && SocialState.Plan.Person == p.Index)
            {
                SocialState.Plan.Clear();
            }
            Dictionary<string, string> data = SocialLife.Data(p);
            SocialLife.SendText(p, byPlayer ? Pick(LoveCatalogue.EndedByYou, SocialLife.Day, p.Index) : SocialLines.Pick(SocialCatalogue.TextBreakUp), data, true, true);
            SocialLinks.PartnerChanged?.Invoke();
        }

        private static void MomHears(SocialPerson w, SocialPerson other, bool left)
        {
            SocialPerson mom = SocialState.ByOrigin(SocialOrigin.Family);
            if (mom == null || !mom.Met || !MomContact.On || !Settings.FamilyRelatives)
            {
                return;
            }
            Dictionary<string, string> data = SocialLife.Data(mom, "name", SocialLife.First(w));
            data["other"] = other != null ? SocialLife.First(other) : string.Empty;
            SocialLife.SendText(mom, Pick(left ? LoveCatalogue.MomLeft : LoveCatalogue.MomStayed, SocialLife.Day, w.Index), data, true, true);
            SocialLife.Change(mom, 0f, left ? -3f : -1f, 0f);
        }

        // ---------------------------------------------------------------- a scandal

        /// <summary>What comes out is gossip among friends; for somebody known, a story in the paper, Flare's talk and a small dent in standing. All by how known the player is.</summary>
        public static void Scandal(bool child, bool bound)
        {
            if (!LoveState.Loaded)
            {
                return;
            }
            LoveFile f = LoveState.File;
            int day = SocialLife.Day;
            int recognition = LoveTests.Recognition >= 0 ? LoveTests.Recognition : LoveLinks.Recognition != null ? Math.Max(0, LoveLinks.Recognition()) : 0;
            ScandalScale s = LoveRules.Scandal(recognition, child, bound);
            SocialPerson friend = Confidant();
            if (s.Gossip && friend != null)
            {
                SocialLife.SendText(friend, Pick(LoveCatalogue.Gossip, day, friend.Index), SocialLife.Data(friend), true, true);
            }
            if (!LoveRules.ScandalMay(f.LastScandalDay, day))
            {
                return;
            }
            int roll = LoveRules.Roll(LoveState.Seed, day, recognition, LoveRules.SaltScandal);
            string story = null;
            if (s.Headline > 0 && roll * 100 / 1000 < s.Headline)
            {
                story = Pick(child ? LoveCatalogue.StoryChild : LoveCatalogue.StoryAffair, day, recognition);
                News.Report(NewsKind.Story, PlayerHelper.GetCityPosition(), story);
            }
            if (s.FlareLost > 0 && LoveLinks.FlareGossip != null)
            {
                int[] moved = LoveLinks.FlareGossip(s.FlareLost, s.FlareGained);
                if (moved != null && moved.Length >= 2 && (moved[0] > 0 || moved[1] > 0))
                {
                    var data = new Dictionary<string, string>(2)
                    {
                        { "lost", moved[0].ToString(CultureInfo.InvariantCulture) }, { "gained", moved[1].ToString(CultureInfo.InvariantCulture) }
                    };
                    Notifications.Show(NotificationType.Info, Pick(LoveCatalogue.FlareTalk, day, recognition), data, 10f);
                }
            }
            if (s.Dent > 0)
            {
                LoveLinks.Dent?.Invoke(s.Dent);
            }
            if (story != null || s.Dent > 0 || s.FlareLost > 0)
            {
                f.LastScandalDay = day;
            }
            SocialDirector.Logger?.Info("Love check (scandal): recognition " + recognition + ", " + (story != null ? "the paper prints it" : "no headline") + ", dent " + s.Dent
                                        + ", Flare loses " + s.FlareLost + " and gains " + s.FlareGained + " per mille: PASS");
        }

        // Somebody the player talks to: the old friend, else the closest friend.
        private static SocialPerson Confidant()
        {
            SocialPerson best = null;
            for (int i = 0; i < SocialState.Count; i++)
            {
                SocialPerson p = SocialState.Get(i);
                if (p == null || !p.Met || p.IsFamily || p.Together || p.Stage < SocialStage.Friend)
                {
                    continue;
                }
                if (p.Origin == SocialOrigin.OldFriend)
                {
                    return p;
                }
                if (best == null || p.Closeness > best.Closeness)
                {
                    best = p;
                }
            }
            return best;
        }

        // ---------------------------------------------------------------- the daily round

        /// <summary>Once a day: suspicion cools, somebody who waited gives up, an offer to date lapses, the child of a night grows nearer, support is paid.</summary>
        public static void Daily(int day)
        {
            if (!LoveState.Loaded)
            {
                return;
            }
            LoveFile f = LoveState.File;
            LoveState.Tidy();
            for (int i = 0; i < 2; i++)
            {
                if (f.Suspicion[i] > 0)
                {
                    f.Suspicion[i] = LoveRules.Settled(f.Suspicion[i], 1);
                }
            }
            if (f.ConfrontWho >= 0)
            {
                SocialPerson w = SocialState.Get(f.ConfrontWho);
                if (w == null || !w.Together)
                {
                    f.ConfrontWho = -1;
                    f.ConfrontOf = -1;
                    LoveLinks.AskDone?.Invoke();
                }
                else if (day - f.ConfrontDay >= ConfrontWaitDays)
                {
                    Lapse(w);
                }
                else if (day > f.ConfrontDay)
                {
                    SocialLife.SendText(w, Pick(LoveCatalogue.Reminder, day, w.Index), SocialLife.Data(w), true, true);
                }
            }
            if (f.DateOfferWho >= 0 && day - f.DateOfferDay > DateOfferDays)
            {
                f.DateOfferWho = -1;
            }
            // A night that got around: the first partner may hear of it.
            if (LoveHooks.SeeingOn && f.AroundUntil >= day && f.ConfrontWho < 0)
            {
                SocialPerson partner = SocialState.Partner;
                LoveHook around = null;
                for (int i = f.Hooks.Count - 1; i >= 0 && around == null; i--)
                {
                    around = f.Hooks[i].Around && f.Hooks[i].Taken ? f.Hooks[i] : null;
                }
                SocialPerson night = around != null ? SocialState.Get(around.Person) : null;
                int side = partner != null && partner != night ? 0 : -1;
                if (side == 0 && night != null && f.Knows[0] == 0 && partner.Index != night.Index
                    && LoveRules.Roll(LoveState.Seed, day, partner.Index, LoveRules.SaltCatch) < 100 + f.Suspicion[0] + (partner.Has(SocialTrait.Envious) ? 60 : 0))
                {
                    Catch(partner, night, CatchHow.Night);
                }
            }
            for (int i = f.Hooks.Count - 1; i >= 0; i--)
            {
                if (day - f.Hooks[i].Day > 40 && f.Hooks[i].TextDay == 0)
                {
                    f.Hooks.RemoveAt(i);
                }
            }
            LoveBabies.Daily(day);
        }

        /// <summary>Once an hour: the next day's texts about a night, sent while the person is awake.</summary>
        public static void Hourly(int day, int hour)
        {
            if (!LoveState.Loaded || hour < 9 || hour > 20)
            {
                return;
            }
            LoveFile f = LoveState.File;
            for (int i = 0; i < f.Hooks.Count; i++)
            {
                LoveHook h = f.Hooks[i];
                if (h.TextDay <= 0 || h.TextDay > day)
                {
                    continue;
                }
                SocialPerson p = SocialState.Get(h.Person);
                if (p == null || !p.Met)
                {
                    h.TextDay = 0;
                    continue;
                }
                string[] pool = h.Outcome == NightOutcome.GotAround ? (h.Taken ? LoveCatalogue.AroundTaken : LoveCatalogue.AroundFree) : LoveCatalogue.NextDay[(int)h.Outcome];
                if (pool == null)
                {
                    h.TextDay = 0;
                    continue;
                }
                SocialPerson sender = p;
                Dictionary<string, string> data = SocialLife.Data(p);
                if (h.Outcome == NightOutcome.GotAround)
                {
                    sender = Confidant() ?? p;
                    data = SocialLife.Data(sender, "other", SocialLife.First(p));
                    data["partner"] = SocialState.Partner != null ? SocialLife.First(SocialState.Partner) : string.Empty;
                }
                data["amount"] = SocialLife.Money(Prices.At(LoveRules.MishapCost(h.Person + h.Day)));
                if (SocialLife.SendText(sender, Pick(pool, day, h.Person), data, false, true))
                {
                    h.TextDay = 0;
                }
                else if (day - h.TextDay > 3)
                {
                    h.TextDay = 0;   // too late to matter
                }
            }
        }

        // ---------------------------------------------------------------- the figures on a card

        internal static string Signed(float value)
        {
            int rounded = Mathf.RoundToInt(value);
            return rounded >= 0 ? "<color=#7CE08A>+" + rounded + "</color>" : "<color=#FF8A70>" + rounded + "</color>";
        }
    }
}
