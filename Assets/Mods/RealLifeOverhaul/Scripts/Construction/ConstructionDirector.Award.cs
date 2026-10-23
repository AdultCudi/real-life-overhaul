using System;
using System.Collections.Generic;
using UI.Notification;

namespace RealLifeOverhaul.Construction
{
    /// <summary>
    /// The City Ledger's opinions on new buildings and its building of the year (AwardRules): each building finished in the
    /// city (the player's company's, the developers', a public building a rival put up) is given the critic's score, the
    /// paper writes of the player's and of the city's best and worst; in the year's last days the four best are shortlisted
    /// and the city votes on Flare (the player's following counts for theirs); the new year's first day names the winner,
    /// which lifts the company's standing and the player's. Seeded by the save's seed and the year; on a handover and the
    /// day's pass, never per frame.
    /// </summary>
    public sealed partial class ConstructionDirector
    {
        private readonly List<int> _awardOrder = new List<int>(AwardRules.EntriesMost);

        /// <summary>The City Ledger's critic's piece for one to five stars.</summary>
        private static readonly string[] CriticKeys =
        {
            "reallife_bld_paper_critic_1", "reallife_bld_paper_critic_2", "reallife_bld_paper_critic_3", "reallife_bld_paper_critic_4", "reallife_bld_paper_critic_5"
        };

        // A building finished: kept as one of the year's, and the critic's opinion in the paper.
        private void NoteFinished(int kind, ProjectFile p, string lot, string street, int number, BuildUse use, int builder, int quality, int lateDays, int day)
        {
            if (!Settings.ConstructionCity)
            {
                return;
            }
            AwardFile a = _state.Award;
            int year = AwardRules.Year(day, ConstructionGame.DaysPerYear);
            if (a.Year != year)
            {
                if (a.Year >= 0 && a.Year < year)
                {
                    AwardDue(day);   // a year gone by unawarded is awarded first
                }
                a.Entries.Clear();
                a.Voting = false;
                a.Year = year;
            }
            int key = BuildRandom.Hash(lot ?? street) ^ number * 131 ^ (p != null ? p.Id : 0);
            var e = new AwardEntry
            {
                Kind = kind, Project = p != null ? p.Id : 0, Lot = lot ?? string.Empty, Street = street ?? string.Empty, Number = number, Use = use, Builder = builder, Day = day,
                Score = AwardRules.Score(quality, lateDays, _state.Seed, key)
            };
            int room = AwardRules.Room(a.Entries, e.Score, builder == -1);
            if (room >= 0)
            {
                if (room < a.Entries.Count)
                {
                    a.Entries[room] = e;
                }
                else
                {
                    a.Entries.Add(e);
                }
            }
            int stars = AwardRules.Stars(e.Score);
            if (builder == -1 || e.Score >= AwardRules.CriticFrom || e.Score < AwardRules.CriticBelow)
            {
                Paper(CriticKeys[stars - 1], AwardName(e), stars);
            }
            if (Verbose)
            {
                Logger?.Info("Construction check (critic): " + AwardName(e) + " by builder " + builder + ", score " + e.Score + " (" + stars + " stars), " + (room >= 0 ? "kept for" : "not among")
                             + " the year's best: PASS.");
            }
        }

        // The year's last days and the next year's first: the shortlist, the city's votes, the award.
        private void AwardDay(int day)
        {
            AwardFile a = _state.Award;
            int dpy = ConstructionGame.DaysPerYear;
            int year = AwardRules.Year(day, dpy);
            if (a.Year < 0)
            {
                a.Year = year;
                return;
            }
            if (a.Year < year)
            {
                AwardDue(day);
                a.Year = year;
                return;
            }
            if (!a.Voting && day >= AwardRules.ShortlistDay(a.Year, dpy) && a.Entries.Count > 0)
            {
                Shortlist(day);
            }
            if (a.Voting)
            {
                Votes(day);
            }
        }

        // The four best of the year shortlisted, the paper's pieces, and the player told if theirs is among them.
        private void Shortlist(int day)
        {
            AwardFile a = _state.Award;
            AwardRules.Rank(a.Entries, _awardOrder);
            bool mine = false;
            for (int k = 0; k < _awardOrder.Count && k < AwardRules.Shortlist; k++)
            {
                AwardEntry e = a.Entries[_awardOrder[k]];
                e.Short = true;
                mine |= e.Builder == -1;
                Paper("reallife_bld_paper_award_shortlist", AwardName(e), e.Score);
            }
            a.Voting = true;
            if (mine)
            {
                _state.Firm.Reputation = FirmRules.Moved(_state.Firm.Reputation, AwardRules.RepShortlisted);
                Tell(NotificationType.Info, "reallife_bld_notice_award_shortlist", BuilderName(-1), null, TabCity, 0);
            }
            Logger?.Info("Construction: the City Ledger's building of the year " + a.Year + ": " + Math.Min(AwardRules.Shortlist, _awardOrder.Count) + " shortlisted, the player's "
                         + (mine ? "among them" : "not among them") + "; the city votes on Flare until the year's end.");
            Votes(day);
        }

