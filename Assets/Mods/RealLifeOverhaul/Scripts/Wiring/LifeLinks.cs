using System;
using System.Collections.Generic;
using BigAmbitions.DayNightCycle;
using Localizor;

namespace RealLifeOverhaul
{
    public static partial class Wiring
    {
        /// <summary>
        /// Everyday life: the social life, careers, the courts, travel, home life and the garage with each other. Left at
        /// their defaults because the other side has nothing to answer with (docs/reference/wiring.md): CareerLinks.Social,
        /// TravelBridges.Transport and Leisure, Justice.Bridges.Transport, GarageBridges.PoliceNotice and RiderFalls. The
        /// cold's energy cost asks what the player wears (Events.Weather.Wardrobe) once an hour. Every random line's needs (a fridge,
        /// a flat, a landlord, a car) are read through LineNeeds.Read, from the game, when a line is drawn.
        /// </summary>
        public static class LifeLinks
        {
            private const float RestPerHour = 3f;        // stress a comfortable hour takes off, before the room's quality
            private const string CompanionPrefix = "social:";

            internal static void Connect()
            {
                SocialLinks.Law = new RecordForFriends();
                SocialLinks.Repute = Repute;
                SocialLinks.Wellbeing = new HomeForFriends();
                SocialLinks.SightCount = SightCount;
                SocialLinks.NearSight = NearSight;
                CareerLinks.Law = new RecordForEmployers();
                CareerLinks.Health = new SickNote();
                Justice.Bridges.Social = new FriendsForCourt();
                TravelBridges.Justice = new CourtForTravel();
                TravelBridges.Home = new HomeForTravel();
                TravelBridges.Family = new FamilyForTravel();   // each call asks the family feature, which says nobody while it is off
                if (Settings.Social)
                {
                    TravelBridges.Social = new FriendsForTravel();   // travel hands companions and gifts over entirely
                }
                GarageBridges.RoadIsWet = () => RainHelper.AreRainDropsFalling || Events.Weather.Sky >= Events.Sky.Rain;
                HomeBridges.Sheltered = () => TravelState.InRoom;
                HomeBridges.SleepingAway = () => HotelStay.SleepingBed;
                LifeAccounts.Home = HasHome;
                LineNeedsGame.Logger = Logger;
                LineNeeds.Read = LineNeedsGame.Holds;   // what every random line takes for granted, read from the game (Core/LineNeeds)
                Events.Weather.Wardrobe = PlayerWardrobe;
            }

            internal static void Disconnect()
            {
                SocialLinks.Law = null;
                SocialLinks.Repute = null;
                SocialLinks.Wellbeing = null;
                SocialLinks.SightCount = null;
                SocialLinks.NearSight = null;
                CareerLinks.Law = null;
                CareerLinks.Health = null;
                Justice.Bridges.Social = null;
                TravelBridges.Justice = null;
                TravelBridges.Home = null;
                TravelBridges.Family = null;
                TravelBridges.Social = null;
                GarageBridges.RoadIsWet = null;
                HomeBridges.Sheltered = null;
                HomeBridges.SleepingAway = null;
                LifeAccounts.Home = null;
                LineNeeds.Read = null;
                Events.Weather.Wardrobe = null;
            }

            // What the city could have heard of the player, for a first impression (SocialImpression): Law's public standing and record, Standing's
            // recognition, integrity and scandal, and the player's Flare followers. Asked once, when somebody is met.
            private static SocialRepute Repute()
            {
                SocialRepute repute = SocialRepute.Newcomer;
                try
                {
                    repute.Public = Standing.Public;
                    repute.HasRecord = Standing.HasRecord;
                    repute.Charged = Standing.Charged;
                    repute.Recognition = Standing.Recognition;
                    repute.Integrity = Standing.Integrity;
                    RealLifeOverhaul.Fame.FameDirector fame = RealLifeOverhaul.Fame.FameDirector.Instance;
                    RealLifeOverhaul.Fame.FameFile file = fame != null ? fame.File : null;
                    repute.Scandal = file != null && RealLifeOverhaul.Fame.FameCity.InScandal(file, SocialLife.Day);
                    RealLifeOverhaul.Flare.FlareDirector flare = RealLifeOverhaul.Flare.FlareDirector.Instance;
                    repute.Followers = flare != null && flare.File != null ? flare.File.Followers : 0;
                }
                catch (Exception e)
                {
                    Fault("LifeLinks.Repute", e);
                }
                return repute;
            }

            // Leisure's list of sights, found once behind the loading screen: a walk is set near one when it can be.
            private static int SightCount()
            {
                try
                {
                    return Settings.Leisure ? Leisure.LeisureVenues.Landmarks.Count : 0;
                }
                catch (Exception e)
                {
                    Fault("LifeLinks.SightCount", e);
                    return 0;
                }
            }

            private static bool NearSight(UnityEngine.Vector3 at)
            {
                try
                {
                    var sights = Leisure.LeisureVenues.Landmarks;
                    for (int i = 0; i < sights.Count; i++)
                    {
                        Leisure.Landmark sight = sights[i];
                        if (SocialDateRules.PleasantSight(sight.Id) && SocialDateRules.NearSight(sight.Position.x - at.x, sight.Position.z - at.z, sight.Radius))
                        {
                            return true;
                        }
                    }
                }
                catch (Exception e)
                {
                    Fault("LifeLinks.NearSight", e);
                }
                return false;
            }

            private static readonly Events.IWardrobe PlayerWardrobe = new WornForWeather();

            // Whether the player is dressed for the cold: a warm top of the game's own or the mod's bomber.
            private sealed class WornForWeather : Events.IWardrobe
            {
                public bool WearingCoat
                {
                    get
                    {
                        try
                        {
                            return CharacterCatalogue.WearingWarmTop();
                        }
                        catch (Exception e)
                        {
                            Fault("LifeLinks.WearingCoat", e);
                            return false;
                        }
                    }
                }
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

            // The person closest to the player at or above a stage: the partner first, then by stage.
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
                    // Home's doctor's note, or the mod's older sickness, which the career ladder counts with nothing set.
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

                public List<string> MissedWhileAway(int fromDay, int toDay) => null;   // a trip over a court date is refused
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

                public bool Laundry(int quality) => false;   // home life keeps no count of clean clothes

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

            private sealed class FamilyForTravel : ITravelFamily
            {
                public bool Members(out string partner, List<string> children) => RealLifeOverhaul.Family.FamilyApi.HolidayFamily(out partner, children);

                public void TripTogether(bool partner, List<string> children, int days) => RealLifeOverhaul.Family.FamilyApi.HolidayTogether(partner, children, days);
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
