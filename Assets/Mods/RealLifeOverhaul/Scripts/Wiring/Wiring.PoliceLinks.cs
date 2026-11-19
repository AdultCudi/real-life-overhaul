using System;
using System.Collections.Generic;
using Helpers;
using RealLifeOverhaul.Careers;
using RealLifeOverhaul.Health;
using RealLifeOverhaul.Law;
using RealLifeOverhaul.Permadeath;
using UnityEngine;

namespace RealLifeOverhaul
{
    public static partial class Wiring
    {
        /// <summary>
        /// The police career (Careers/Police) with the rest of the mod:
        ///
        ///   Precincts   the posts are at the city's police stations (Law), one a district
        ///   Conduct     the record, the courts, the driving licence and the player's health: who may join, who may go on patrol, who is let go
        ///   Incident    the nearest incident an officer on a shift can answer: a street crime (Crime), a road accident (Traffic) or a person lying in the road (Ragdoll)
        ///   Settle      the scene plays out what the officer's move came to
        ///   Hurt        an injury on duty: Health names it as an accident at work and Permadeath judges it as it judges every danger
        ///   GigOn       the two kinds of shift share the player's work clothes, so only one is on
        ///   OnDuty      the crime roll sends an officer where the crime is, and a minor traffic stop of theirs ends in a word (Crime)
        ///   Duty        the front desk's entry: go on patrol, end the shift (Law)
        ///
        /// Each is asked once a second at most, on an event or on a press; never per frame. A part that is away answers as nothing to offer, and a fault is said
        /// once (<see cref="Fault"/>) and the bridge's default answered.
        /// </summary>
        public static class PoliceLinks
        {
            internal static void Connect()
            {
                CareersBridges.Precincts = Precincts;
                CareersBridges.Conduct = Conduct;
                CareersBridges.Incident = Incident;
                CareersBridges.Settle = Settle;
                CareersBridges.Hurt = Hurt;
                CareersBridges.GigOn = () => JobDirector.OnShift;
                CityWatch.OnDuty = () => CareersDirector.Instance != null && CareersDirector.Instance.DutyOn;
                Law.Bridges.Duty = DeskView;
                Law.Bridges.DutyPress = DeskPress;
            }

            internal static void Disconnect()
            {
                CareersBridges.Precincts = null;
                CareersBridges.Conduct = null;
                CareersBridges.Incident = null;
                CareersBridges.Settle = null;
                CareersBridges.Hurt = null;
                CareersBridges.GigOn = null;
                CityWatch.OnDuty = null;
                Law.Bridges.Duty = null;
                Law.Bridges.DutyPress = null;
            }

            // ------------------------------------------------------------------------------------------------- the posts

            private static void Precincts(List<PrecinctView> into)
            {
                try
                {
                    int count = LawDirector.PrecinctCount;
                    for (int i = 0; i < count; i++)
                    {
                        Precinct precinct = LawDirector.PrecinctAt(i);
                        string address = precinct != null ? precinct.Address : null;
                        int bar = address != null ? address.IndexOf('|') : -1;
                        if (bar > 0 && int.TryParse(address.Substring(bar + 1), System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out int number))
                        {
                            into.Add(new PrecinctView { Name = precinct.Name, Street = address.Substring(0, bar), Number = number });
                        }
                    }
                }
                catch (Exception e)
                {
                    Fault("PoliceLinks.Precincts", e);
                }
            }

            // ------------------------------------------------------------------------------------------------- the player

            // days: how far back to look (int.MaxValue: ever). Serious is a stay in a cell or a warrant in that time, Convicted a court's conviction in it.
            private static PoliceRules.Conduct Conduct(int days)
            {
                var c = new PoliceRules.Conduct { SinceOffence = -1, Licence = true, Sound = true, Fit = true };
                try
                {
                    int today = SaveGameManager.Current != null ? TimeHelper.CurrentDay : 0;
                    c.SinceOffence = Law.Record.DaysSinceLastOffence;
                    c.Serious = Standing.Arrested(days);
                    c.Convicted = days == int.MaxValue ? Standing.Convictions > 0
                        : JusticeState.Loaded && JusticeState.LastConvictionDay > 0 && today - JusticeState.LastConvictionDay <= days;
                    c.Licence = !LicenceState.Loaded || LicenceState.Valid(LicenceClass.Car, today);
                    HealthDirector health = HealthDirector.Instance;
                    if (health != null)
                    {
                        c.Fit = !health.Ailing(out bool serious);
                        c.Sound = !serious;
                    }
                }
                catch (Exception e)
                {
                    Fault("PoliceLinks.Conduct", e);
                }
                return c;
            }

            // ------------------------------------------------------------------------------------------------- the incidents

