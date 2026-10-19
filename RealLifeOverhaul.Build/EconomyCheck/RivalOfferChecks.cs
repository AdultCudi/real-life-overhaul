using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;
using RealLifeOverhaul;

// A rival's offer to an employee who had already left kept its Accept and Decline buttons (a player's screenshot, 19
// Oct 2026, 0.40.2 or 0.40.3). The game's offer (EmployeeInstance.PoachByRival) keeps its two buttons in the message as
// calls on the employee, and only a press takes them away (ContextButton.SetUp); the chat draws them on the newest
// message of a chat (ContactsApp.ShowMessage), most ways an employee leaves send nothing to it, and a save keeps the
// calls with the employee they act on. Decline then sends somebody to the rival a second time (MoveEmployeeToRival:
// another copy among the rival's staff, one of the rival's own let go, the day's wage paid again and a second
// resignation); after the headhunter's replacement, which keeps the employee object under a new name, both buttons act
// on the new hire. Core/RivalOfferGuard empties a rival message's buttons, as a press does, once the rule
// (Core/RivalOfferRules) says it is no longer the player's to answer; business pressure's watch on its own poaching
// reads the same rule. First the rule, then the report and every way of leaving on a model of the game's chat and
// employees as the decompiled game has them, the business watch, the cost, the sources, then the texts.
internal static partial class Program
{
    private const string OfferRival = "special_rival_7";   // a rival's id (RivalData.id)

    private static void RivalOffers()
    {
        RivalOfferKinds();
        RivalOfferStanding();
        RivalOfferRule();
        RivalOfferReport();
        RivalOfferPaths();
        RivalOfferKept();
        RivalOfferSurrender();
        RivalOfferSaves();
        RivalOfferBusiness();
        RivalOfferPace();
        RivalOfferSources();
        RivalOfferWords();
    }

    // ---------------------------------------------------------------- a model of the game's chat and employees

    // One of the player's employees as the game keeps one (EmployeeInstance): what a rival's offer reads and writes.
    private sealed class OfferPerson
    {
        public string Name;
        public float Satisfaction = 70f;
        public bool BeingReplaced;               // isBeingReplaced
        public string RivalId = string.Empty;    // poachedByRivalId
        public int DeadlineDays;                 // poachDeadlineDays (private)
        public int OfferDay = -1;                // lastPoachDay (private)
        public float Wage = 20f, Proposed;       // hourlyWage, poachProposedHourlyWage
        public int WorkedToday = 6;              // workedHoursToday: what Resign's PayWage pays

        public OfferPerson Copy()
        {
            return (OfferPerson)MemberwiseClone();
        }
    }

    // A chat's message: its key, its day, and its buttons as calls on one employee (Target null: the calls are
    // another's, as on Going Public's copy of a partner's message, whose buttons call its own relay).
    private sealed class OfferMessage
    {
        public string Key;
        public int Day;
        public OfferPerson Target;
        public readonly List<string> Buttons = new List<string>();   // "decline", "accept"; a press empties them
    }

    private sealed class OfferChat
    {
        public string Id;   // Contact.id: the name the employee's messages were sent to
        public readonly List<OfferMessage> Messages = new List<OfferMessage>();
    }

    // A way an employee stops being the player's, and whether the game then texts their chat below the offer.
    private sealed class OfferWay
    {
        public string Name;
        public Action<OfferWorld, OfferPerson> Leave;
        public bool Texts;
    }

    // The game around them, with what a press costs counted.
    private sealed class OfferWorld
    {
        public int Day = 170;
        public readonly List<OfferPerson> Staff = new List<OfferPerson>();     // GameInstance.EmployeeInstances
        public readonly List<OfferChat> Chats = new List<OfferChat>();         // GameInstance.Contacts
        public int RivalCopies, RivalOwnStaff = 5;                             // a rival business's poached and own
        public int WagesPaid, Resignations;                                    // PayWage's hours, Resign's calls

        public OfferPerson Hire(string name)
        {
            var p = new OfferPerson { Name = name };
            Staff.Add(p);
            return p;
        }

        public OfferChat Chat(string id)
        {
            foreach (OfferChat c in Chats)
            {
                if (c.Id == id)
                {
                    return c;
                }
            }
            var chat = new OfferChat { Id = id };
            Chats.Add(chat);
            return chat;
        }

        // EmployeeInstance.SendMessage: into the chat of the employee's name as it is now (Contact.GetContact by name).
        public OfferMessage Text(OfferPerson p, string key, bool buttons)
        {
            var m = new OfferMessage { Key = key, Day = Day, Target = p };
            if (buttons)
            {
                m.Buttons.Add("decline");
                m.Buttons.Add("accept");
            }
            Chat(p.Name).Messages.Add(m);
            return m;
        }

        // EmployeeInstance.PoachByRival
        public OfferMessage Offer(OfferPerson p, int days, int raise)
        {
            p.RivalId = OfferRival;
            p.DeadlineDays = days;
            p.Proposed = p.Wage * (1f + raise / 100f);
            p.OfferDay = Day;
            return Text(p, RivalOfferRules.OfferKey, true);
        }

        // ContactsApp.ShowMessage draws buttons on a chat's newest message only, while its list is not empty.
        public static OfferMessage Drawn(OfferChat chat)
        {
            OfferMessage newest = chat.Messages.Count > 0 ? chat.Messages[chat.Messages.Count - 1] : null;
            return newest != null && newest.Buttons.Count > 0 ? newest : null;
        }

