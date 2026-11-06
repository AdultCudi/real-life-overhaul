using System;
using UI.Notification;

namespace RealLifeOverhaul.Tech
{
    /// <summary>
    /// Terms (VentureTerms): what each term sheet asks, the player pushing on one term in the pitch, the board's say over a big spend and a salary
    /// over the band, a slip in a board pack, and the player's skills doing the work (Law reads the terms, Selling and Relationships warm the firm, Leadership
    /// carries a vote, Accounting keeps the pack honest, Analysis and Statistics see an offer's true value). COST: nothing per frame; the provision is watched on
    /// a money move only when the move is as large as the smallest provision of a funded firm, never otherwise. No money moves here: a vote only changes what may be
    /// done, and everything done still goes through the firm account.
    /// </summary>
    public sealed partial class TechDirector
    {
        private long _provisionLeast;   // the smallest provision of a funded firm at today's prices; 0 when no firm has one

        private void LoadTerms()
        {
            TermsStore.Apply(Save.modData, _venture);
        }

        private void WriteTerms(System.Collections.Generic.Dictionary<string, string> data)
        {
            string line = TermsStore.Write(_venture);
            if (line != null)
            {
                data[TermsStore.Key] = line;
            }
            else
            {
                data.Remove(TermsStore.Key);
            }
        }

        // The smallest provision of the firms the investors watch, kept with the floor so that a money move costs one compare.
        private void RefreshProvision()
        {
            long least = 0L;
            for (int i = 0; i < _venture.Caps.Count; i++)
            {
                Cap c = _venture.Caps[i];
                long prov = StartupMoney.Has(c) ? VentureTerms.ProvisionOf(c) : 0L;
                if (prov > 0 && (least == 0 || prov < least))
                {
                    least = prov;
                }
            }
            _provisionLeast = Level(least);
        }

        /// <summary>The player's skill grows a little by doing it, less the nearer to 100 it is (Careers' own route).</summary>
        internal void Grow(int skill, int thousandths)
        {
            TechBridges.LearnNow(skill, thousandths * (100 - TechBridges.SkillNow(skill)) / 100);
        }

        // ---------------------------------------------------------------- the offer

        /// <summary>The terms a firm asks for a round of the player's firm, set by the firm, the stage, the market's mood and how the firm is doing.</summary>
        private Terms TermsFor(Cap c, FirmPlace firm, int vc, int day)
        {
            int weak = c.Misses * 250 + c.Pressure * 250 + (c.Acc.Taken > 0 ? 300 : 0);
            long profit = FirmProfit(firm);
            if (profit < 0 && VentureGame.Cash / -profit < 90)
            {
                weak += 300;
            }
            return VentureTerms.Stamp(vc, c.Stage + 1, VentureRules.Mood(HiringGame.Cycle), Math.Min(1000, weak), _venture.Seed, StartupMoney.Hash(c.Place), day);
        }

        /// <summary>The offer a firm has on the table for a firm of the player's; null for none.</summary>
        internal TermSheet SheetOf(string place, int vc)
        {
            Cap c = CapFor(place, false);
            for (int i = 0; c != null && i < c.Sheets.Count; i++)
            {
                if (c.Sheets[i].Vc == vc)
                {
                    return c.Sheets[i];
                }
            }
            return null;
        }

        /// <summary>An offer's terms in words, one to a line, the hard ones marked when the player's Law reads them.</summary>
        internal string TermsText(string place, TermSheet t)
        {
            Cap c = CapFor(place, false);
            return TermsView.Plain(this, t.T, c != null && TermsView.Reads ? CostOf(c, t) : null);
        }

        /// <summary>What the offered terms cost in a sale (Law reads it), worked by the waterfall on the cap table with the new investor in it.</summary>
        internal TermsCosts CostOf(Cap c, TermSheet t)
        {
            int stage = TalentCatalogue.Clamp(c.Stage + 1, 0, (int)RoundStage.Count - 1);
            return VentureTerms.Cost(c.Holders, t.T, t.Vc, t.Amount, t.Pre, c.Stage + 1, VentureCatalogue.BurnByStage[stage] * 30);
        }