            private static DutyCall Incident()
            {
                try
                {
                    PlayerController player = PlayerHelper.PlayerController;
                    if (player == null)
                    {
                        return default;
                    }
                    Vector3 at = player.transform.position;
                    DutyCall best = default;
                    float bestSqr = float.MaxValue;
                    if (CrimeDirector.Duty(out string id, out Vector3 focus, out int key) && PoliceRules.Crime(id, out DutyKind kind, out int danger))
                    {
                        Consider(ref best, ref bestSqr, DutySource.Crime, kind, danger, key, focus, at, id);
                    }
                    if (TrafficAccidents.Duty(out Vector3 spot, out int severity, out bool person))
                    {
                        int akey = unchecked(Mathf.RoundToInt(spot.x * 2f) * 7919 + Mathf.RoundToInt(spot.z * 2f)) | 1;
                        Consider(ref best, ref bestSqr, person ? DutySource.Casualty : DutySource.Accident, person ? DutyKind.Casualty : DutyKind.Accident,
                            person ? PoliceRules.CasualtyDanger : PoliceRules.AccidentDanger(severity), akey, spot, at, null);
                    }
                    if (PedestrianIncidents.Duty(at, out Vector3 lying, out int ckey))
                    {
                        Consider(ref best, ref bestSqr, DutySource.Casualty, DutyKind.Casualty, PoliceRules.CasualtyDanger, ckey, lying, at, null);
                    }
                    return best;
                }
                catch (Exception e)
                {
                    Fault("PoliceLinks.Incident", e);
                    return default;
                }
            }

            private static void Consider(ref DutyCall best, ref float bestSqr, DutySource source, DutyKind kind, int danger, int key, Vector3 where, Vector3 at, string id)
            {
                float dx = where.x - at.x, dz = where.z - at.z;
                float sqr = dx * dx + dz * dz;
                if (sqr >= bestSqr)
                {
                    return;
                }
                bestSqr = sqr;
                best = new DutyCall { Present = true, Source = source, Kind = kind, Danger = danger, Key = key, Metres = Mathf.RoundToInt(Mathf.Sqrt(sqr)), Id = id };
            }

            private static bool Settle(DutyCall call, DutyMove move, DutyResult result)
            {
                try
                {
                    switch (call.Source)
                    {
                        case DutySource.Accident:
                            // Whatever the officer does at a crash, the services are sent at once (what a call to 911 does); the crash plays out as it does.
                            return TrafficAccidents.Instance != null && TrafficAccidents.Instance.ReportAccident();
                        case DutySource.Casualty:
                            if (move == DutyMove.FirstAid && result == DutyResult.Success)
                            {
                                PedestrianIncidents.DutyAid(PlayerHelper.GetPosition(), Time.time);
                            }
                            return TrafficAccidents.Instance != null && TrafficAccidents.Instance.HasActiveAccident && TrafficAccidents.Instance.ReportAccident();
                        default:
                            return CrimeDirector.DutySettle(move, result);
                    }
                }
                catch (Exception e)
                {
                    Fault("PoliceLinks.Settle", e);
                    return false;
                }
            }

            // ------------------------------------------------------------------------------------------------- an injury on duty

            // 0 hurt, 1 hurt and warned that the next could be fatal, 2 the life ends: the verdicts and the near miss are Permadeath's, as a mugging's are.
            private static int Hurt(int hours, int danger)
            {
                try
                {
                    Permadeath.Verdict fate = Mortality.Judge(DeathCause.Duty, PoliceRules.FatalMeasure(danger));
                    if (fate == Permadeath.Verdict.Death && Mortality.Die(DeathCause.Duty))
                    {
                        return 2;
                    }
                    LifeEvents life = LifeEvents.Instance;
                    float energy = SaveGameManager.Current != null && SaveGameManager.Current.Energy > 35f ? PoliceRules.InjuryEnergy : 0f;   // never enough to make a tired player faint
                    life?.InjureOnDuty(hours, energy, "reallife_police_note_hurt");
                    if (fate == Permadeath.Verdict.NearMiss)
                    {
                        Mortality.NearMiss(DeathCause.Duty);
                        return 1;
                    }
                }
                catch (Exception e)
                {
                    Fault("PoliceLinks.Hurt", e);
                }
                return 0;
            }

            // ------------------------------------------------------------------------------------------------- the front desk

            private static Law.Bridges.DutyDeskView DeskView()
            {
                CareersDirector director = CareersDirector.Instance;
                if (director == null || !director.DutyDesk(out bool on, out string why, out string hours))
                {
                    return default;
                }
                return new Law.Bridges.DutyDeskView
                {
                    Offered = true, Can = on || why == null,
                    Title = CareersText.T(on ? "reallife_police_desk_entry_end" : "reallife_police_desk_entry"),
                    Detail = on ? CareersText.T("reallife_police_desk_entry_end_detail")
                        : why != null ? CareersText.T(why) : CareersText.T("reallife_police_desk_entry_detail", "hours", hours)
                };
            }

            private static string DeskPress()
            {
                CareersDirector director = CareersDirector.Instance;
                string why = director != null ? director.DutyPress() : "reallife_study_test_off";
                return why == null ? null : CareersText.T(why);
            }
        }
    }
}