        // ContextButton.SetUp's listener: the button's call, then the list emptied. False when no such button is drawn.
        public bool Press(OfferChat chat, string which)
        {
            OfferMessage m = Drawn(chat);
            if (m == null || !m.Buttons.Contains(which))
            {
                return false;
            }
            OfferPerson p = m.Target;
            if (m.Key == RivalOfferRules.OfferKey && which == "decline")
            {
                p.DeadlineDays = 0;                    // DeclinePoachPayRaise
                MoveToRival(p, "ba:messagetype_rivals_poach_employee_decline");
            }
            else if (m.Key == RivalOfferRules.OfferKey)
            {
                p.RivalId = string.Empty;              // AcceptPoachPayRaise
                p.DeadlineDays = 0;
                p.Wage = p.Proposed;
                Text(p, "ba:messagetype_rivals_poach_employee_accept", false);
            }
            else if (which == "accept")
            {
                p.Wage = p.Proposed;                   // StopPoachingRivalSurrender's accept
                p.Satisfaction = 100f;
                Text(p, "ba:messagetype_rivals_poach_employee_raise_accepted", false);
            }
            else
            {
                Text(p, "ba:messagetype_rivals_poach_employee_raise_declined", false);
            }
            m.Buttons.Clear();
            return true;
        }

        // EmployeeInstance.MoveEmployeeToRival without the headhunter: a copy at the rival, one of the rival's own let
        // go, the message, Resign (PayWage) and RemoveEmployee.
        public void MoveToRival(OfferPerson p, string key)
        {
            RivalCopies++;
            RivalOwnStaff--;
            Text(p, key, false);
            Resign(p);
            Staff.Remove(p);
        }

        public void Resign(OfferPerson p)
        {
            WagesPaid += p.WorkedToday;
            Resignations++;
        }

        // EmployeeHelper.RunDaily: an offer past its deadline (HasExpiredPoachDeadline) sends the employee to the
        // rival.
        public void NextDay()
        {
            Day++;
            foreach (OfferPerson p in Staff.ToArray())
            {
                if (p.DeadlineDays > 0 && p.OfferDay + p.DeadlineDays < Day)
                {
                    p.DeadlineDays = 0;                // PoachDeadlineExpired
                    MoveToRival(p, "ba:messagetype_rivals_poach_employee_expired");
                }
            }
        }

        // EmployeeInstance.StopPoachingRivalSurrender: the offer given up, a new message with the raise's two buttons.
        public OfferMessage Surrender(OfferPerson p)
        {
            p.DeadlineDays = 0;
            p.Satisfaction *= 0.6f;
            return Text(p, RivalOfferRules.SurrenderKey, true);
        }

        // HeadhunterPlan.ReplaceEmployee: the same object, a new name, started afresh (ResetEmployee); the proposed
        // wage is kept.
        public void Replace(OfferPerson p, string newName)
        {
            p.Name = newName;
            p.Satisfaction = 100f;
            p.BeingReplaced = false;
            p.RivalId = string.Empty;
            p.DeadlineDays = 0;
            p.OfferDay = -1;
        }

        // Core/RivalOfferGuard's look: every rival message with buttons whose calls are one of the player's employees',
        // judged by the rule; one it takes is emptied. How many were emptied.
        public int Guard(bool known = true)
        {
            int taken = 0;
            foreach (OfferChat chat in Chats)
            {
                foreach (OfferMessage m in chat.Messages)
                {
                    RivalOfferRules.Kind kind = RivalOfferRules.KindOf(m.Key);
                    if (m.Buttons.Count == 0 || kind == RivalOfferRules.Kind.None || m.Target == null)
                    {
                        continue;
                    }
                    if (RivalOfferRules.Of(kind, FactsOf(m.Target, chat, m, known)) != RivalOfferRules.State.Live)
                    {
                        m.Buttons.Clear();
                        taken++;
                    }
                }
            }
            return taken;
        }

        public RivalOfferRules.Facts FactsOf(OfferPerson p, OfferChat chat, OfferMessage m, bool known = true)
        {
            return new RivalOfferRules.Facts
            {
                Employed = Staff.Contains(p),
                SameName = p.Name == chat.Id,
                BeingReplaced = p.BeingReplaced,
                Satisfaction = p.Satisfaction,
                RivalId = p.RivalId,
                Known = known,
                DeadlineDays = p.DeadlineDays,
                OfferDay = p.OfferDay,
                MessageDay = m.Day
            };
        }
    }

    // A city with two employees and the rival's offer to the first on day 170 (three days, as both the game's own
    // poaching at low aggression and business pressure's give).
    private static OfferWorld OfferSetup(out OfferPerson ann, out OfferChat chat)
    {
        var w = new OfferWorld();
        ann = w.Hire("Ann Reed");
        w.Hire("Bob Lee");
        w.Offer(ann, 3, 12);
        chat = w.Chat("Ann Reed");
        return w;
    }

    private static RivalOfferRules.Facts OfferFacts(bool employed = true, bool sameName = true, bool beingReplaced = false,
                                                    float satisfaction = 70f, string rival = OfferRival, bool known = true,
                                                    int deadline = 3, int offerDay = 170, int messageDay = 170)
    {
        return new RivalOfferRules.Facts
        {
            Employed = employed, SameName = sameName, BeingReplaced = beingReplaced, Satisfaction = satisfaction,
            RivalId = rival, Known = known, DeadlineDays = deadline, OfferDay = offerDay, MessageDay = messageDay
        };
    }

    // ---------------------------------------------------------------- the rule