        /// <summary>The lead's terms in force at a firm of the player's, or null when it has none or they are the plain ones.</summary>
        internal Holder LeadLine(Cap c)
        {
            Holder lead = null;
            for (int i = 0; i < c.Holders.Count; i++)
            {
                Holder h = c.Holders[i];
                if (h.Kind == (int)HolderKind.Vc && (lead == null || h.Invested > lead.Invested))
                {
                    lead = h;
                }
            }
            return lead;
        }

        /// <summary>What the lead's terms cost in a sale at the firm's last round, worked on the cap table without the lead's line.</summary>
        internal TermsCosts CostOfHeld(Cap c, Holder lead)
        {
            var others = new System.Collections.Generic.List<Holder>(c.Holders.Count);
            for (int i = 0; i < c.Holders.Count; i++)
            {
                if (c.Holders[i] != lead)
                {
                    others.Add(c.Holders[i]);
                }
            }
            int stage = TalentCatalogue.Clamp(lead.Round, 0, (int)RoundStage.Count - 1);
            long pre = Math.Max(1L, c.Post - lead.Invested);
            return VentureTerms.Cost(others, lead.T, lead.Who, lead.Invested, pre, lead.Round, VentureCatalogue.BurnByStage[stage] * 30);
        }

        /// <summary>What the firm is worth on the market now, cents at the start's prices: the figure a venture firm's offer is worked from.</summary>
        internal long MarketOf(string place)
        {
            FirmPlace firm = FirmOf(place);
            if (firm == null || !_firmsOn)
            {
                return 0L;
            }
            long arr = FirmsRules.Arr(_firmsFile, place);
            return Math.Max(VentureCatalogue.FloorValue[0], FirmsRules.Value(firm.File, arr, 500));
        }

        /// <summary>What the going price of a startup's round would be on its true revenue, before the money, cents at the start's prices.</summary>
        internal long GoingPrice(Startup s)
        {
            int mood = VentureRules.Mood(HiringGame.Cycle);
            return VentureRules.PreMoney(s.Mrr, VentureRules.Growth(s, mood, HypeNow), mood, s.Stage + 1);
        }

        /// <summary>The player's eye for a deck: its revenue looks stretched, by Accounting or by Analysis and Statistics, when the startup's deck is not yet checked.</summary>
        internal bool DeckStretched(Startup s)
        {
            if (s.Diligence || (s.Revealed & 1) != 0)
            {
                return false;
            }
            return VentureTerms.Stretched(TechBridges.SkillNow(TalentCatalogue.Accounting), TechBridges.SkillNow(TalentCatalogue.Analysis),
                TechBridges.SkillNow(TalentCatalogue.Statistics), s.ClaimBp);
        }

        // ---------------------------------------------------------------- pushing on a term

        /// <summary>
        /// The player pushes on one term of an offer in the pitch, once per offer: a roll seeded by the firm and the day from Law, Selling, Relationships,
        /// the meeting's warmth, the firm's diligence and the market. Won: the term gives way a step. Lost: the offer is shorter, and lost by a wide margin it is worth less.
        /// Returns 1 won, 2 lost, 3 lost and soured, 0 not possible.
        /// </summary>
        internal int PushTerm(string place, int vc, int term)
        {
            Cap c = CapFor(place, false);
            TermSheet t = null;
            for (int i = 0; c != null && i < c.Sheets.Count; i++)
            {
                t = c.Sheets[i].Vc == vc ? c.Sheets[i] : t;
            }
            if (t == null || t.Pushed || t.T == null || Save == null || term < 0 || term >= TermsCatalogue.Count || !VentureTerms.Pushable(t.T, term))
            {
                return 0;
            }
            int day = Save.Day, mood = VentureRules.Mood(HiringGame.Cycle);
            int law = TechBridges.SkillNow(TalentCatalogue.Law), selling = TechBridges.SkillNow(TalentCatalogue.Selling), relationships = TechBridges.SkillNow(TalentCatalogue.Relationships);
            int chance = VentureTerms.PushChance(law, selling, relationships, t.Score, VentureCatalogue.Firms[TalentCatalogue.Clamp(vc, 0, VentureCatalogue.FirmCount - 1)].Diligence, mood);
            int roll = TechRandom.Mille(_venture.Seed, StartupMoney.Hash(place), day, 96 + vc * 7 + term);
            t.Pushed = true;
            int result;
            if (roll < chance)
            {
                VentureTerms.Relax(t.T, term);
                if (t.T.Standard)
                {
                    t.T = null;
                }
                result = 1;
            }
            else if (roll >= chance + 300)
            {
                t.Pre = Math.Max(VentureCatalogue.FloorValue[0], t.Pre * (100 - TermsCatalogue.SourPercent) / 100);
                t.Lapse = Math.Min(t.Lapse, day + 2);
                result = 3;
            }
            else
            {
                t.Lapse = Math.Min(t.Lapse, day + TermsCatalogue.SoftenDays);
                result = 2;
            }
            Grow(TalentCatalogue.Law, TermsCatalogue.GrowPush);
            Grow(TalentCatalogue.Selling, TermsCatalogue.GrowPush);
            ChangedTech();
            return result;
        }

