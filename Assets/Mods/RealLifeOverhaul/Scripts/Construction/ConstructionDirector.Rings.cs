using UI.Notification;

namespace RealLifeOverhaul.Construction
{
    /// <summary>
    /// Rigged tenders (RingRules): a ring of three of the city's builders forms on a tender now and then, the leader to win
    /// and the others covering; the player's company may be asked to cover for a cut, and refuses, joins or reports it. At
    /// the award the ring's bids are its own (or struck out, reported); afterwards the city's inquiry may find it out: the
    /// builders fined in their standing, the City Ledger's story (the builders named through the paper's story path), and a
    /// bid rigging case if the player covered. Deterministic from the save's seed, the tender and the day; on a tender's
    /// opening, its award and the day's pass, never per frame. Not on a Going Public guest (a guest has no tenders).
    /// </summary>
    public sealed partial class ConstructionDirector
    {
        // A new tender: a ring may form on it, and ask the player's company to cover.
        private void RingFor(TenderFile t)
        {
            if (!Settings.ConstructionCity || GoingPublic.IsGuest || _state.Ring(t.Id) != null || !RingRules.Forms(_state.Seed, t.Id, t.Source))
            {
                return;
            }
            var g = new RingFile { Tender = t.Id };
            g.Members = RingRules.Ring(_state.Seed, t.Id, out g.Leader);
            g.Asked = _state.Firm.Founded > 0 && RingRules.Approach(_state.Seed, t.Id, Licensed, _state.Firm.Reputation);
            _state.Rings.Add(g);
            _state.Tidy();
            Logger?.Info("Construction: a ring of " + RingRules.Count(g.Members) + " builders (leader " + g.Leader + ") formed on tender " + t.Id
                         + (g.Asked ? "; the player's company is asked to cover." : "."));
            if (g.Asked)
            {
                Tell(NotificationType.Info, "reallife_bld_notice_ring_approach", TenderName(t), BuildCatalogue.RivalKeys[g.Leader], TabTenders, t.Id);
            }
        }

        /// <summary>The ring's approach on an open tender, still to be answered; null for none.</summary>
        public RingFile Approach(TenderFile t)
        {
            RingFile g = t != null ? _state.Ring(t.Id) : null;
            return g != null && g.Asked && g.Answer == RingFile.NoAnswer && t.State == TenderState.Open ? g : null;
        }

        /// <summary>The player covers for the ring: the high bid it asks for, and a cut if its leader wins. Returns a text key.</summary>
        public string RingJoin(TenderFile t)
        {
            RingFile g = Approach(t);
            if (g == null)
            {
                return "reallife_bld_answer_ring_gone";
            }
            if (!Licensed)
            {
                return "reallife_bld_answer_no_licence";
            }
            g.Answer = RingFile.Joined;
            t.BidPermille = RingRules.PlayerCoverPermille;
            t.Fast = false;
            Logger?.Info("Construction: the player covers for the ring on tender " + t.Id + " at " + t.BidPermille + " in 1000.");
            Changed();
            return "reallife_bld_answer_ring_joined";
        }

        /// <summary>The player turns the ring down: nothing is said, and the ring bids as it planned. Returns a text key.</summary>
        public string RingRefuse(TenderFile t)
        {
            RingFile g = Approach(t);
            if (g == null)
            {
                return "reallife_bld_answer_ring_gone";
            }
            g.Answer = RingFile.Refused;
            Changed();
            return "reallife_bld_answer_ring_refused";
        }

        /// <summary>The player reports the ring to the city: an inquiry opens, and the ring's bids are struck out at the award. Returns a text key.</summary>
        public string RingReport(TenderFile t)
        {
            RingFile g = Approach(t);
            if (g == null)
            {
                return "reallife_bld_answer_ring_gone";
            }
            g.Answer = RingFile.Reported;
            Paper("reallife_bld_paper_ring_inquiry", TenderName(t), RingRules.Count(g.Members));
            Logger?.Info("Construction: the player reported the ring on tender " + t.Id + " to the city.");
            Changed();
            return "reallife_bld_answer_ring_reported";
        }

        // The award's rival bids as the ring has them: a member's own price put up (the leader) or a cover, or struck out when
        // the ring was reported. The bids kept, counted.
        private int RingBids(TenderFile t, int days, Speciality need, int count)
        {
            RingFile g = _state.Ring(t.Id);
            if (g == null)
            {
                return count;
            }
            int kept = 0;
            for (int i = 0; i < count; i++)
            {
                TenderBid b = _bids[i];
                if (b.Bidder < 0 || !RingRules.Member(g.Members, b.Bidder))
                {
                    _bids[kept++] = b;
                    continue;
                }
                if (g.Answer == RingFile.Reported)
                {
                    continue;   // the inquiry has the ring's bids: they are not opened
                }
                bool special = BuildCatalogue.RivalSpeciality[b.Bidder] == need;
                _bids[kept++] = RingRules.Rigged(b, _state.Seed, t.Id, t.Value, days, b.Bidder == g.Leader, special);
            }
            return kept;
        }

