using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Parties
{
    /// <summary>What the player asks for when they host: where, how good, when, and whom (their places in the cast).</summary>
    public struct PartyAsk
    {
        public PartyPlace Place;
        public PartyTier Tier;
        public int Day, Hour;
        public string Street;     // a venue's address; null for the home, or for the venue the world finds
        public int Number;
        public IList<int> Guests;
    }

    /// <summary>
    /// A party with friends from the booking to its end, and other people's parties the player is asked to: every step the
    /// game takes, written against <see cref="IPartyWorld"/> so it runs offline as it runs in the game. The order of every
    /// step is the rule: the option and the session are asked first, a refusal comes before any money, and money moves
    /// before anything is recorded. Nothing a frame: a booking, a press, or the director's look once a second.
    /// </summary>
    public static class PartyDesk
    {
        public const string HostKey = "reallife_transaction_party_host";   // a party's food and drink, from the player's own pocket; its refund is the same key

        /// <summary>Whether anything may be booked, begun or answered at all: not with the option off, and not as a guest of a Going Public session.</summary>
        public static PartyRefusal Stand(IPartyWorld w)
        {
            return !w.On ? PartyRefusal.Off : w.Guest ? PartyRefusal.Guest : PartyRefusal.None;
        }

        // ------------------------------------------------------------------------------------------------- hosting

        /// <summary>
        /// The party as it would be booked now, with what each person answers, the room the place has and what it costs:
        /// what the page shows before the invitations go out, and what <see cref="Host"/> books. Nothing is changed.
        /// </summary>
        public static Party Draft(PartyFile f, IPartyWorld w, PartyAsk ask, out PlaceFacts place, out int capacity, out int cost)
        {
            int tier = PartyCatalogue.Clamp((int)ask.Tier, 0, PartyCatalogue.Minutes.Length - 1);
            int start = PartyRules.Stamp(ask.Day, ask.Hour);
            place = ask.Place != PartyPlace.Home && string.IsNullOrEmpty(ask.Street) ? w.FindVenue(ask.Place, start, PartyCatalogue.Minutes[tier])
                : w.Place(ask.Place, ask.Street, ask.Number, start, PartyCatalogue.Minutes[tier]);
            var p = new Party
            {
                Id = f.NextId, Place = ask.Place, Tier = (PartyTier)tier, Day = ask.Day, Hour = ask.Hour, State = PartyState.Planned,
                Street = place.Street ?? string.Empty, Number = place.Number, Name = place.Name ?? string.Empty, Own = place.Own, Sqm = place.Sqm
            };
            int today = w.Day;
            for (int i = 0; ask.Guests != null && i < ask.Guests.Count; i++)
            {
                SocialPerson who = w.Person(ask.Guests[i]);
                if (who == null || !who.Met || Asked(p, who.Index))
                {
                    continue;
                }
                p.Guests.Add(new PartyGuest
                {
                    Person = who.Index, Answer = PartyRules.Answer(f.Seed, p.Id, who, ask.Place, ask.Day, ask.Hour, today, place.Found ? place.District : -1)
                });
            }
            capacity = PartyRules.Capacity(ask.Place, place.Sqm);
            cost = PartyRules.Cost(p.Tier, p.Place, p.Own, w.Level, p.Coming);
            return p;
        }

        private static bool Asked(Party p, int person)
        {
            for (int i = 0; i < p.Guests.Count; i++)
            {
                if (p.Guests[i].Person == person)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>Why a drafted party cannot be booked, the first reason that holds; None when it can. Asks for no money.</summary>
        public static PartyRefusal Refuse(PartyFile f, IPartyWorld w, Party p, PlaceFacts place, int capacity, int cost)
        {
            PartyRefusal stand = Stand(w);
            if (stand != PartyRefusal.None)
            {
                return stand;
            }
            if (!place.Found)
            {
                return p.Place == PartyPlace.Home ? PartyRefusal.NoHome : PartyRefusal.NoVenue;
            }
            if (p.Hour < PartyCatalogue.FirstHour || p.Hour > PartyCatalogue.LastHour)
            {
                return PartyRefusal.BadHour;
            }
            int ahead = p.Start - w.Now;
            if (ahead < PartyCatalogue.LeadHours * 60)
            {
                return PartyRefusal.TooSoon;
            }
            if (ahead > PartyCatalogue.AheadDays * 1440)
            {
                return PartyRefusal.TooFar;
            }
            if (!place.Open)
            {
                return PartyRefusal.VenueShut;
            }
            if (f.Planned >= PartyCatalogue.PlannedMost)
            {
                return PartyRefusal.TooMany;
            }
            for (int i = 0; i < f.Parties.Count; i++)
            {
                if (!f.Parties[i].Over && f.Parties[i].Day == p.Day)
                {
                    return PartyRefusal.DayTaken;
                }
            }
            if (p.Guests.Count == 0)
            {
                return PartyRefusal.NoGuests;
            }
            if (p.Guests.Count > capacity)
            {
                return PartyRefusal.TooManyGuests;
            }
            if (p.Coming == 0)
            {
                return PartyRefusal.NobodyComes;
            }
            if (w.Money < cost)
            {
                return PartyRefusal.NoMoney;
            }
            if (w.Floor > 0 && w.Money - cost < w.Floor)
            {
                return PartyRefusal.Reserve;   // the wallet would fall under what it keeps for a firm's investors: a party is never their money
            }
            return PartyRefusal.None;
        }

        /// <summary>
        /// Books a party: the invitations are answered, the food and drink for everybody who said yes and the host is paid
        /// at once, and only then is it recorded. Refused before any money for every reason there is.
        /// </summary>
        public static PartyRefusal Host(PartyFile f, IPartyWorld w, PartyAsk ask, out Party booked)
        {
            booked = null;
            PartyRefusal stand = Stand(w);
            if (stand != PartyRefusal.None)
            {
                return stand;
            }
            Party p = Draft(f, w, ask, out PlaceFacts place, out int capacity, out int cost);
            PartyRefusal why = Refuse(f, w, p, place, capacity, cost);
            if (why != PartyRefusal.None)
            {
                return why;
            }
            if (!w.Pay(cost, HostKey, null))
            {
                return PartyRefusal.NoMoney;
            }
            p.Id = f.NextId++;
            p.Paid = cost;
            f.Parties.Add(p);
            booked = p;
            w.Tell(PartyTone.Good, "reallife_party_note_booked", "place", p.Name, "when", w.When(p.Day, p.Hour), "count", p.Coming.ToString(), "cost", w.Cash(cost));
            return PartyRefusal.None;
        }

        /// <summary>
        /// The host calls a planned party off: part of the money back by the notice given, and those who said yes mind by
        /// the notice too, as the social life counts a plan called off. A party never held gives nothing, so there is
        /// nothing to keep by booking and cancelling.
        /// </summary>
        public static PartyRefusal Cancel(PartyFile f, IPartyWorld w, int id)
        {
            PartyRefusal stand = Stand(w);
            if (stand != PartyRefusal.None)
            {
                return stand;
            }
            Party p = f.Find(id);
            if (p == null || p.State != PartyState.Planned)
            {
                return p != null && p.State == PartyState.Running ? PartyRefusal.Begun : PartyRefusal.NotPlanned;
            }
            Withdraw(f, w, p, w.Now, "reallife_party_note_cancelled");
            Trim(f);
            return PartyRefusal.None;
        }

        /// <summary>
        /// A planned party ended by the host's own doing, measured at the moment it was done (<paramref name="at"/>): calling it off, the option switched off, a venue of their
        /// own shut or sold. Part of the money back by the notice that moment gave, and those who said yes mind by it, as the social life counts a plan called off. The one place
        /// a planned party is given up by the host, so no way of ending one is better than calling it off at that hour.
        /// </summary>
        private static void Withdraw(PartyFile f, IPartyWorld w, Party p, int at, string key)
        {
            int ahead = p.Start - at;
            int back = PartyRules.Refund(p.Paid, p.Start, at);
            float trust = ahead >= PartyCatalogue.EarlyHours * 60 ? 0f : ahead >= PartyCatalogue.LeadHours * 60 ? -1f : SocialRules.CancelledTrust;
            float affection = ahead >= PartyCatalogue.EarlyHours * 60 ? 0f : ahead >= PartyCatalogue.LeadHours * 60 ? -1f : SocialRules.CancelledAffection;
            for (int i = 0; i < p.Guests.Count && (trust != 0f || affection != 0f); i++)
            {
                SocialPerson who = p.Guests[i].Answer == PartyAnswer.Yes ? w.Person(p.Guests[i].Person) : null;
                if (who != null && who.Met)
                {
                    w.Change(who, 0f, affection, trust);
                }
            }
            if (back > 0)
            {
                w.Refund(back, HostKey, null);
            }
            p.Paid -= back;
            p.State = PartyState.Cancelled;
            w.Tell(PartyTone.Info, key, "place", p.Name, "back", w.Cash(back));
        }

        // Called off by what the host did not do: everything back, nobody minds.
        private static void CallOff(PartyFile f, IPartyWorld w, Party p, string key)
        {
            int back = p.Paid;
            if (back > 0)
            {
                w.Refund(back, HostKey, null);
            }
            p.Paid = 0;
            p.State = PartyState.Cancelled;
            w.Tell(PartyTone.Warn, key, "place", p.Name, "back", w.Cash(back));
        }

        /// <summary>
        /// A planned party whose place cannot hold it now (<paramref name="place"/> as the world finds it) is ended. A venue of the host's own found shut by their switch or
        /// hours, or gone from their hands, is their doing: it is called off at this moment, by the notice then. Anything else (a closure that is not theirs, a home they no
        /// longer hold, another's venue) is nobody's fault: everything back and nobody minds. False when the place can hold it and nothing was done.
        /// </summary>
        private static bool Gone(PartyFile f, IPartyWorld w, Party p, PlaceFacts place, int at)
        {
            if (place.Found && place.Open)
            {
                return false;
            }
            if (p.Own && (!place.Found || place.Made))
            {
                Withdraw(f, w, p, at, "reallife_party_note_off_own");
            }
            else
            {
                CallOff(f, w, p, "reallife_party_note_off_place");
            }
            return true;
        }

        /// <summary>Why the host cannot begin a party now; None when they can: it is planned, its time has come and they are there.</summary>
        public static PartyRefusal CanBegin(Party p, IPartyWorld w)
        {
            PartyRefusal stand = Stand(w);
            if (stand != PartyRefusal.None)
            {
                return stand;
            }
            if (p == null || p.State != PartyState.Planned)
            {
                return p != null && p.State == PartyState.Running ? PartyRefusal.Begun : PartyRefusal.NotPlanned;
            }
            int now = w.Now;
            if (now < p.Start - PartyCatalogue.EarlyMinutes)
            {
                return PartyRefusal.TooEarly;
            }
            if (now > p.Start + PartyCatalogue.MissedMinutes)
            {
                return PartyRefusal.TooLate;
            }
            return w.There(p.Place, p.Street, p.Number) ? PartyRefusal.None : PartyRefusal.NotThere;
        }

        /// <summary>
        /// The host is there and begins: who turns up is settled now, and the party runs to its end. A place found shut or
        /// gone calls it off with everything back; a party nobody came to does nothing and says so.
        /// </summary>
        public static PartyRefusal Begin(PartyFile f, IPartyWorld w, int id)
        {
            Party p = f.Find(id);
            PartyRefusal why = CanBegin(p, w);
            if (why != PartyRefusal.None)
            {
                return why;
            }
            int now = w.Now, from = Math.Max(now, p.Start);
            PlaceFacts place = w.Place(p.Place, p.Street, p.Number, from, Math.Max(1, p.End - from));
            if (Gone(f, w, p, place, now))
            {
                Trim(f);
                return p.Place == PartyPlace.Home ? PartyRefusal.NoHome : PartyRefusal.VenueShut;
            }
            p.Began = now;
            p.Late = now > p.Start + PartyCatalogue.LateMinutes;
            for (int i = 0; i < p.Guests.Count; i++)
            {
                PartyGuest g = p.Guests[i];
                g.Came = g.Answer == PartyAnswer.Yes && PartyRules.ShowsUp(f.Seed, p.Id, w.Person(g.Person), p.Place);
                p.Guests[i] = g;
            }
            if (p.Came == 0)
            {
                p.State = PartyState.Empty;
                w.Tell(PartyTone.Warn, "reallife_party_note_empty", "place", p.Name);
                Trim(f);
                return PartyRefusal.None;
            }
            p.State = PartyState.Running;
            w.Tell(PartyTone.Good, "reallife_party_note_begun", "place", p.Name, "count", p.Came.ToString(), "until", w.When(p.End / 1440, p.End % 1440 / 60));
            return PartyRefusal.None;
        }

        // A party that ran to its end: its quality, and to each who came their share of the week's cap.
        private static void Settle(PartyFile f, IPartyWorld w, Party p)
        {
            p.Left = !w.There(p.Place, p.Street, p.Number);
            int came = p.Came, day = w.Day, gained = 0;
            var home = new HomeFacts { Tidy = -1 };
            if (p.Place == PartyPlace.Home)
            {
                home = w.Home(p.Street, p.Number);
            }
            p.Quality = PartyRules.Quality(p.Tier, p.Place, came, p.Coming, home, p.Late, p.Left);
            bool bad = p.Quality < PartyCatalogue.BadBelow;
            for (int i = 0; i < p.Guests.Count; i++)
            {
                SocialPerson who = p.Guests[i].Came ? w.Person(p.Guests[i].Person) : null;
                if (who == null || !who.Met)
                {
                    continue;
                }
                if (bad)
                {
                    w.Change(who, 0f, PartyCatalogue.BadAffection, 0f);
                    w.Remember(who, MemoryCode.PartyBad);
                    continue;
                }
                int gain = PartyRules.Give(f.Of(who.Index, true), day, PartyCatalogue.WindowDays, PartyRules.Worth(p.Tier, p.Quality, who, p.Place));
                gained += Gain(w, who, gain, day) ? 1 : 0;
                w.Remember(who, MemoryCode.Party);
            }
            if (p.Place == PartyPlace.Home && came > 0)
            {
                w.Mess(came * PartyCatalogue.TidyPerGuest, p.Street, p.Number);
            }
            p.State = PartyState.Held;
            PartyHosted.Tell(p);
            string key = bad ? "reallife_party_note_bad" : p.Quality >= PartyCatalogue.GoodFrom ? "reallife_party_note_good" : "reallife_party_note_fair";
            w.Tell(bad ? PartyTone.Bad : PartyTone.Good, key, "place", p.Name, "count", came.ToString(), "closer", gained.ToString());
        }

        // What a share of the cap gives one person, through the social life's own call; a party that gave half the cap or more counts as a thing done together.
        private static bool Gain(IPartyWorld w, SocialPerson who, int gain, int day)
        {
            who.LastContactDay = day;
            who.LastOutingDay = day;
            who.Drifted = false;
            if (gain <= 0)
            {
                return false;
            }
            if (gain >= PartyCatalogue.SharedAt)
            {
                who.Shared++;
            }
            w.Change(who, PartyRules.Points(PartyCatalogue.CapFamiliarity, gain, who.Familiarity), PartyRules.Points(PartyCatalogue.CapAffection, gain, who.Affection),
                PartyRules.Points(PartyCatalogue.CapTrust, gain, who.Trust));
            return true;
        }

        // The host never came: everybody who said yes stood at the door.
        private static void Miss(PartyFile f, IPartyWorld w, Party p)
        {
            int stood = 0;
            for (int i = 0; i < p.Guests.Count; i++)
            {
                SocialPerson who = p.Guests[i].Answer == PartyAnswer.Yes ? w.Person(p.Guests[i].Person) : null;
                if (who != null && who.Met)
                {
                    w.Change(who, 0f, PartyCatalogue.MissedAffection, PartyCatalogue.MissedTrust);
                    w.Remember(who, MemoryCode.StoodUp);
                    stood++;
                }
            }
            p.State = PartyState.Missed;
            w.Tell(PartyTone.Bad, "reallife_party_note_missed", "place", p.Name, "count", stood.ToString());
        }

        // ------------------------------------------------------------------------------------------------- other people's parties

        /// <summary>
        /// The day's round for invitations, once a day: when the gap since the last is over and nothing is open, a draw
        /// says whether a friend who can host asks the player to a party of theirs in two days, at a place the city has,
        /// by a text of the social life's own. No text, no invitation.
        /// </summary>
        public static Invite InviteDay(PartyFile f, IPartyWorld w)
        {
            int day = w.Day;
            if (Stand(w) != PartyRefusal.None || day - f.LastInvite < PartyCatalogue.InviteGapDays || Roll(f, day) >= PartyCatalogue.InviteChance)
            {
                return null;
            }
            for (int i = 0; i < f.Invites.Count; i++)
            {
                if (f.Invites[i].Open)
                {
                    return null;
                }
            }
            int on = day + PartyCatalogue.InviteAhead, hosts = 0;
            for (int i = 0; i < w.People; i++)
            {
                hosts += CanHost(w.Person(i), on) ? 1 : 0;
            }
            if (hosts == 0)
            {
                return null;
            }
            int pick = PartyRules.Roll(f.Seed, PartyRules.PurposeHost, day, hosts) * hosts / 1000;
            SocialPerson host = null;
            for (int i = 0; i < w.People && host == null; i++)
            {
                SocialPerson p = w.Person(i);
                if (CanHost(p, on) && pick-- == 0)
                {
                    host = p;
                }
            }
            PartyPlace kind = host.Has(SocialTrait.NightOwl) ? PartyPlace.Bar : host.Has(SocialTrait.Foodie) ? PartyPlace.Restaurant
                : PartyRules.Roll(f.Seed, PartyRules.PurposeInvitePlace, day, host.Index) < 500 ? PartyPlace.Restaurant : PartyPlace.Bar;
            PlaceFacts place = w.FindVenue(kind, PartyRules.Stamp(on, PartyCatalogue.InviteHour), PartyCatalogue.InviteMinutes);
            if (!place.Found || !place.Open)
            {
                return null;
            }
            if (!w.Text(host, "reallife_party_text_invite", "place", place.Name, "when", w.When(on, PartyCatalogue.InviteHour)))
            {
                return null;
            }
            var invite = new Invite
            {
                Id = f.NextId++, Person = host.Index, State = InviteState.Asked, Asked = day, Day = on, Hour = PartyCatalogue.InviteHour, Place = kind,
                Street = place.Street ?? string.Empty, Number = place.Number, Name = place.Name ?? string.Empty
            };
            f.Invites.Add(invite);
            f.LastInvite = day;
            return invite;
        }

        private static int Roll(PartyFile f, int day) => PartyRules.Roll(f.Seed, PartyRules.PurposeInvite, day, 0);

        // Somebody who would throw a party and ask the player: a friend or closer, in touch, free and awake at the hour, not family (the family has its own dinners)
        // and not living with the player.
        private static bool CanHost(SocialPerson p, int day)
        {
            return p != null && p.Met && !p.Drifted && !p.IsFamily && !p.MovedIn && p.Stage >= SocialStage.Friend
                   && p.FreeAt(PartyCatalogue.InviteHour, PartyRules.Weekend(day)) && !p.Asleep(PartyCatalogue.InviteHour);
        }

        /// <summary>The player answers an invitation: a yes is a plan to keep; a no costs a little with the host.</summary>
        public static PartyRefusal Reply(PartyFile f, IPartyWorld w, int id, bool yes)
        {
            PartyRefusal stand = Stand(w);
            if (stand != PartyRefusal.None)
            {
                return stand;
            }
            Invite invite = f.Invitation(id);
            if (invite == null || invite.State != InviteState.Asked)
            {
                return PartyRefusal.NotPlanned;
            }
            if (w.Now > invite.Start)
            {
                return PartyRefusal.TooLate;
            }
            SocialPerson host = w.Person(invite.Person);
            invite.State = yes ? InviteState.Accepted : InviteState.Declined;
            if (host != null && host.Met)
            {
                if (yes)
                {
                    host.LastContactDay = w.Day;
                    host.Drifted = false;
                }
                else
                {
                    w.Change(host, 0f, PartyCatalogue.DeclineAffection, 0f);
                }
            }
            return PartyRefusal.None;
        }

        /// <summary>Why the player cannot join a party they said yes to now; None when they can.</summary>
        public static PartyRefusal CanJoin(Invite invite, IPartyWorld w)
        {
            PartyRefusal stand = Stand(w);
            if (stand != PartyRefusal.None)
            {
                return stand;
            }
            if (invite == null || invite.State != InviteState.Accepted)
            {
                return PartyRefusal.NotPlanned;
            }
            if (invite.Began >= 0)
            {
                return PartyRefusal.Begun;
            }
            int now = w.Now;
            if (now < invite.Start - PartyCatalogue.EarlyMinutes)
            {
                return PartyRefusal.TooEarly;
            }
            if (now > invite.Start + PartyCatalogue.MissedMinutes)
            {
                return PartyRefusal.TooLate;
            }
            return w.There(invite.Place, invite.Street, invite.Number) ? PartyRefusal.None : PartyRefusal.NotThere;
        }

        /// <summary>The player is at the door of a party they said yes to: they are in until it ends. A place found shut calls it off, and nobody minds.</summary>
        public static PartyRefusal Join(PartyFile f, IPartyWorld w, int id)
        {
            Invite invite = f.Invitation(id);
            PartyRefusal why = CanJoin(invite, w);
            if (why != PartyRefusal.None)
            {
                return why;
            }
            int now = w.Now, from = Math.Max(now, invite.Start);
            PlaceFacts place = w.Place(invite.Place, invite.Street, invite.Number, from, Math.Max(1, invite.End - from));
            if (!place.Found || !place.Open)
            {
                invite.State = InviteState.Off;
                w.Tell(PartyTone.Warn, "reallife_party_note_invite_off", "name", NameOf(w, invite.Person), "place", invite.Name);
                return PartyRefusal.VenueShut;
            }
            invite.Began = now;
            w.Tell(PartyTone.Good, "reallife_party_note_joined", "name", NameOf(w, invite.Person), "place", invite.Name);
            return PartyRefusal.None;
        }

        private static string NameOf(IPartyWorld w, int person)
        {
            SocialPerson p = w.Person(person);
            return p != null ? p.Name ?? string.Empty : string.Empty;
        }

        // An invitation's turn: never answered, said yes and never came, or been to.
        private static bool InviteTurn(PartyFile f, IPartyWorld w, Invite invite, int now)
        {
            SocialPerson host = w.Person(invite.Person);
            if (host == null || !host.Met)
            {
                invite.State = InviteState.Off;   // lost touch since: nothing to keep, nothing to mind
                return true;
            }
            if (invite.State == InviteState.Asked && now > invite.Start)
            {
                invite.State = InviteState.Lapsed;
                w.Change(host, 0f, PartyCatalogue.LapseAffection, 0f);
                w.Tell(PartyTone.Warn, "reallife_party_note_invite_lapsed", "name", host.Name);
                return true;
            }
            if (invite.State == InviteState.Accepted && invite.Began < 0 && now > invite.Start + PartyCatalogue.MissedMinutes)
            {
                invite.State = InviteState.Missed;
                w.Change(host, 0f, PartyCatalogue.NoShowAffection, PartyCatalogue.NoShowTrust);
                w.Remember(host, MemoryCode.StoodUp);
                w.Tell(PartyTone.Bad, "reallife_party_note_invite_missed", "name", host.Name);
                return true;
            }
            if (invite.State == InviteState.Accepted && invite.Began >= 0 && now >= invite.End)
            {
                if (!w.There(invite.Place, invite.Street, invite.Number))
                {
                    // Presence at the end decides, as for a party the player hosts, whatever the time skip did: gone before it ended, nothing is gained and it counts as a no.
                    invite.State = InviteState.Left;
                    w.Change(host, 0f, PartyCatalogue.LeftAffection, 0f);
                    w.Tell(PartyTone.Warn, "reallife_party_note_invite_left", "name", host.Name, "place", invite.Name);
                    return true;
                }
                invite.State = InviteState.Went;
                int liking = (int)Math.Round(SocialRules.Liking(host, PartyCatalogue.KindOf(invite.Place)) * 1000f);
                int worth = Math.Min(1000, PartyCatalogue.InviteWorth * liking / 1000);
                int gain = PartyRules.Give(f.Of(host.Index, true), w.Day, PartyCatalogue.WindowDays, worth);
                Gain(w, host, gain, w.Day);
                w.Remember(host, MemoryCode.PartyTheirs);
                w.Tell(PartyTone.Good, "reallife_party_note_invite_went", "name", host.Name, "place", invite.Name);
                return true;
            }
            return false;
        }

        // ------------------------------------------------------------------------------------------------- the clock

        /// <summary>
        /// The look at the clock, whenever the game's minute has moved: a reminder an hour before, a planned party whose host
        /// never came, a party at its end, and each invitation's turn. With the option off, or as a guest of a session,
        /// nothing moves and nothing is charged; what came due meanwhile is called off, with everything back and nobody
        /// minding, when it is on again. True if anything changed. A pass over a handful of records.
        /// </summary>
        public static bool Tick(PartyFile f, IPartyWorld w)
        {
            int now = w.Now;
            if (Stand(w) != PartyRefusal.None)
            {
                if (f.OffSince < 0)
                {
                    f.OffSince = now;
                    return true;
                }
                return false;
            }
            bool changed = false;
            int off = f.OffSince;
            f.OffSince = -1;
            changed |= off >= 0;
            for (int i = f.Parties.Count - 1; i >= 0; i--)
            {
                Party p = f.Parties[i];
                if (p.State == PartyState.Planned)
                {
                    int last = p.Start + PartyCatalogue.MissedMinutes;
                    if (now > last)
                    {
                        if (off >= 0 && last >= off)
                        {
                            Withdraw(f, w, p, off, "reallife_party_note_off_while");   // its time came while parties were switched off: the host's own doing, at the hour it was done
                        }
                        else if (!Gone(f, w, p, w.Place(p.Place, p.Street, p.Number, p.Start, Math.Max(1, p.End - p.Start)), now))
                        {
                            Miss(f, w, p);   // the place could hold it, and the host never came
                        }
                        changed = true;
                    }
                    else if (!p.Reminded && now >= p.Start - 60 && now < p.Start)
                    {
                        p.Reminded = true;
                        w.Tell(PartyTone.Info, "reallife_party_note_soon", "place", p.Name, "when", w.When(p.Day, p.Hour));
                        changed = true;
                    }
                }
                else if (p.State == PartyState.Running && now >= p.End)
                {
                    Settle(f, w, p);
                    changed = true;
                }
            }
            for (int i = f.Invites.Count - 1; i >= 0; i--)
            {
                Invite invite = f.Invites[i];
                if (!invite.Open)
                {
                    continue;
                }
                if (off >= 0 && invite.Began < 0 && now > invite.Start && invite.Start + PartyCatalogue.MissedMinutes >= off)
                {
                    invite.State = InviteState.Off;   // it came and went while parties were switched off
                    changed = true;
                    continue;
                }
                if (invite.State == InviteState.Accepted && !invite.Reminded && invite.Began < 0 && now >= invite.Start - 60 && now < invite.Start)
                {
                    invite.Reminded = true;
                    w.Tell(PartyTone.Info, "reallife_party_note_invite_soon", "name", NameOf(w, invite.Person), "place", invite.Name, "when", w.When(invite.Day, invite.Hour));
                    changed = true;
                }
                changed |= InviteTurn(f, w, invite, now);
            }
            changed |= StaffDesk.Turn(f, w, now, off);
            if (changed)
            {
                Trim(f);
            }
            return changed;
        }

        /// <summary>
        /// The day's round, once a day from <see cref="PartyCatalogue.RoundHour"/>: every lift fades to what is still
        /// counted of it, the managers book the events that are due, and an invitation may come. True if it ran.
        /// </summary>
        public static bool Day(PartyFile f, IPartyWorld w, int hour)
        {
            int day = w.Day;
            if (f.Settled == day || hour < PartyCatalogue.RoundHour)
            {
                return false;
            }
            f.Settled = day;
            StaffDesk.Fade(f, w);
            Watch(f, w);
            StaffDesk.ManagerDay(f, w);
            InviteDay(f, w);
            Trim(f);
            return true;
        }

        /// <summary>
        /// The day's look at what the host's own doing may have ended since the booking: a planned party at a venue of theirs that is now shut by their switch or gone from
        /// their hands, and a planned event at a business that is no longer theirs. Each is called off at the moment it was found, by the notice that gives, so closing or
        /// selling something is never a way to a full refund. One place lookup a party at a venue of the player's own, once a day.
        /// </summary>
        private static void Watch(PartyFile f, IPartyWorld w)
        {
            if (Stand(w) != PartyRefusal.None)
            {
                return;
            }
            int now = w.Now;
            for (int i = 0; i < f.Parties.Count; i++)
            {
                Party p = f.Parties[i];
                if (p.State == PartyState.Planned && p.Own && p.Place != PartyPlace.Home)
                {
                    PlaceFacts place = w.Place(p.Place, p.Street, p.Number, p.Start, Math.Max(1, p.End - p.Start));
                    if (!place.Found || (!place.Open && place.Made))
                    {
                        Gone(f, w, p, place, now);
                    }
                }
            }
            StaffDesk.Watch(f, w);
        }

        /// <summary>
        /// The player lost touch with whoever had this place in the cast: their cap, their invitations and their place on
        /// every guest list go with them, so whoever takes the place starts afresh.
        /// </summary>
        public static bool Forget(PartyFile f, int person)
        {
            bool any = false;
            for (int i = f.Lifts.Count - 1; i >= 0; i--)
            {
                if (f.Lifts[i].Person == person && f.Lifts[i].Id.Length == 0)
                {
                    f.Lifts.RemoveAt(i);
                    any = true;
                }
            }
            for (int i = f.Invites.Count - 1; i >= 0; i--)
            {
                if (f.Invites[i].Person == person)
                {
                    f.Invites.RemoveAt(i);
                    any = true;
                }
            }
            for (int i = 0; i < f.Parties.Count; i++)
            {
                List<PartyGuest> guests = f.Parties[i].Guests;
                for (int g = guests.Count - 1; g >= 0; g--)
                {
                    if (guests[g].Person == person)
                    {
                        guests.RemoveAt(g);
                        any = true;
                    }
                }
            }
            return any;
        }

        /// <summary>Keeps the lists bounded: the newest parties and invitations that are over, and every one still to come.</summary>
        public static void Trim(PartyFile f)
        {
            int over = 0;
            for (int i = f.Parties.Count - 1; i >= 0; i--)
            {
                if (f.Parties[i].Over && ++over > PartyCatalogue.HostedMost)
                {
                    f.Parties.RemoveAt(i);
                }
            }
            over = 0;
            for (int i = f.Invites.Count - 1; i >= 0; i--)
            {
                if (!f.Invites[i].Open && ++over > PartyCatalogue.HostedMost)
                {
                    f.Invites.RemoveAt(i);
                }
            }
            for (int i = f.Lifts.Count - 1; i >= 0; i--)
            {
                Lift lift = f.Lifts[i];
                if (lift.Gains.Count == 0 && lift.Stand <= 0)
                {
                    f.Lifts.RemoveAt(i);
                }
            }
        }
    }
}
