using System;
using BigAmbitions.DayNightCycle;
using Extensions;
using Helpers;
using Localizor;
using RealLifeOverhaul.Flare;
using RealLifeOverhaul.Texts;
using UnityEngine;

namespace RealLifeOverhaul
{
    public static partial class Wiring
    {
        /// <summary>
        /// An invitation in a Flare message with the social life (Social/Flare/FlareDirector.Invites.cs), one line each:
        ///
        ///   InviteLook    what the social life knows for the decision, by the phone's own rules for a plan made by a call
        ///                 (SocialDialogs.Plans, When, Agree): who they are (a city person who said yes once is in People
        ///                 under the name they have on Flare; somebody new is who they would be when met), a plan on already,
        ///                 their night hours, a rough day, a stood-up date, a rude word or money not repaid remembered, the
        ///                 outing their stage allows (OutingDef.Needs, a workout with somebody not sporty), and a place near
        ///                 the player with the first hour from the yes's arrival on that suits both and at which the place is
        ///                 open for the whole outing (SocialLife.PlaceAndHour, as a call finds them)
        ///   InviteBook    the plan booked exactly as a call books it (SocialLife.MakePlan: its reminder an hour before, the
        ///                 job panel's line and the game's marker, late and stood-up as for any plan); somebody new is met
        ///                 online first, as when they answer a post (FlareLinks.MeetOnline: SocialOrigin.Online,
        ///                 SocialLife.Meet), and is who they are on Flare: their name, and their sex and age where Flare's
        ///                 say them (a person of the cast not yet met takes them on; never one a friend is bringing, or a star)
        ///
        /// Nothing of the social life is changed but through its own entries. COST: nothing per frame; a look per invitation
        /// sent (a walk over the city's buildings for each free hour tried, at most SocialRules.OpenTries, as a call), a
        /// booking per yes.
        /// </summary>
        public static class FlareInviteLinks
        {
            private const float PlaceFrom = 40f;     // metres: a place this far from the player at least, and at most...
            private const float PlaceTo = 1800f;     // ...this far (the phone's own distances, SocialDialogs.Plans)

            internal static void Connect()
            {
                FlareBridges.InviteLook = Look;
                FlareBridges.InviteBook = Book;
            }

            internal static void Disconnect()
            {
                FlareBridges.InviteLook = null;
                FlareBridges.InviteBook = null;
            }

            // What the decision needs; for an invitation that could be a yes, the place and the time too.
            private static void Look(FlareInviteAsk a)
            {
                try
                {
                    a.Off = !Settings.Social || !Settings.SocialOutings || !SocialState.Loaded || SocialDirector.Instance == null;
                    a.Outing = SocialLife.T(SocialCatalogue.Outing(a.Kind).Name);
                    if (a.Off)
                    {
                        return;
                    }
                    SocialPerson p = a.Influencer ? null : Known(a.First, a.Last);
                    a.Known = p != null;
                    if (p == null && !a.Influencer)
                    {
                        p = Stranger(a.Sex);
                        a.NobodyNew = p == null;
                    }
                    a.Cast = p != null ? p.Index : -1;
                    SocialPlan plan = SocialState.Plan;
                    if (plan.Active && a.Known && plan.Person == p.Index)
                    {
                        a.PlanWithThem = true;
                        a.PlanKind = plan.Kind;
                        a.Outing = SocialLife.T(SocialCatalogue.Outing(plan.Kind).Name);
                        a.Place = plan.Place;
                        a.When = When(plan.Time);
                        a.CardWhen = CardWhen(plan.Time);
                        return;
                    }
                    a.PlanOther = plan.Active;
                    if (a.Known)
                    {
                        a.Asleep = p.Asleep(a.Hour);
                        a.LowMood = p.Mood <= -2;
                        a.Dislikes = p.Remembers(MemoryCode.StoodUp) || p.Remembers(MemoryCode.Rude) || p.Remembers(MemoryCode.NotRepaid);
                    }
                    if (p == null || a.PlanOther || a.Dislikes || (!a.Known && (a.Taken || !FlareInviteRules.MayMeet(a))))
                    {
                        return;   // (no yes can come of it: no place is looked for)
                    }
                    SocialStage stage = a.Known ? p.Stage : SocialStage.Acquaintance;   // (somebody new is an acquaintance once met)
                    OutingDef def = SocialCatalogue.Outing(a.Kind);
                    a.NeedsMore = (stage < def.Needs && !p.IsFamily) || (a.Kind == PlanKind.Workout && !p.Has(SocialTrait.Sporty) && stage < SocialStage.Friend);
                    if (a.NeedsMore)
                    {
                        return;
                    }
                    // The place and the hour together: a place open for the whole outing at one of their free hours (SocialLife.PlaceAndHour).
                    PlanFind found = SocialLife.PlaceAndHour(p, a.Kind, SocialDirector.Where(), PlaceFrom, PlaceTo, a.From, out Venue venue, out float time);
                    if (found == PlanFind.NoHour)
                    {
                        a.NoSlot = true;
                        return;
                    }
                    if (found != PlanFind.Found || string.IsNullOrEmpty(venue.Street))
                    {
                        a.NoPlace = true;
                        return;
                    }
                    a.Place = venue.Name;
                    a.Street = venue.Street;
                    a.Number = venue.Number;
                    a.PlanAt = (long)time;
                    a.When = When(time);
                    a.CardWhen = CardWhen(time);
                }
                catch (Exception e)
                {
                    Fault("FlareInviteLinks.Look", e);
                    a.Off = true;
                }
            }

