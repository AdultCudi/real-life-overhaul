using System;
using System.Collections.Generic;
using BigAmbitions.DayNightCycle;
using RealLifeOverhaul.Law;
using FamilyApi = RealLifeOverhaul.Family.FamilyApi;
using FamilyBridges = RealLifeOverhaul.Family.FamilyBridges;
using FamilyEvent = RealLifeOverhaul.Family.FamilyEvent;
using FamilyHomeBridge = RealLifeOverhaul.Family.IHomeBridge;
using FamilyLawBridge = RealLifeOverhaul.Family.ILawBridge;
using FamilyPhone = RealLifeOverhaul.Family.FamilyPhone;
using FamilySocialBridge = RealLifeOverhaul.Family.ISocialBridge;
using PersonInfo = RealLifeOverhaul.Family.PersonInfo;

namespace RealLifeOverhaul
{
    public static partial class Wiring
    {
        /// <summary>
        /// Family life with the features it leans on. The doors that are filled in:
        ///
        ///   FamilyBridges.Social     the partner is one of the people of Scripts/Social (the one the player is dating or with),
        ///                            what happens between the two moves that person's figures, the wedding's guests are the close people,
        ///                            and a job the partner retrained for is that person's job in People
        ///   FamilyBridges.Home       a partner who looks after the player when ill shortens the illness; a baby's broken night is owed sleep
        ///   FamilyBridges.Law        whether the player is locked up (a cell or a prison term): family life waits
        ///   SocialLinks.PartnerChanged   a break-up or a new partner in Social reaches the family at once
        ///   SocialPhone.FamilyMatters    the partner's People call has time apart and the in-laws while there is no Family contact
        ///   HomeFridgeButton.Family*     the fridge's Family button opens the family's board then
        ///
        /// An arrest is told to the family from <see cref="Arrests"/>, once, as it happens.
        ///
        /// Left at their defaults, each because the other side has nothing to answer with: FamilyBridges.Travel (a trip is bought at
        /// the travel agent's desk and run by the travel feature; it cannot be started for somebody else), FamilyBridges.Leisure (an
        /// evening or an outing there is an activity at a venue for the player, not a date with a named companion), and from Home
        /// the shared cooking and television and the health visit (Home has no routine for two, or for recording a visit). Social's
        /// own outings are planned with a venue and a time by its people, so StartOuting answers false and Family runs its short evening.
        /// </summary>
        public static class FamilyLinks
        {
            internal static void Connect()
            {
                FamilyBridges.Social = new SocialForFamily();
                FamilyBridges.Home = new HomeForFamily();
                FamilyBridges.Law = new LawForFamily();
                SocialLinks.PartnerChanged = TellFamily;
                SocialPerson.FreeOf = (p, hour, weekend) => p != null && PeopleOn && p == SocialState.Partner ? FamilyApi.PartnerFree(hour, weekend) : null;
                SocialPhone.HasFamilyMatters = p => p != null && PeopleOn && p == SocialState.Partner && FamilyPhone.HasCallMatters;
                SocialPhone.FamilyMatters = FamilyPhone.CallMatters;
                HomeFridgeButton.FamilyShown = () => FamilyPhone.BoardShown;
                HomeFridgeButton.FamilyClicked = FamilyPhone.Board;
            }

            internal static void Disconnect()
            {
                SocialLinks.PartnerChanged = null;
                SocialPerson.FreeOf = null;
                SocialPhone.HasFamilyMatters = null;
                SocialPhone.FamilyMatters = null;
                HomeFridgeButton.FamilyShown = null;
                HomeFridgeButton.FamilyClicked = null;
                FamilyBridges.Reset();
            }

            // Social lost its partner or has a new one.
            private static void TellFamily()
            {
                try
                {
                    FamilyApi.PartnerChanged();
                }
                catch (Exception e)
                {
                    Fault("FamilyLinks.PartnerChanged", e);
                }
            }

            private static bool PeopleOn => SocialState.Loaded && Settings.Social;

            private static PersonInfo Info(SocialPerson p)
            {
                return new PersonInfo
                {
                    Id = p.Index.ToString(),
                    Name = p.Name,
                    LookSeed = p.Seed,
                    Female = p.Female,
                    Affection = p.Affection,
                    Trust = p.Trust,
                    Stage = (int)p.Stage,
                    DaysTogether = Math.Max(0, TimeHelper.CurrentDay - p.StageDay),  // since the stage they are in began
                    LastContactDay = p.LastContactDay,                                 // Social's calls, texts, outings and meetings
                    JobKey = SocialCatalogue.Jobs[Math.Max(0, Math.Min(p.Job, SocialCatalogue.Jobs.Length - 1))],
                    FreeFrom = p.FreeFrom,
                    FreeTo = p.FreeTo,
                    NightOwl = p.Has(SocialTrait.NightOwl),
                    Leaning = Family.PartnerWealthRules.LeaningOfTraits(p.Has(SocialTrait.Supportive), p.Has(SocialTrait.Envious)),
                    Spend = Family.PartnerExpectRules.SpendOfTraits(p.Has(SocialTrait.Careful), p.Has(SocialTrait.Envious), p.Has(SocialTrait.Generous))
                };
            }

            private sealed class SocialForFamily : FamilySocialBridge
            {
                public bool Active => Settings.Social && SocialDirector.Instance != null;

                public bool Ready => SocialState.Loaded;