        // The award made: the cut paid when the player covered and the leader won; a reported ring found out at once.
        private void RingAwarded(TenderFile t, TenderBid won, int day)
        {
            RingFile g = _state.Ring(t.Id);
            if (g == null)
            {
                return;
            }
            g.Awarded = day;
            g.LeaderWon = won.Bidder == g.Leader;
            if (g.Answer == RingFile.Reported)
            {
                RingFound(g, t, day);
                return;
            }
            if (g.Answer == RingFile.Joined && g.LeaderWon)
            {
                g.Cut = RingRules.Cut(won.Price);
                ConstructionGame.Give(g.Cut, "reallife_transaction_bld_ring_cut", ConstructionText.T(BuildCatalogue.RivalKeys[g.Leader]));
                Tell(NotificationType.Info, "reallife_bld_notice_ring_cut", TenderName(t), BuildCatalogue.RivalKeys[g.Leader], TabTenders, t.Id);
            }
        }

        // The day's look: the inquiry may find a ring out in the weeks after its award.
        private void RingsDay(int day)
        {
            for (int i = 0; i < _state.Rings.Count; i++)
            {
                RingFile g = _state.Rings[i];
                if (g.Found == 0 && g.Awarded > 0 && g.Answer != RingFile.Reported && RingRules.Found(_state.Seed, g.Tender, g.Awarded, day))
                {
                    RingFound(g, _state.Tender(g.Tender), day);
                }
            }
        }

        // Found out: the builders fined in their standing and named in the paper; the player's company caught if it covered,
        // thanked if it reported the ring.
        private void RingFound(RingFile g, TenderFile t, int day)
        {
            g.Found = day;
            for (int r = 0; r < BuildCatalogue.RivalCount; r++)
            {
                if (RingRules.Member(g.Members, r))
                {
                    _state.Rivals[r].Reputation = BuildCatalogue.Clamp(_state.Rivals[r].Reputation - RingRules.RepFined, 0, 100);
                    RivalNews(r, RivalRigged, 1);   // the stock market hears it as the paper prints it: each listed member's shares fall
                }
            }
            string name = t != null ? TenderName(t) : ConstructionText.T("reallife_bld_place_lot");
            Paper("reallife_bld_paper_ring_found", RingNames(g), RingRules.Count(g.Members));
            Logger?.Info("Construction check (inquiry): the ring on tender " + g.Tender + " was found out on day " + day + " (answer " + g.Answer + ", cut " + g.Cut + "): PASS.");
            switch (g.Answer)
            {
                case RingFile.Joined:
                    g.Case = BidRigging(g.Cut);
                    _state.Firm.Reputation = FirmRules.Moved(_state.Firm.Reputation, -RingRules.RepCaught);
                    Standing(RingRules.StandingCaught, true);
                    Tell(NotificationType.Error, "reallife_bld_notice_ring_caught", name, null, TabTenders, g.Tender);
                    break;
                case RingFile.Reported:
                    _state.Firm.Reputation = FirmRules.Moved(_state.Firm.Reputation, RingRules.RepReported);
                    Standing(RingRules.StandingReported, false);
                    Tell(NotificationType.Success, "reallife_bld_notice_ring_broken", name, null, TabTenders, g.Tender);
                    break;
                default:
                    if (g.Asked || (t != null && t.BidPermille > 0))
                    {
                        Tell(NotificationType.Info, "reallife_bld_notice_ring_found", name, null, TabTenders, g.Tender);
                    }
                    break;
            }
            Changed();
        }

        /// <summary>The ring's builders in words, the leader first.</summary>
        public string RingNames(RingFile g)
        {
            string first = ConstructionText.T(BuildCatalogue.RivalKeys[g.Leader]), second = null, third = null;
            for (int r = 0; r < BuildCatalogue.RivalCount; r++)
            {
                if (r == g.Leader || !RingRules.Member(g.Members, r))
                {
                    continue;
                }
                if (second == null)
                {
                    second = ConstructionText.T(BuildCatalogue.RivalKeys[r]);
                }
                else if (third == null)
                {
                    third = ConstructionText.T(BuildCatalogue.RivalKeys[r]);
                }
            }
            return ConstructionText.T("reallife_bld_ring_names", "first", first, "second", second ?? string.Empty, "third", third ?? string.Empty);
        }
    }
}