            // A yes's plan booked as a call books it; -1 if it cannot be now (a plan on already, the place gone, the person not there).
            private static int Book(FlareInviteBook b)
            {
                try
                {
                    if (!Settings.Social || !Settings.SocialOutings || !SocialState.Loaded || SocialDirector.Instance == null || SocialState.Plan.Active
                        || !SocialPlaces.Resolve(b.Street, b.Number, out Venue venue))
                    {
                        return -1;
                    }
                    SocialPerson p = b.Cast >= 0 ? SocialState.Get(b.Cast) : Known(b.First, b.Last) ?? Stranger(b.Sex);
                    if (p == null || (b.Cast >= 0 && !p.Met))
                    {
                        return -1;
                    }
                    if (!p.Met)
                    {
                        MeetOnline(p, b);
                    }
                    if (!SocialLife.MakePlan(p, b.Kind, b.At, venue, false, out string reason))
                    {
                        SocialDirector.Logger?.Info("Social: the plan agreed on Flare with " + p.Name + " could not be made (" + reason + ").");
                        return -1;
                    }
                    return p.Index;
                }
                catch (Exception e)
                {
                    Fault("FlareInviteLinks.Book", e);
                    return -1;
                }
            }

            // Somebody new said yes on Flare: met online, as they are on Flare (their name; their sex, age and face where Flare says them).
            private static void MeetOnline(SocialPerson p, FlareInviteBook b)
            {
                if (!string.IsNullOrEmpty(b.First) && !string.IsNullOrEmpty(b.Last))
                {
                    p.Name = b.First + " " + b.Last;
                }
                if (b.Sex >= 0)
                {
                    p.Female = b.Sex == 1;
                }
                if (b.Age > 0)
                {
                    p.Age = Mathf.Clamp(b.Age, 18, 85);   // (a retiree of the city is 82 at most: the age their face is drawn at)
                }
                if (b.Face > 0)
                {
                    p.Seed = b.Face;   // the face they have on Flare, in the street and on their contact's picture as well
                }
                p.Origin = SocialOrigin.Online;
                ContactPictures.Person(SocialPhone.IdOf(p), p.Female, p.Age, p.Seed);   // (their face under the name they now have: the contact's picture)
                SocialLife.Meet(p, true);
                SocialDirector.Logger?.Info("Social: " + p.Name + " said yes to an invitation on Flare: met online.");
            }

            // A person of the social life met already under a city person's name (they said yes on Flare once), or null.
            private static SocialPerson Known(string first, string last)
            {
                if (string.IsNullOrEmpty(first) || string.IsNullOrEmpty(last))
                {
                    return null;
                }
                string name = first + " " + last;
                for (int i = 0; i < SocialRules.CastSize; i++)
                {
                    SocialPerson p = SocialState.Get(i);
                    if (p != null && p.Met && p.Name == name)
                    {
                        return p;
                    }
                }
                return null;
            }

            // The person of the cast somebody new would be when met: one not yet met, not being brought by a friend, not a star
            // influencer's, of the sex their name says if one is (1 a woman, 0 a man, -1 either); null if nobody is left.
            private static SocialPerson Stranger(int sex)
            {
                FlareFile file = FlareDirector.Instance != null ? FlareDirector.Instance.File : null;
                SocialPerson either = null;
                for (int i = 0; i < SocialRules.CastSize; i++)
                {
                    SocialPerson p = SocialState.Get(i);
                    if (p == null || p.Met || p.IsFamily || p.Origin == SocialOrigin.OldFriend || SocialDirector.Expected(p) || (file != null && FlareCast.IsOne(file, p.Index)))
                    {
                        continue;
                    }
                    if (sex < 0 || p.Female == (sex == 1))
                    {
                        return p;
                    }
                    either = either ?? p;
                }
                return either;
            }

            // A plan's day and hour in the game's language, always with the day ("Monday 18:00"): the answer is read later.
            private static string When(float minutes)
            {
                var stamp = new Timestamp(minutes);
                return TimeHelper.GetDayOfWeek(Mathf.Max(1, stamp.Day)).GetLocalization() + " " + stamp.Hour.GetFormattedTime(0f, true);
            }

            // ...and as a card says it ("Monday 18:00", TextWords.When).
            private static string CardWhen(float minutes)
            {
                var stamp = new Timestamp(minutes);
                int weekday = (int)TimeHelper.GetDayOfWeek(Mathf.Max(1, stamp.Day)) - (int)DayOfWeekOrdered.Monday;
                return TextWords.When(weekday, stamp.Hour, Mathf.FloorToInt(stamp.Minute) % 60);
            }
        }
    }
}
