using System;
using System.Collections.Generic;
using BigAmbitions.DayNightCycle;
using Localizor;

namespace RealLifeOverhaul
{
    public static partial class Wiring
    {
        /// <summary>
        /// Everyday life: who the player knows, where they work, how they are, where they travel.
        ///
        ///   SocialLinks.Law            <see cref="Standing"/>: people know of a record and of a good name
        ///   SocialLinks.Wellbeing      an evening out takes stress off, and a meal out is a meal (Scripts/Home)
        ///   CareerLinks.Law            the line about the record on a CV
        ///   CareerLinks.Health         a doctor's sick note from Scripts/Home counts as one at work
        ///   Justice.Bridges.Social     a close friend from Scripts/Social speaks in court and visits in prison
        ///   TravelBridges.Justice      nobody leaves town with a warrant out or a court date inside the trip
        ///   TravelBridges.Home         a hotel's shower, a night's rest and a holiday act on hygiene and stress
        ///   TravelBridges.Social       the player's partner and close friends come along; souvenirs are for friends
        ///   GarageBridges.RoadIsWet    wet in the rain of Events/Weather as well as the game's
        ///   HomeBridges.Sheltered      up in a hotel room is indoors: no rain, no cold from it
        ///   HomeBridges.SleepingAway   a night or a nap in a hotel room is sleep, in the room's bed
        ///   LifeAccounts.Home          Home's residential lookup: a home of the player's, wherever they stand
        ///
        /// Left at their defaults, each because the other side has nothing to answer with (see the list in docs/reference/wiring.md):
        /// CareerLinks.Social, TravelBridges.Transport and Leisure, Justice.Bridges.Transport,
        /// GarageBridges.PoliceNotice and RiderFalls, Events.Weather.Wardrobe.
        /// </summary>
        public static class LifeLinks
        {
            private const float RestPerHour = 3f;        // stress a comfortable hour takes off, before the room's quality
            private const string CompanionPrefix = "social:";

            internal static void Connect()
            {
                SocialLinks.Law = new RecordForFriends();
                SocialLinks.Wellbeing = new HomeForFriends();
                CareerLinks.Law = new RecordForEmployers();
                CareerLinks.Health = new SickNote();
                Justice.Bridges.Social = new FriendsForCourt();
                TravelBridges.Justice = new CourtForTravel();
                TravelBridges.Home = new HomeForTravel();
                if (Settings.Social)
                {
                    TravelBridges.Social = new FriendsForTravel();   // (travel hands companions and gifts over entirely: only while there is a social life)
                }
                GarageBridges.RoadIsWet = () => RainHelper.AreRainDropsFalling || Events.Weather.Sky >= Events.Sky.Rain;
                HomeBridges.Sheltered = () => TravelState.InRoom;
                HomeBridges.SleepingAway = () => HotelStay.SleepingBed;
                LifeAccounts.Home = HasHome;
            }

            internal static void Disconnect()
            {
                SocialLinks.Law = null;
                SocialLinks.Wellbeing = null;
                CareerLinks.Law = null;
                CareerLinks.Health = null;
                Justice.Bridges.Social = null;
                TravelBridges.Justice = null;
                TravelBridges.Home = null;
                TravelBridges.Social = null;
                GarageBridges.RoadIsWet = null;
                HomeBridges.Sheltered = null;
                HomeBridges.SleepingAway = null;
                LifeAccounts.Home = null;
            }

            private static bool HasHome()
            {
                try
                {
                    return HomeTidy.HasHome;
                }
                catch (Exception e)
                {
                    Fault("LifeLinks.HasHome", e);
                    return false;
                }
            }

            private static bool NeedsOn => HomeState.Loaded && Settings.HomeLife && Settings.HomeNeeds;

            private static bool FriendsOn => SocialState.Loaded && Settings.Social;

            // The person nearest the player at or above a stage: the partner first, then by stage.
            private static SocialPerson Closest(SocialStage atLeast)
            {
                SocialPerson best = null;
                for (int i = 0; FriendsOn && i < SocialState.Count; i++)
                {
                    SocialPerson p = SocialState.Get(i);
                    if (p != null && p.Met && p.Stage >= atLeast && (best == null || p.Stage > best.Stage))
                    {
                        best = p;
                    }
                }
                return best;
            }

            private static SocialPerson Named(string name)
            {
                for (int i = 0; FriendsOn && i < SocialState.Count; i++)
                {
                    SocialPerson p = SocialState.Get(i);
                    if (p != null && p.Met && p.Name == name)
                    {
                        return p;
                    }
                }
                return null;
            }

            private sealed class RecordForFriends : ISocialLaw
            {
                public bool HasRecord => Standing.HasRecord;
                public int Reputation => Standing.Public;
            }

            private sealed class HomeForFriends : ISocialWellbeing
            {
                public void Stress(float change)
                {
                    if (NeedsOn)
                    {
                        HomeState.Stress = HomeState.Clamp(HomeState.Stress + change, 0f, 100f);
                    }
                }

                public void Ate()
                {
                    if (HomeState.Loaded && Settings.HomeLife)
                    {
                        HomeState.Ate(Meal.Plain);   // a proper meal, not one cooked at home
                    }
                }
            }

            private sealed class RecordForEmployers : ICareerRecord
            {
                public bool Clean => !Standing.HasRecord && !Standing.Charged;

                public string Summary
                {
                    get
                    {
                        int convictions = Standing.Convictions;
                        return convictions > 0 ? "reallife_wiring_cv_convictions".GetLocalization().Replace("{count}", convictions.ToString())
                            : Standing.Charged ? "reallife_wiring_cv_charged".GetLocalization()
                            : Standing.HasRecord ? "reallife_wiring_cv_arrests".GetLocalization()
                            : null;
                    }
                }
            }