        /// <summary>A pitch done: Selling and Relationships grow by it.</summary>
        internal void PitchDone()
        {
            Grow(TalentCatalogue.Selling, TermsCatalogue.GrowPitch);
            Grow(TalentCatalogue.Relationships, TermsCatalogue.GrowPitch);
        }

        /// <summary>A founders' meeting done, the player the investor: Analysis and Statistics grow by weighing a startup.</summary>
        internal void WeighedStartup()
        {
            Grow(TalentCatalogue.Analysis, TermsCatalogue.GrowRead);
            Grow(TalentCatalogue.Statistics, TermsCatalogue.GrowRead);
        }

        // ---------------------------------------------------------------- the board's say

        /// <summary>
        /// A provision on a spend at the firm's address: the money has moved already (the game books a fit out itself), so a spend over the sum that no approval covers is
        /// a breach that adds to the board's suspicion. Looked at only for a move as large as the smallest provision; the transaction is read only then.
        /// </summary>
        private void WatchProvision(GameInstance save, long dropCents)
        {
            if (save.Day != _scanDay)
            {
                _scanDay = save.Day;
                _scans = 0;
            }
            if (++_scans > MoneyCatalogue.ScansPerDay)
            {
                return;
            }
            Transaction t = LastTransaction(save);
            if (t == null || t.address == null || StatementLine(t))
            {
                return;
            }
            Cap own = CapAt(t.address);
            if (own == null || VentureTerms.Spend(own, Base(dropCents), save.Day))
            {
                return;
            }
            VentureTerms.Breach(own, save.Day);
            FirmPlace firm = FirmOf(own.Place);
            TellTech(NotificationType.Warning, "reallife_vc_note_breach",
                Data("firm", firm != null ? firm.File.Name : own.Acc.Name, "amount", TechText.Cents(Level(Base(dropCents))), "limit", TechText.Cents(Level(VentureTerms.ProvisionOf(own))),
                    "vc", LeadName(own)), TabFunding, TechApp.CapRow + own.Place);
            ChangedTech();
        }