                public bool TryGetPartner(out PersonInfo partner)
                {
                    partner = default;
                    try
                    {
                        SocialPerson p = PeopleOn ? SocialState.Partner : null;
                        if (p == null || !p.Met)
                        {
                            return false;
                        }
                        partner = Info(p);
                        return true;
                    }
                    catch (Exception e)
                    {
                        Fault("FamilyLinks.TryGetPartner", e);
                        return false;
                    }
                }

                public void Changed(FamilyEvent what, float affection, float trust)
                {
                    try
                    {
                        SocialPerson p = PeopleOn ? SocialState.Partner : null;
                        if (p == null)
                        {
                            return;
                        }
                        if (what == FamilyEvent.Divorced)
                        {
                            // The family ended it: the figures fall, and Social is told it is over without its own announcements.
                            SocialRules.Change(p, 0f, affection, trust);
                            p.Stage = SocialStage.Acquaintance;
                            p.StageDay = TimeHelper.CurrentDay;
                            p.MovedIn = false;
                            p.Dates = 0;
                            SocialLife.Remember(p, MemoryCode.BreakUp);
                            if (SocialState.Plan.Active && SocialState.Plan.Person == p.Index)
                            {
                                SocialState.Plan.Clear();
                            }
                            return;
                        }
                        if (what == FamilyEvent.MovedIn)
                        {
                            p.MovedIn = true;
                            SocialLife.Remember(p, MemoryCode.MovedIn);
                        }
                        else if (what == FamilyEvent.MovedOut)
                        {
                            p.MovedIn = false;
                        }
                        else if (what == FamilyEvent.SharedTime)
                        {
                            p.Shared++;
                        }
                        if (affection >= 0f && trust >= 0f)
                        {
                            SocialLife.Contacted(p);
                        }
                        SocialLife.Change(p, 0f, affection, trust);
                    }
                    catch (Exception e)
                    {
                        Fault("FamilyLinks.Changed", e);
                    }
                }

                public bool StartOuting(string kind, Action<bool> done) => false;

                public Entities.Contact PartnerContact
                {
                    get
                    {
                        try
                        {
                            SocialPerson p = PeopleOn ? SocialState.Partner : null;
                            return p != null && p.Met ? SocialPhone.ContactOf(p) : null;
                        }
                        catch (Exception e)
                        {
                            Fault("FamilyLinks.PartnerContact", e);
                            return null;
                        }
                    }
                }

                public bool PartnerJob(string jobKey)
                {
                    try
                    {
                        SocialPerson p = PeopleOn ? SocialState.Partner : null;
                        int job = Array.IndexOf(SocialCatalogue.Jobs, jobKey);
                        if (p == null || !p.Met || job < 0)
                        {
                            return false;
                        }
                        p.Job = job;   // saved with the person (SocialState): People, the texts and Flare all read it
                        return true;
                    }
                    catch (Exception e)
                    {
                        Fault("FamilyLinks.PartnerJob", e);
                        return false;
                    }
                }

                public int ClosePeople(List<PersonInfo> into, int most)
                {
                    int added = 0;
                    try
                    {
                        if (!PeopleOn)
                        {
                            return 0;
                        }
                        SocialPerson partner = SocialState.Partner;
                        for (int pass = 0; pass < 2; pass++)
                        {
                            // Family first, then the rest from the closest down.
                            for (int stage = (int)SocialStage.CloseFriend; stage >= (int)SocialStage.Friend; stage--)
                            {
                                for (int i = 0; i < SocialRules.CastSize && added < most; i++)
                                {
                                    SocialPerson p = SocialState.Get(i);
                                    if (p != null && p.Met && p != partner && (int)p.Stage == stage && p.IsFamily == (pass == 0))
                                    {
                                        into.Add(Info(p));
                                        added++;
                                    }
                                }
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        Fault("FamilyLinks.ClosePeople", e);
                    }
                    return added;
                }
            }

            private sealed class HomeForFamily : FamilyHomeBridge
            {
                public bool CookTogether(string partner) => false;

                public bool WatchTogether(string partner) => false;

                public bool HealthVisit(string reason) => false;

                public bool CareWhenSick(string partner)
                {
                    try
                    {
                        if (!HomeState.Loaded || !Settings.HomeLife || !HomeHealth.Ill)
                        {
                            return false;
                        }
                        HomeHealth.Shorten(8f * 60f);   // a night of being looked after, as the default takes off the old sickness
                        ModState.AddModifier(ModState.MoodUp);
                        return true;
                    }
                    catch (Exception e)
                    {
                        Fault("FamilyLinks.CareWhenSick", e);
                        return false;
                    }
                }

                public bool BrokenNight(int wakings)
                {
                    try
                    {
                        if (!HomeState.Loaded || !Settings.HomeLife || !Settings.HomeSleep)
                        {
                            return false;
                        }
                        HomeState.SleepDebt = HomeState.Clamp(HomeState.SleepDebt + 0.5f * Math.Max(0, wakings), 0f, HomeBalance.DebtMost);   // half an hour up, each time
                        return true;
                    }
                    catch (Exception e)
                    {
                        Fault("FamilyLinks.BrokenNight", e);
                        return false;
                    }
                }
            }

            private sealed class LawForFamily : FamilyLawBridge
            {
                public bool InCustody
                {
                    get
                    {
                        try
                        {
                            return Custody.Active || LawState.Custody.Active
                                   || (JusticeState.Loaded && (JusticeState.InPrison || JusticeState.JailHoursPending > 0));
                        }
                        catch (Exception e)
                        {
                            Fault("FamilyLinks.InCustody", e);
                            return false;
                        }
                    }
                }
            }
        }
    }
}