            private sealed class SickNote : ICareerSickNote
            {
                public bool Covers(int day)
                {
                    // The doctor's note (Scripts/Home), or the mod's older sickness: what the career ladder counts with nothing set.
                    int today = TimeHelper.CurrentDay;
                    float dayBegan = TimeHelper.NowInMinutes() - (today - day) * 1440f;
                    bool note = HomeState.Loaded && HomeState.SickNoteUntil > 0f && (day >= today ? HomeHealth.SickNoteActive : HomeState.SickNoteUntil >= dayBegan);
                    return note || (day >= today ? ModState.IsSick : ModState.SickUntil > 0f && ModState.SickUntil >= dayBegan);
                }
            }

            private sealed class FriendsForCourt : ISocialBridge
            {
                private readonly DefaultSocialBridge _alone = new DefaultSocialBridge();   // the Friend contact, when the social life is off

                public bool CloseFriend(out string name)
                {
                    if (!FriendsOn)
                    {
                        return _alone.CloseFriend(out name);
                    }
                    SocialPerson friend = Closest(SocialStage.CloseFriend);
                    name = friend != null ? friend.Name : null;
                    return friend != null;
                }

                public void Stood(string name, bool visit)
                {
                    SocialPerson friend = Named(name);
                    if (friend == null)
                    {
                        _alone.Stood(name, visit);
                        return;
                    }
                    SocialLife.Contacted(friend);
                    SocialLife.Change(friend, 0f, visit ? 3f : 1f, visit ? 4f : 2f);   // they showed up: it is remembered
                    ModState.AddModifier(ModState.Social);
                }
            }

            private sealed class CourtForTravel : ITravelJustice
            {
                public bool MayLeaveTown(int days, out string reasonKey)
                {
                    reasonKey = null;
                    if (!JusticeState.Loaded || !JusticeSettings.Enabled)
                    {
                        return true;
                    }
                    if (Justice.Wanted)
                    {
                        reasonKey = "reallife_wiring_travel_warrant";
                        return false;
                    }
                    int today = TimeHelper.CurrentDay;
                    for (int i = 0; i < JusticeState.Cases.Count; i++)
                    {
                        JusticeCase c = JusticeState.Cases[i];
                        if (c.Open && (c.Stage == CaseStage.Bail || (c.Stage == CaseStage.Pending && c.CourtDay >= today && c.CourtDay <= today + days)))
                        {
                            reasonKey = "reallife_wiring_travel_court";
                            return false;
                        }
                    }
                    return true;
                }

                public List<string> MissedWhileAway(int fromDay, int toDay) => null;   // (nothing can be missed: a trip over a court date is refused)
            }

            private sealed class HomeForTravel : ITravelHome
            {
                public bool Freshen(int quality)
                {
                    if (!NeedsOn)
                    {
                        return false;
                    }
                    HomeState.Hygiene = 100f;
                    return true;
                }

                public bool Laundry(int quality) => false;   // (home life keeps no count of clean clothes)

                public bool Rest(int hours, int quality)
                {
                    if (!NeedsOn)
                    {
                        return false;
                    }
                    HomeState.Stress = HomeState.Clamp(HomeState.Stress - hours * (RestPerHour + quality), 0f, 100f);
                    return true;
                }

                public bool Holiday(int days)
                {
                    if (!NeedsOn)
                    {
                        return false;
                    }
                    HomeState.Stress = 0f;
                    HomeState.StressHighHours = 0f;
                    return true;
                }

                public int HighStressDays => NeedsOn ? (int)(HomeState.StressHighHours / 24f) : -1;

                public bool? HasBed => null;
            }

            private sealed class FriendsForTravel : ITravelSocial
            {
                public void Companions(List<TravelCompanion> into)
                {
                    if (!FriendsOn)
                    {
                        if (PhoneContacts.Friend != null)
                        {
                            into.Add(new TravelCompanion { Id = "reallife-friend", Name = TravelText.T("reallife-friend") });   // as with nobody connected
                        }
                        return;
                    }
                    // The partner first, then close friends.
                    for (int stage = (int)SocialStage.Partner; stage >= (int)SocialStage.CloseFriend; stage--)
                    {
                        for (int i = 0; i < SocialState.Count; i++)
                        {
                            SocialPerson p = SocialState.Get(i);
                            if (p != null && p.Met && (int)p.Stage == stage)
                            {
                                into.Add(new TravelCompanion { Id = CompanionPrefix + p.Index, Name = p.Name });
                            }
                        }
                    }
                }

                public void TripTogether(string companionId, int days)
                {
                    SocialPerson p = companionId != null && companionId.StartsWith(CompanionPrefix, StringComparison.Ordinal)
                                     && int.TryParse(companionId.Substring(CompanionPrefix.Length), out int index) ? SocialState.Get(index) : null;
                    if (p != null)
                    {
                        SocialLife.Contacted(p);
                        SocialLife.Change(p, 4f, 3f + Math.Min(days, 5), 3f);
                    }
                    ModState.AddModifier(ModState.Social, 24);
                }

                public int Souvenirs(int count)
                {
                    int given = 0;
                    for (int i = 0; FriendsOn && i < SocialState.Count && given < count; i++)
                    {
                        SocialPerson p = SocialState.Get(i);
                        if (p != null && p.Met && p.Stage >= SocialStage.Friend)
                        {
                            SocialLife.Change(p, 0f, 3f, 1f);
                            given++;
                        }
                    }
                    if (given > 0)
                    {
                        ModState.AddModifier(ModState.Generous);
                    }
                    return given;
                }

                public bool TableBooked(string hotelName) => false;
                public List<string> MissedWhileAway(int fromDay, int toDay) => null;
            }
        }
    }
}