        /// <summary>The player asks the board to agree a big spend: a vote, Leadership helps. Agreed, it covers spends over the provision for a fortnight.</summary>
        internal bool AskSpend(string place)
        {
            FirmPlace firm = FirmOf(place);
            Cap c = CapFor(place, false);
            if (firm == null || c == null || !StartupMoney.Has(c) || Save == null)
            {
                return false;
            }
            long prov = VentureTerms.ProvisionOf(c);
            int day = Save.Day;
            if (prov <= 0 || day < c.Rt.RefusedUntil || VentureTerms.Approved(c, prov, day))
            {
                TellTech(NotificationType.Info, prov <= 0 ? "reallife_vc_note_spend_none" : "reallife_vc_note_spend_wait", Data("firm", firm.File.Name, "day", c.Rt.RefusedUntil.ToString()),
                    TabFunding, TechApp.CapRow + place);
                return false;
            }
            int chance = VentureTerms.VoteChance(c, TechBridges.SkillNow(TalentCatalogue.Leadership), false);
            bool yes = TechRandom.Mille(_venture.Seed, StartupMoney.Hash(place), day, 92) < chance;
            if (yes)
            {
                c.Rt.Approved = prov * TermsCatalogue.ApproveTimes;
                c.Rt.ApprovedUntil = day + TermsCatalogue.ApproveDays;
            }
            else
            {
                c.Rt.RefusedUntil = day + TermsCatalogue.RefuseDays;
            }
            Grow(TalentCatalogue.Leadership, TermsCatalogue.GrowBoard);
            TellTech(yes ? NotificationType.Success : NotificationType.Warning, yes ? "reallife_vc_note_spend_yes" : "reallife_vc_note_spend_no",
                Data("firm", firm.File.Name, "amount", TechText.Cents(Level(c.Rt.Approved)), "until", c.Rt.ApprovedUntil.ToString(), "days", TermsCatalogue.RefuseDays.ToString(), "vc", LeadName(c)),
                TabFunding, TechApp.CapRow + place);
            ChangedTech();
            return true;
        }

        /// <summary>The player asks the board for a salary over the band: a vote, once a round, Leadership helps. Agreed, the band is half as high again until the next round.</summary>
        internal bool AskRaise(string place)
        {
            FirmPlace firm = FirmOf(place);
            Cap c = CapFor(place, false);
            if (firm == null || c == null || !StartupMoney.Has(c) || Save == null)
            {
                return false;
            }
            if (c.Rt.RaiseStage == c.Stage)
            {
                TellTech(NotificationType.Info, "reallife_vc_note_raise_asked", Data("firm", firm.File.Name), TabFunding, TechApp.CapRow + place);
                return false;
            }
            int chance = VentureTerms.VoteChance(c, TechBridges.SkillNow(TalentCatalogue.Leadership), true);
            bool yes = TechRandom.Mille(_venture.Seed, StartupMoney.Hash(place), Save.Day, 93) < chance;
            c.Rt.RaiseStage = c.Stage;
            if (yes)
            {
                c.Acc.Band = Math.Max(c.Acc.Band, StartupMoney.Band(c.Stage) * TermsCatalogue.RaisePercent / 100);
            }
            Grow(TalentCatalogue.Leadership, TermsCatalogue.GrowBoard);
            TellTech(yes ? NotificationType.Success : NotificationType.Warning, yes ? "reallife_vc_note_band_yes" : "reallife_vc_note_band_no",
                Data("firm", firm.File.Name, "band", TechText.Cents(Level(c.Acc.Band)), "vc", LeadName(c)), TabFunding, TechApp.CapRow + place);
            ChangedTech();
            return true;
        }

        // A related party deal over the provision's sum needs the board, as a big spend does: allowed with an approval that covers it, else refused before anything is booked.
        private bool RelatedAllowed(Cap c, FirmPlace firm, long paid, int day)
        {
            long prov = VentureTerms.ProvisionOf(c);
            if (prov <= 0 || paid <= prov || VentureTerms.Spend(c, paid, day))
            {
                return true;
            }
            TellTech(NotificationType.Warning, "reallife_vc_note_related_provision", Data("firm", firm.File.Name, "limit", TechText.Cents(Level(prov)), "vc", LeadName(c)),
                TabFunding, TechApp.CapRow + c.Place);
            return false;
        }

        /// <summary>The honest slip a board pack may carry: more likely with less Accounting. It adds a little to the board's suspicion for a while; the books themselves stay true.</summary>
        private void PackSlip(Cap c, FirmPlace firm, int day)
        {
            int accounting = TechBridges.SkillNow(TalentCatalogue.Accounting);
            if (TechRandom.Mille(_venture.Seed, StartupMoney.Hash(c.Place), day, 95) < VentureTerms.SlipChance(accounting))
            {
                c.Rt.SlipDay = day;
                TellTech(NotificationType.Warning, "reallife_vc_note_pack_slip", Data("firm", firm.File.Name, "vc", LeadName(c)), TabFunding, TechApp.CapRow + c.Place);
            }
            Grow(TalentCatalogue.Accounting, TermsCatalogue.GrowPack);
        }
    }
}