    private static void RivalOfferKinds()
    {
        Check(RivalOfferRules.KindOf("ba:messagetype_rivals_poach_employee_offer") == RivalOfferRules.Kind.Offer
              && RivalOfferRules.KindOf("ba:messagetype_rivals_poach_employee_rival_surrender") == RivalOfferRules.Kind.Surrender,
            "rival offers: the game's offer (PoachByRival) and the rival's giving up (StopPoachingRivalSurrender) are told"
            + " by their keys");
        Check(RivalOfferRules.KindOf("ba:messagetype_rivals_poach_employee_accept") == RivalOfferRules.Kind.None
              && RivalOfferRules.KindOf("ba:messagetype_rivals_poach_employee_decline") == RivalOfferRules.Kind.None
              && RivalOfferRules.KindOf("ba:messagetype_rivals_poach_employee_expired") == RivalOfferRules.Kind.None
              && RivalOfferRules.KindOf("ba:messagetype_rivals_attempting_to_poach") == RivalOfferRules.Kind.None
              && RivalOfferRules.KindOf("BA:MESSAGETYPE_RIVALS_POACH_EMPLOYEE_OFFER") == RivalOfferRules.Kind.None
              && RivalOfferRules.KindOf(null) == RivalOfferRules.Kind.None
              && RivalOfferRules.KindOf(string.Empty) == RivalOfferRules.Kind.None,
            "rival offers: every other message, the answers' own and the rival's notice among them, is not the rule's"
            + " (the keys exact, null and empty none)");
        Check(RivalOfferRules.Of(RivalOfferRules.Kind.None, OfferFacts(employed: false)) == RivalOfferRules.State.Live,
            "rival offers: a message that is not a rival's is never judged, whatever its employee");
    }

    private static void RivalOfferStanding()
    {
        Check(RivalOfferRules.Standing(true, true, false, 70f) == RivalOfferRules.State.Live,
            "rival offers: an employee on the list, under the chat's name, not replaced and content is the player's");
        Check(RivalOfferRules.Standing(false, true, false, 70f) == RivalOfferRules.State.Gone
              && RivalOfferRules.Standing(false, false, true, 0f) == RivalOfferRules.State.Gone,
            "rival offers: one off the player's list is gone, whatever else is true of them");
        Check(RivalOfferRules.Standing(true, false, false, 70f) == RivalOfferRules.State.Replaced,
            "rival offers: one under another name than the chat's was replaced by the headhunter (the same object)");
        Check(RivalOfferRules.Standing(true, true, true, 70f) == RivalOfferRules.State.Replaced
              && RivalOfferRules.Standing(true, true, true, 0f) == RivalOfferRules.State.Replaced,
            "rival offers: one the headhunter is replacing is replaced");
        Check(RivalOfferRules.Standing(true, true, false, 0f) == RivalOfferRules.State.Leaving
              && RivalOfferRules.Standing(true, true, false, -3f) == RivalOfferRules.State.Leaving
              && RivalOfferRules.Standing(true, true, false, float.NaN) == RivalOfferRules.State.Leaving,
            "rival offers: one at no satisfaction has resigned (the game's low-satisfaction resignation cannot be"
            + " answered), below it and an unreadable figure too");
        Check(RivalOfferRules.Standing(true, true, false, 0.0001f) == RivalOfferRules.State.Live
              && RivalOfferRules.Standing(true, true, false, 100f) == RivalOfferRules.State.Live,
            "rival offers: any satisfaction above nothing, the least and the most, keeps them the player's");
    }