        private void Votes(int day)
        {
            AwardFile a = _state.Award;
            int followers = Followers();
            for (int i = 0; i < a.Entries.Count; i++)
            {
                AwardEntry e = a.Entries[i];
                if (e.Short)
                {
                    e.Votes += AwardRules.DayVotes(_state.Seed, a.Year, i, day, e.Score, followers, e.Builder == -1);
                }
            }
        }

        // The award for the year the entries are of: shortlisted now if it never was, the most votes wins.
        private void AwardDue(int day)
        {
            AwardFile a = _state.Award;
            if (a.Entries.Count == 0)
            {
                a.Voting = false;
                return;
            }
            if (!a.Voting)
            {
                Shortlist(day);
            }
            int won = AwardRules.Winner(a.Entries);
            if (won >= 0)
            {
                AwardEntry e = a.Entries[won];
                int votes = 0;
                for (int i = 0; i < a.Entries.Count; i++)
                {
                    votes += a.Entries[i].Short ? a.Entries[i].Votes : 0;
                }
                a.Last.CopyFrom(e);
                a.LastYear = a.Year;
                a.LastVotes = votes;
                Paper("reallife_bld_paper_award_won", AwardName(e) + " (" + BuilderName(e.Builder) + ")", e.Votes);
                if (e.Builder == -1)
                {
                    _state.Firm.Reputation = FirmRules.Moved(_state.Firm.Reputation, AwardRules.RepWon);
                    Standing(AwardRules.StandingWon, false);
                    Tell(NotificationType.Success, "reallife_bld_notice_award_won", AwardName(e), null, TabCity, 0, e.Votes);
                }
                else
                {
                    for (int i = 0; i < a.Entries.Count; i++)
                    {
                        if (a.Entries[i].Short && a.Entries[i].Builder == -1)
                        {
                            Tell(NotificationType.Info, "reallife_bld_notice_award_lost", AwardName(e), null, TabCity, 0);
                            break;
                        }
                    }
                    if (e.Builder >= 0 && e.Builder < BuildCatalogue.RivalCount)
                    {
                        _state.Rivals[e.Builder].Reputation = BuildCatalogue.Clamp(_state.Rivals[e.Builder].Reputation + AwardRules.RepWon / 2, 0, 100);
                        RivalNews(e.Builder, RivalAward, 1);   // the stock market hears of the building of the year: its builder's shares rise
                    }
                }
                Logger?.Info("Construction check (award): the building of the year " + a.Year + " is " + AwardName(e) + " by builder " + e.Builder + ", " + e.Votes + " of " + votes
                             + " votes: PASS.");
            }
            a.Entries.Clear();
            a.Voting = false;
        }

        /// <summary>A building of the year's list in words: what it is and where.</summary>
        public string AwardName(AwardEntry e)
        {
            string where = !string.IsNullOrEmpty(e.Lot) && Lot(e.Lot, out LotView lot) ? ConstructionGame.AddressText(lot.Street, lot.Number)
                : !string.IsNullOrEmpty(e.Street) ? ConstructionGame.AddressText(e.Street, e.Number) : ConstructionText.T("reallife_bld_place_lot");
            if (e.Kind == AwardEntry.OfCity)
            {
                return ConstructionText.T("reallife_bld_name_public", "address", where);
            }
            if (e.Kind == AwardEntry.OfPlayer && string.IsNullOrEmpty(e.Lot))
            {
                return ConstructionText.T("reallife_bld_name_rebuild", "address", where);
            }
            return ConstructionText.T("reallife_bld_name_plot", "use", ConstructionText.T(BuildCatalogue.UseKeys[(int)e.Use]), "address", where);
        }

        /// <summary>A builder in words: the player's company by its office's name, or a rival's.</summary>
        public string BuilderName(int builder)
        {
            if (builder >= 0 && builder < BuildCatalogue.RivalCount)
            {
                return ConstructionText.T(BuildCatalogue.RivalKeys[builder]);
            }
            return Office(out PlaceView office) && _state.Firm.Founded > 0 ? office.Name : ConstructionText.T("reallife_bld_rival_you");
        }
    }
}