    private static void RivalOfferRule()
    {
        RivalOfferRules.Kind offer = RivalOfferRules.Kind.Offer, surrender = RivalOfferRules.Kind.Surrender;
        Check(RivalOfferRules.Of(offer, OfferFacts()) == RivalOfferRules.State.Live,
            "rival offers: an open offer to the player's employee, on its own day, keeps its buttons");
        Check(RivalOfferRules.Of(offer, OfferFacts(deadline: 1)) == RivalOfferRules.State.Live,
            "rival offers: an offer with a day left is still open");
        Check(RivalOfferRules.Of(offer, OfferFacts(rival: string.Empty)) == RivalOfferRules.State.Closed
              && RivalOfferRules.Of(offer, OfferFacts(rival: null)) == RivalOfferRules.State.Closed,
            "rival offers: an offer matched (Accept clears the rival, empty or none) is closed");
        Check(RivalOfferRules.Of(offer, OfferFacts(deadline: 0)) == RivalOfferRules.State.Closed
              && RivalOfferRules.Of(offer, OfferFacts(deadline: -1)) == RivalOfferRules.State.Closed,
            "rival offers: an offer whose days are gone (answered, run out or given up) is closed");
        Check(RivalOfferRules.Of(offer, OfferFacts(messageDay: 150, offerDay: 170)) == RivalOfferRules.State.Older
              && RivalOfferRules.Of(offer, OfferFacts(messageDay: 171, offerDay: 170)) == RivalOfferRules.State.Older,
            "rival offers: an offer message of another day than the open offer's is an older one");
        Check(RivalOfferRules.Of(offer, OfferFacts(employed: false)) == RivalOfferRules.State.Gone
              && RivalOfferRules.Of(offer, OfferFacts(sameName: false)) == RivalOfferRules.State.Replaced
              && RivalOfferRules.Of(offer, OfferFacts(beingReplaced: true)) == RivalOfferRules.State.Replaced
              && RivalOfferRules.Of(offer, OfferFacts(satisfaction: 0f)) == RivalOfferRules.State.Leaving,
            "rival offers: an offer to an employee gone, replaced or resigned loses its buttons with its days still"
            + " running");
        Check(RivalOfferRules.Of(offer, OfferFacts(known: false, deadline: 0, messageDay: 1)) == RivalOfferRules.State.Live
              && RivalOfferRules.Of(offer, OfferFacts(known: false, employed: false)) == RivalOfferRules.State.Gone
              && RivalOfferRules.Of(offer, OfferFacts(known: false, rival: string.Empty)) == RivalOfferRules.State.Closed,
            "rival offers: without the game's two private fields (a newer game) only the employee and the rival are"
            + " judged, never the days");
        Check(RivalOfferRules.Of(surrender, OfferFacts(deadline: 0, messageDay: 172)) == RivalOfferRules.State.Live
              && RivalOfferRules.Of(surrender, OfferFacts(deadline: 0, rival: string.Empty)) == RivalOfferRules.State.Live,
            "rival offers: the rival's giving up keeps its two buttons while its employee is the player's (the rival's"
            + " id left set or not)");
        Check(RivalOfferRules.Of(surrender, OfferFacts(messageDay: 172, offerDay: 192)) == RivalOfferRules.State.Older,
            "rival offers: the rival's giving up loses its buttons once a newer offer is made");
        Check(RivalOfferRules.Of(surrender, OfferFacts(employed: false, deadline: 0, messageDay: 172)) == RivalOfferRules.State.Gone
              && RivalOfferRules.Of(surrender, OfferFacts(sameName: false, deadline: 0, offerDay: -1, messageDay: 172))
              == RivalOfferRules.State.Replaced,
            "rival offers: the rival's giving up loses its buttons once its employee is gone or replaced");
        Check(RivalOfferRules.Lost(RivalOfferRules.State.Gone) && RivalOfferRules.Lost(RivalOfferRules.State.Replaced)
              && RivalOfferRules.Lost(RivalOfferRules.State.Leaving) && !RivalOfferRules.Lost(RivalOfferRules.State.Live)
              && !RivalOfferRules.Lost(RivalOfferRules.State.Closed) && !RivalOfferRules.Lost(RivalOfferRules.State.Older),
            "rival offers: an employee is lost when gone, replaced or resigned, never for an offer that is merely over");
        Check(RivalOfferRules.Kept(RivalOfferRules.State.Live, string.Empty) && RivalOfferRules.Kept(RivalOfferRules.State.Live, null)
              && !RivalOfferRules.Kept(RivalOfferRules.State.Live, OfferRival)
              && !RivalOfferRules.Kept(RivalOfferRules.State.Replaced, string.Empty)
              && !RivalOfferRules.Kept(RivalOfferRules.State.Leaving, string.Empty),
            "rival offers: an employee is kept when still the player's and the offer matched, never one replaced or"
            + " resigned whose rival is cleared");
        var whys = new HashSet<string>(StringComparer.Ordinal);
        bool plain = true;
        foreach (RivalOfferRules.State s in Enum.GetValues(typeof(RivalOfferRules.State)))
        {
            string why = RivalOfferRules.Why(s);
            whys.Add(why);
            plain &= !string.IsNullOrEmpty(why) && !why.Contains("—") && !why.Contains("–") && !why.Contains(" - ")
                     && !why.Contains("!");
        }
        Check(plain && whys.Count == Enum.GetValues(typeof(RivalOfferRules.State)).Length,
            "rival offers: each state has its own plain words for Player.log");
        Check(RivalOfferRules.Of(offer, OfferFacts(messageDay: 150)) == RivalOfferRules.Of(offer, OfferFacts(messageDay: 150)),
            "rival offers: the rule gives the same answer for the same facts (no clock, no chance)");
    }

    // ---------------------------------------------------------------- the report, and every way of leaving

    // The report's case. Ann has the rival's offer of day 170; on day 172 she leaves another way (the company's own
    // offer to her lapsed, and business pressure's leaving had her resign): nothing came to her chat, so the offer is
    // its newest message and the chat draws its two buttons. Without the guard, Decline sends her to the rival again.
    private static void RivalOfferReport()
    {
        OfferWorld before = OfferSetup(out OfferPerson ann, out OfferChat chat);
        before.NextDay();
        before.NextDay();
        ann.Satisfaction = 0f;                            // BusinessGame.Leave
        before.Resign(ann);
        int paid = before.WagesPaid, copies = before.RivalCopies, own = before.RivalOwnStaff;
        OfferMessage drawn = OfferWorld.Drawn(chat);
        Check(drawn != null && drawn.Key == RivalOfferRules.OfferKey && drawn.Buttons.Count == 2,
            "rival offers, the report: after she resigned, her offer is still the newest message of her chat with Decline"
            + " and Accept drawn (the game's own behaviour)");
        before.Press(chat, "decline");
        Check(before.RivalCopies == copies + 1 && before.RivalOwnStaff == own - 1 && before.WagesPaid == paid + ann.WorkedToday
              && before.Resignations == 2,
            "rival offers, the report: without the guard, Decline puts a copy of her among the rival's staff, lets one of"
            + " the rival's own go, pays her day again and resigns her a second time");

        OfferWorld after = OfferSetup(out ann, out chat);
        after.NextDay();
        after.NextDay();
        ann.Satisfaction = 0f;
        after.Resign(ann);
        int taken = after.Guard();                        // RivalOfferGuard.Look, from BusinessGame.Leave
        paid = after.WagesPaid;
        copies = after.RivalCopies;
        own = after.RivalOwnStaff;
        bool pressed = after.Press(chat, "decline") | after.Press(chat, "accept");
        Check(taken == 1 && OfferWorld.Drawn(chat) == null && !pressed && after.RivalCopies == copies
              && after.RivalOwnStaff == own && after.WagesPaid == paid && after.Resignations == 1,
            "rival offers, the report: with the guard her offer loses its buttons as she resigns; nothing is drawn to"
            + " press, and nobody is moved, paid or resigned again");
        after.Staff.Remove(ann);                          // the game's hour takes her off the list
        after.NextDay();
        Check(after.Guard() == 0 && OfferWorld.Drawn(chat) == null && after.RivalCopies == copies,
            "rival offers, the report: her offer stays without buttons after she has gone, and her deadline then moves"
            + " nobody");
    }

    // Every way the game or this mod has an employee leave, each before the offer's deadline: the offer's buttons go, a
    // press on it can do nothing; without the guard the stale press does harm.
    private static void RivalOfferPaths()
    {
        var ways = new List<OfferWay>
        {
            new OfferWay { Name = "fired in MyEmployees (RemoveEmployee, no message)", Leave = (w, p) => w.Staff.Remove(p) },
            new OfferWay
            {
                Name = "let go by business pressure or the company (BusinessGame.Leave: resigned until the hour)",
                Leave = (w, p) => { p.Satisfaction = 0f; w.Resign(p); }
            },
            new OfferWay
            {
                Name = "a complaint's resignation after the offer (ComplaintResign: off the list in the same hour)",
                Leave = (w, p) => { p.Satisfaction = 0f; w.Resign(p); w.Staff.Remove(p); }
            },
            new OfferWay { Name = "sold with a business (ServicesGame.Sale: RemoveEmployee)", Leave = (w, p) => w.Staff.Remove(p) },
            new OfferWay { Name = "the tech sector's dismissal (TalentGame.Fire: RemoveEmployee)", Leave = (w, p) => w.Staff.Remove(p) },
            new OfferWay
            {
                Name = "replaced at once by the headhunter (a retirement: the same object renamed, no message)",
                Leave = (w, p) => w.Replace(p, "Ray Hart")
            },
            new OfferWay
            {
                Name = "the headhunter replacing them (isBeingReplaced, its message below the offer)",
                Leave = (w, p) => { p.BeingReplaced = true; w.Text(p, "ba:messagetype_headhunter_expected_completion_days_range", false); },
                Texts = true
            },
            new OfferWay
            {
                Name = "moved to another player by Going Public (another object on the list)",
                Leave = (w, p) => { w.Staff.Remove(p); w.Staff.Add(p.Copy()); }
            },
        };
        foreach (OfferWay way in ways)
        {
            // Without the guard: what a press of the offer's buttons does once the employee has gone this way.
            OfferWorld stale = OfferSetup(out OfferPerson p, out OfferChat chat);
            stale.NextDay();
            way.Leave(stale, p);
            int copies = stale.RivalCopies, paid = stale.WagesPaid;
            if (!way.Texts)
            {
                Check(OfferWorld.Drawn(chat) != null, "rival offers, " + way.Name + ": the game leaves the offer the newest"
                                                      + " message with its buttons drawn");
                stale.Press(chat, "decline");
                Check(stale.RivalCopies == copies + 1 && stale.WagesPaid > paid,
                    "rival offers, " + way.Name + ": without the guard Decline moves and pays somebody again");
            }

            // With the guard.
            OfferWorld w = OfferSetup(out p, out chat);
            w.NextDay();
            way.Leave(w, p);
            int taken = w.Guard();
            int staff = w.Staff.Count;
            copies = w.RivalCopies;
            paid = w.WagesPaid;
            float wage = p.Wage;
            chat.Messages.RemoveAll(m => m.Key != RivalOfferRules.OfferKey);   // the cross on a later message: newest again
            bool pressed = w.Press(chat, "decline") | w.Press(chat, "accept");
            Check(taken == 1 && !pressed && w.Staff.Count == staff && w.RivalCopies == copies && w.WagesPaid == paid
                  && p.Wage == wage,
                "rival offers, " + way.Name + ": the guard takes the offer's buttons; even as the newest message nothing"
                + " can be pressed, and nobody is moved, paid, removed or given a wage");
        }

        // The deadline: the game sends its own message under the offer, which hides the buttons; the cross on that
        // message brings the offer back to the bottom of the chat, buttons and all, and Decline would move her again.
        OfferWorld lapse = OfferSetup(out OfferPerson ann, out OfferChat lapsed);
        for (int d = 0; d < 4; d++)
        {
            lapse.NextDay();
        }
        Check(!lapse.Staff.Contains(ann) && lapse.RivalCopies == 1 && OfferWorld.Drawn(lapsed) == null,
            "rival offers, the deadline: she goes to the rival and the game's message hides the offer's buttons");
        lapsed.Messages.RemoveAt(lapsed.Messages.Count - 1);
        Check(OfferWorld.Drawn(lapsed) != null,
            "rival offers, the deadline: deleting the game's message brings the offer's buttons back (the game's own"
            + " behaviour)");
        Check(lapse.Guard() == 1 && OfferWorld.Drawn(lapsed) == null && !lapse.Press(lapsed, "decline") && lapse.RivalCopies == 1,
            "rival offers, the deadline: the guard takes them; nobody goes to the rival twice");

        // After the headhunter's replacement both buttons act on the new hire: Accept gives her the old offer's wage.
        OfferWorld swap = OfferSetup(out ann, out OfferChat old);
        swap.Replace(ann, "Ray Hart");
        float proposed = ann.Proposed;
        swap.Press(old, "accept");
        Check(Math.Abs(ann.Wage - proposed) < 0.001f && ann.Name == "Ray Hart",
            "rival offers, the headhunter: without the guard Accept on the old offer gives the new hire the old offer's"
            + " wage");
        swap = OfferSetup(out ann, out old);
        swap.Replace(ann, "Ray Hart");
        float wage0 = ann.Wage;
        Check(swap.Guard() == 1 && !swap.Press(old, "accept") && ann.Wage == wage0 && swap.Staff.Contains(ann),
            "rival offers, the headhunter: with the guard the new hire keeps their wage and their place");
    }

    // An offer still open to the player keeps its buttons exactly, and they do what the game's do.
    private static void RivalOfferKept()
    {
        OfferWorld w = OfferSetup(out OfferPerson ann, out OfferChat chat);
        OfferMessage offer = OfferWorld.Drawn(chat);
        List<string> list = offer.Buttons;
        w.NextDay();
        w.NextDay();
        Check(w.Guard() == 0 && ReferenceEquals(offer.Buttons, list) && list.Count == 2 && list[0] == "decline"
              && list[1] == "accept",
            "rival offers: an open offer keeps its two buttons, in their order, its list untouched, every day of its three");
        ann.Satisfaction = 4f;                            // unhappy, complaining, or unassigned: still hers to answer
        Check(w.Guard() == 0 && OfferWorld.Drawn(chat) == offer,
            "rival offers: an unhappy employee's offer, short of no satisfaction, stays open");
        Check(w.Press(chat, "accept") && Math.Abs(ann.Wage - ann.Proposed) < 0.001f && ann.RivalId.Length == 0
              && offer.Buttons.Count == 0 && w.RivalCopies == 0,
            "rival offers: Accept on an open offer matches the wage as the game does, and the game empties the buttons");
        Check(w.Guard() == 0, "rival offers: an answered offer has nothing left for the guard");

        OfferWorld d = OfferSetup(out ann, out chat);
        Check(d.Guard() == 0, "rival offers: an offer made today keeps its buttons");
        Check(d.Press(chat, "decline") && !d.Staff.Contains(ann) && d.RivalCopies == 1 && d.WagesPaid == ann.WorkedToday,
            "rival offers: Decline on an open offer lets the employee go to the rival once, as the game does");

        // Going Public's copy of a partner's message: its buttons call Going Public's relay, not an employee.
        OfferWorld gp = OfferSetup(out ann, out chat);
        gp.Staff.Remove(ann);
        OfferMessage copy = gp.Text(new OfferPerson { Name = "A partner's employee" }, RivalOfferRules.OfferKey, true);
        copy.Target = null;
        Check(gp.Guard() == 1 && copy.Buttons.Count == 2,
            "rival offers, Going Public: a copy of a partner's offer, whose calls are the relay's, keeps its buttons; the"
            + " player's own stale one loses them");
    }

    // The rival gives up: the open offer is over, the giving-up's own buttons are the player's until a newer offer.
    private static void RivalOfferSurrender()
    {
        OfferWorld w = OfferSetup(out OfferPerson ann, out OfferChat chat);
        OfferMessage offer = OfferWorld.Drawn(chat);
        w.NextDay();
        OfferMessage gaveUp = w.Surrender(ann);
        Check(w.Guard() == 1 && offer.Buttons.Count == 0 && gaveUp.Buttons.Count == 2,
            "rival offers, a rival beaten: the offer it gave up loses its buttons, the giving-up message keeps its two");
        chat.Messages.Remove(gaveUp);
        Check(!w.Press(chat, "decline") && w.Staff.Contains(ann) && w.RivalCopies == 0,
            "rival offers, a rival beaten: with the giving-up deleted, the old offer is drawn without buttons and cannot"
            + " send her to a rival that is gone");
        chat.Messages.Add(gaveUp);
        for (int d = 0; d < 20; d++)
        {
            w.NextDay();
        }
        OfferMessage second = w.Offer(ann, 2, 16);        // the next offer may come 20 days after the last (IsPoachable)
        Check(w.Guard() == 1 && gaveUp.Buttons.Count == 0 && second.Buttons.Count == 2,
            "rival offers, a newer offer: the giving-up loses its buttons, the new offer keeps them");

        // An older offer the guard never saw end (its giving up was missed): the open offer is the newer one.
        OfferWorld two = OfferSetup(out ann, out chat);
        OfferMessage first = OfferWorld.Drawn(chat);
        ann.DeadlineDays = 0;                             // given up while the guard was not looking
        two.Day += 21;
        OfferMessage next = two.Offer(ann, 3, 12);
        Check(two.Guard() == 1 && first.Buttons.Count == 0 && next.Buttons.Count == 2,
            "rival offers, an older offer: of two offers to one employee only the open one keeps its buttons");
    }

    // The save keeps the buttons as calls on the employee (Odin keeps a call's object and method): after a load, an
    // employee still on the list is the same object as the calls'; one let go before the save is a copy nobody holds.
    private static void RivalOfferSaves()
    {
        OfferWorld w = OfferSetup(out OfferPerson ann, out OfferChat chat);
        OfferPerson bob = w.Staff[1];
        OfferMessage bobs = w.Offer(bob, 3, 12);
        w.Staff.Remove(ann);                              // fired before the save
        OfferMessage hers = OfferWorld.Drawn(chat);
        hers.Target = ann.Copy();                         // the load: her calls' object, alone
        Check(w.Guard() == 1 && hers.Buttons.Count == 0 && bobs.Buttons.Count == 2,
            "rival offers, an older save: the offer to an employee gone before the save loses its buttons on load; one"
            + " still open keeps them");
        Check(w.Guard(false) == 0 && bobs.Buttons.Count == 2,
            "rival offers, an older save on a newer game (the private fields not read): the open offer is still kept");
    }

    // Business pressure's watch on its own poaching says lost or kept by the same rule.
    private static void RivalOfferBusiness()
    {
        Check(!RivalOfferRules.Lost(RivalOfferRules.Standing(true, true, false, 55f)),
            "business pressure: an employee who is only unassigned (no business) is not lost to the rival");
        Check(RivalOfferRules.Lost(RivalOfferRules.Standing(true, false, false, 100f))
              && !RivalOfferRules.Kept(RivalOfferRules.Standing(true, false, false, 100f), string.Empty),
            "business pressure: one the headhunter has replaced under a new name is lost, not kept, though the new hire"
            + " has no rival");
        Check(RivalOfferRules.Lost(RivalOfferRules.Standing(true, true, true, 70f)),
            "business pressure: one the headhunter is replacing is lost");
        Check(RivalOfferRules.Lost(RivalOfferRules.Standing(true, true, false, 0f))
              && RivalOfferRules.Lost(RivalOfferRules.Standing(false, true, false, 70f)),
            "business pressure: one resigned or off the list is lost");
        Check(RivalOfferRules.Kept(RivalOfferRules.Standing(true, true, false, 70f), string.Empty)
              && !RivalOfferRules.Kept(RivalOfferRules.Standing(true, true, false, 70f), OfferRival),
            "business pressure: one still the player's with the offer matched is kept; with the offer open, neither");
    }

    // The rule is a few comparisons: a million judgements well inside a frame's budget.
    private static void RivalOfferPace()
    {
        RivalOfferRules.Facts f = OfferFacts(messageDay: 169);
        int live = 0;
        Stopwatch watch = Stopwatch.StartNew();
        for (int i = 0; i < 1000000; i++)
        {
            f.MessageDay = 169 + (i & 1);
            live += RivalOfferRules.Of(RivalOfferRules.Kind.Offer, f) == RivalOfferRules.State.Live ? 1 : 0;
        }
        watch.Stop();
        Check(live == 500000 && watch.Elapsed.TotalMilliseconds < 250.0,
            "rival offers: a million judgements take " + watch.Elapsed.TotalMilliseconds.ToString("0.0", CultureInfo.InvariantCulture)
            + " ms (under 250)");
    }

    // ---------------------------------------------------------------- the sources

    private static void RivalOfferSources()
    {
        string guard = ModSource(@"Core\RivalOfferGuard.cs");
        string watch = ModSource(@"Core\RivalOfferWatch.cs");
        string start = ModSource(@"Core\RealLifeOverhaulMod.cs");
        string contract = ModSource(@"Core\GameContract.cs");
        Check(guard.Contains("EventHold.Keep(ref GameEvent.onGameEventTriggered, _onGameEvent, ref _gameEventSeen)")
              && guard.Contains("_onGameEvent = Guard.Wrap<string>(FrameGuard, OnGameEvent)")
              && guard.Contains("\"ba:gameevent_newhour\"") && guard.Contains("\"ba:gameevent_rivaldefeated\"")
              && guard.Contains("name.Length > 0 && name != NewHour && name != RivalDefeated"),
            "rival offers: the guard keeps its handler on the game's event, made once by the guard, and looks at the end"
            + " of each hour, after a rival is beaten and after the unnamed ones (the game's firing)");
        Check(guard.Contains("Sweep(\"the save just loaded\")") && guard.Contains("_watch = RivalOfferWatch.Install()")
              && guard.Contains("new WaitForSecondsRealtime(1f)") && guard.Contains("StartCoroutine(Guard.Routine(Tick()))"),
            "rival offers: a save is looked through once it has loaded, and the watch is put on each city's Contacts app,"
            + " on a one-second timer inside the guard");
        Check(guard.Contains("RivalOfferRules.Of(kind, FactsOf(e, chat.id, m, staff))") && guard.Contains("buttons.Clear();")
              && guard.Contains("call.Target as EmployeeInstance") && guard.Contains("ReferenceEquals(staff[i], e)")
              && guard.Contains("string.Equals(e.characterData.name, name, StringComparison.Ordinal)")
              && guard.Contains("DeadlineField = GameContract.OfferDeadline.Field")
              && guard.Contains("OfferDayField = GameContract.OfferDay.Field"),
            "rival offers: the guard judges each rival message by the rule, from the very employee its calls are on, the"
            + " chat's name and the two private fields, and empties the buttons as a press does");
        Check(guard.Contains("GoingPublic.Foreign(reg)") && guard.Contains("if (e == null || Foreign(e))")
              && !guard.Contains("SharedCity") && !guard.Contains("GoingPublicLink"),
            "rival offers, Going Public: another player's employee and a copy of a partner's message are left alone, and"
            + " nothing is sent");
        Check(guard.Contains("app.OpenAppWithContact(chat)")
              && guard.Contains("app.isActiveAndEnabled && ReferenceEquals(app.selectedContact, chat)"),
            "rival offers: a chat on screen that lost buttons is drawn again by the game's own call");
        Check(watch.Contains("_app.selectedContact") && watch.Contains("RivalOfferGuard.Picked(chat, false)")
              && watch.Contains("RivalOfferGuard.Picked(watch._seen, true)")
              && Regex.IsMatch(watch, @"private void OnEnable\(\)\s*\{\s*_seen = null;")
              && watch.Contains("app.gameObject.AddComponent<RivalOfferWatch>()")
              && watch.Contains("using var hotSpot = HotSpots.Time(HotUpdate);") && watch.Contains("Guard.Fail(FrameGuard, e, this)"),
            "rival offers: the watch sits on the Contacts app, judges a chat as it is picked (before the game draws it)"
            + " and afresh each time the app opens, timed and guarded");
        Check(start.Contains("Guard.Add<RivalOfferGuard>(_root)") && start.Contains("RivalOfferGuard.Logger = context.Logger"),
            "rival offers: the guard is started with the city, with its logger");
        Check(contract.Contains("OfferDeadline = Optional(Field(RivalOffers, Game, \"Entities.EmployeeInstance\", \"poachDeadlineDays\", \"Int32\"))")
              && contract.Contains("OfferDay = Optional(Field(RivalOffers, Game, \"Entities.EmployeeInstance\", \"lastPoachDay\", \"Int32\"))")
              && contract.Contains("ContractBook.Part(\"rival_offers\", null,"),
            "rival offers: the offer's two private fields are lines of the game contract, optional, under a part of their own");

        // Every place this mod lets one of the game's employees go asks the guard at once, so a new one cannot be added
        // without it.
        string business = ModSource(@"Business\BusinessGame.cs");
        string tech = ModSource(@"Tech\People\TalentGame.cs");
        string sale = ModSource(@"Economy\Services\ServicesGame.Sale.cs");
        Check(MemberText(business, "public static void Leave(EmployeeInstance e)").Contains("RivalOfferGuard.Look(")
              && MemberText(tech, "public static void Resign(EmployeeInstance e)").Contains("RivalOfferGuard.Look(")
              && MemberText(tech, "public static void Fire(EmployeeInstance e)").Contains("RivalOfferGuard.Look(")
              && MemberText(sale, "public static SaleAfter Complete(").Contains("RivalOfferGuard.Look("),
            "rival offers: business pressure's leaving (the company's too), the tech sector's resignation and dismissal,"
            + " and a sale with its staff each ask the guard at once");
        int sites = 0, asked = 0;
        string root = System.IO.Path.GetFullPath(System.IO.Path.Combine(AppContext.BaseDirectory,
                                                                        @"..\..\..\..\..\Assets\Mods\RealLifeOverhaul\Scripts"));
        foreach (string file in System.IO.Directory.GetFiles(root, "*.cs", System.IO.SearchOption.AllDirectories))
        {
            string code = System.IO.File.ReadAllText(file);
            foreach (Match call in Regex.Matches(code, @"\.(RemoveEmployee|Resign)\(\);"))
            {
                sites++;
                int end = code.IndexOf("\n        }", call.Index, StringComparison.Ordinal);   // the end of its method
                asked += end > 0 && code.IndexOf("RivalOfferGuard.Look(", call.Index, end - call.Index, StringComparison.Ordinal) >= 0
                    ? 1
                    : 0;
            }
        }
        Check(sites >= 4 && asked == sites,
            "rival offers: each of the mod's " + sites + " calls that have a game employee resign or removed is followed"
            + " in its method by the guard's look (" + asked + ")");

        string issues = ModSource(@"Business\BusinessIssues.cs");
        string watchPoach = MemberText(issues, "public static void Watch(BusinessIssue issue, BuildingRegistration reg)");
        Check(watchPoach.Contains("BusinessGame.Standing(e, RefName(issue))") && watchPoach.Contains("RivalOfferRules.Lost(standing)")
              && watchPoach.Contains("RivalOfferRules.Kept(standing, e.poachedByRivalId)")
              && !watchPoach.Contains("assignedAddress == null") && !watchPoach.Contains("satisfaction <= 0f"),
            "business pressure: its watch on its poaching says lost and kept by the guard's rule, not by an empty"
            + " business or the satisfaction alone");
    }

    // ---------------------------------------------------------------- the texts

    private static void RivalOfferWords()
    {
        Check(FixLineEverywhere("no longer keeps its Accept and Decline buttons", "não fica mais com os botões Aceitar e Recusar",
                                "不再保留“接受”和“拒绝”按钮", "behält seine Schaltflächen Annehmen und Ablehnen nicht mehr",
                                "ne garde plus ses boutons Accepter et Refuser", "ya no conserva sus botones Aceptar y Rechazar",
                                "больше не сохраняет кнопки «Принять» и «Отклонить»",
                                "houdt niet langer de knoppen Accepteren en Weigeren", "Kabul et ve Reddet düğmelerini korumuyor"),
            "rival offers: What's new has the fix's line, with its fix mark, in some row of every language");
        Dictionary<string, string>[] langs = LanguageWords(out string[] codes);
        if (!LanguagesRead("rival offers", langs, codes))
        {
            return;
        }
        const string key = "reallife_contract_part_rival_offers";
        bool all = true;
        for (int l = 0; l < langs.Length; l++)
        {
            bool has = langs[l].TryGetValue(key, out string text) && text.Length > 10;
            all &= has && (l == 0 || text != langs[0][key]) && !text.Contains("!") && !text.Contains("—")
                   && !text.Contains("–") && !text.Contains(" - ");
        }
        Check(all && codes.Length == ModLanguages.All.Length,
            "rival offers: the contract part's name is in every language, each its own, with no dash and no exclamation mark");
    }
}
