using System;
using System.Collections.Generic;
using UI.Notification;

namespace RealLifeOverhaul.Tech
{
    /// <summary>
    /// The new kinds of client in the firms' day (ClientRules): who each day's offers come from, what a delivered contract is still owed, the posture a contract asks of the firm,
    /// the notices, and the offers of a software or design business withdrawn. The rules are game-free; this part only gathers the facts and tells the player.
    /// </summary>
    public sealed partial class TechDirector
    {
        private readonly List<string> _clientTypes = new List<string>(16);
        private Func<string, string> _typeAt;

        // The business type of the registration at a place, or null for a place the game does not hold.
        private string TypeAt(string place)
        {
            var reg = TalentGame.Find(place);
            return reg != null ? reg.businessTypeName : null;
        }

        // The day's offers have been drawn (from index `from` on): the option gives the new ones their client, and an offer whose client is a business in the firms' own line of work
        // (software, IT, web or graphic design) is withdrawn whatever the option says, with a short notice when any was open.
        private void ClientsOffered(int day, int from)
        {
            if (Settings.TechDealKinds)
            {
                int made = ClientRules.Mix(_firmsFile, day, from, _clients, _clientTypes);
                if (Verbose && made > 0)
                {
                    Logger?.Info("Tech check (clients, day " + day + "): " + made + " offer(s) given a department, an individual or a company as client: PASS");
                }
            }
            _typeAt = _typeAt ?? TypeAt;
            int gone = ClientRules.Withdraw(_firmsFile, _typeAt, day);
            if (gone > 0)
            {
                TellTech(NotificationType.Info, "reallife_deal_note_withdrawn", Data("count", gone.ToString()), TabContracts, null);
                ChangedTech();
            }
        }

        // What delivered contracts of this firm are owed, paid on their day: a department's weeks after, a client that pays late.
        private void ClientsPaid(FirmPlace firm, int day, int level)
        {
            for (int i = 0; i < _firmsFile.Contracts.Count; i++)
            {
                Contract w = _firmsFile.Contracts[i];
                if (w.Owed <= 0 || w.Place != firm.File.Place)
                {
                    continue;
                }
                long due = ClientRules.Due(w, day);
                if (due > 0)
                {
                    Paid(firm, w, due * level / 1000);
                    _work.Add(new WorkNote { Contract = w.Id, Kind = FirmsRules.NotePaid, Amount = due });
                }
            }
        }

        // True, with a notice, when the firm falls short of the security posture or the reliability the contract asks for: it cannot take or bid for the work yet.
        private bool NeedRefused(Contract w, FirmPlace firm)
        {
            if (w == null || firm == null || ClientRules.Short(w, firm.Crew) == 0)
            {
                return false;
            }
            TellTech(NotificationType.Warning, "reallife_deal_note_need", Data("client", w.Client, "firm", firm.File.Name, "security", TechText.Mille(w.NeedSecurity),
                "reliability", TechText.Mille(w.NeedReliability), "have_security", TechText.Mille(firm.Crew.Security), "have_reliability", TechText.Mille(firm.Crew.Reliability)),
                TabContracts, TechApp.ContractRow + w.Id);
            return true;
        }

        // The notes of the new kinds of client, told as the old ones are (Worked's switch hands them here).
        private void ClientNote(WorkNote note, Contract w)
        {
            string row = TechApp.ContractRow + w.Id;
            switch (note.Kind)
            {
                case FirmsRules.NoteChange:
                    TellTech(NotificationType.Warning, "reallife_deal_note_change", Data("client", w.Client, "extra", note.Amount.ToString(), "due", w.Due.ToString()), TabContracts, row);
                    break;
                case FirmsRules.NoteOwed:
                    TellTech(NotificationType.Info, "reallife_deal_note_owed", Data("client", w.Client, "amount", TechText.Cents(note.Amount * Prices.Level / 1000), "day", w.PayDay.ToString()),
                        TabContracts, row);
                    break;
                case FirmsRules.NotePaid:
                    TellTech(NotificationType.Success, "reallife_deal_note_paid", Data("client", w.Client, "amount", TechText.Cents(note.Amount * Prices.Level / 1000)), TabContracts, row);
                    break;
                case FirmsRules.NoteScrutiny:
                    if (Save != null)
                    {
                        _firmsFile.GovHold = Math.Max(_firmsFile.GovHold, Save.Day + ClientCatalogue.ScrutinyDays);
                        TellTech(NotificationType.Warning, "reallife_deal_note_audit", Data("client", w.Client, "days", ClientCatalogue.ScrutinyDays.ToString()), TabContracts, row);
                    }
                    break;
            }
        }

        /// <summary>An offer of each new kind of client, and a public tender, to the first firm that can take the work (a test of Options > Tests).</summary>
        public void TestDealKinds()
        {
            if (!_firmsOn || Save == null)
            {
                Notifications.ShowError("reallife_tech_test_off");
                return;
            }
            ReadFirms(Save.Day);
            int made = 0;
            for (int i = 0; i < _firms.Count && made == 0; i++)
            {
                Firm firm = _firms[i].File;
                int kinds = FirmsCatalogue.TakesWork[firm.Kind];
                for (int c = (int)ClientKind.Government; c <= (int)ClientKind.Company; c++)
                {
                    for (int k = 0; k < (int)WorkKind.Count; k++)
                    {
                        if ((kinds & (1 << k)) == 0 || k == (int)WorkKind.Incident)
                        {
                            continue;
                        }
                        Contract w = ClientRules.Make(_firmsFile, firm.Place, (ClientKind)c, (WorkKind)k, false, Save.Day, (Save.Day * 37 + c * 211 + k * 71) % 1000);
                        if (w != null)
                        {
                            _firmsFile.Contracts.Add(w);
                            Worked(new WorkNote { Contract = w.Id, Kind = FirmsRules.NoteOffered });
                            made++;
                            break;
                        }
                    }
                }
                WorkKind tender = (kinds & (1 << (int)WorkKind.Integration)) != 0 ? WorkKind.Integration : WorkKind.Managed;
                Contract t = (kinds & (1 << (int)tender)) != 0 ? ClientRules.Make(_firmsFile, firm.Place, ClientKind.Government, tender, true, Save.Day, (Save.Day * 53) % 1000) : null;
                if (t != null)
                {
                    _firmsFile.Contracts.Add(t);
                    Worked(new WorkNote { Contract = t.Id, Kind = FirmsRules.NoteTender });
                    made++;
                }
            }
            if (made == 0)
            {
                Notifications.ShowError("reallife_tech_test_no_taker");
                return;
            }
            ChangedTech();
        }

        // ------------------------------------------------------------------------------- the in-game self-test: a read-only probe and its set-ups

        /// <summary>
        /// A read-only account of the firms contracts for the in-game self-test, in plain words: "offers" (the open ones by kind of client and how many are from a barred business),
        /// "colo" (the data centres clients and how many are barred), "hold" (the audit hold), "contract N" or "first KIND" (one contract terms). Changes nothing; costs nothing unless called.
        /// </summary>
        public string Probe(string what)
        {
            if (!_firmsOn || _firmsFile == null)
            {
                return "off";
            }
            what = what ?? string.Empty;
            if (what == "offers")
            {
                int[] kinds = new int[(int)ClientKind.Count];
                int barred = 0, open = 0;
                foreach (Contract w in _firmsFile.Contracts)
                {
                    if (w.Stage > (int)WorkStage.Bid)
                    {
                        continue;
                    }
                    open++;
                    kinds[Math.Max(0, Math.Min(kinds.Length - 1, w.Deal))]++;
                    string type = string.IsNullOrEmpty(w.OwnPlace) ? null : TypeAt(w.OwnPlace);
                    barred += type != null && ClientFilter.TypeExcluded(type) ? 1 : 0;
                }
                return "open " + open + " classic " + kinds[0] + " government " + kinds[1] + " individual " + kinds[2] + " company " + kinds[3] + " barred " + barred;
            }
            if (what == "colo")
            {
                int n = 0, bad = 0;
                for (int i = 0; _infra != null && i < _infra.Colo.Count; i++)
                {
                    n++;
                    bad += ColoRules.ClientOpen(_infra.Colo[i].Client) ? 0 : 1;
                }
                return "colo " + n + " barred " + bad;
            }
            if (what == "hold")
            {
                return "hold " + _firmsFile.GovHold;
            }
            if (what == "rep")
            {
                return "rep " + (_firmsFile.Firms.Count > 0 ? _firmsFile.Firms[0].Reputation : -1);
            }
            Contract found = null;
            if (what.StartsWith("contract ", StringComparison.Ordinal) && int.TryParse(what.Substring(9), out int id))
            {
                found = _firmsFile.ContractOf(id);
            }
            else if (what.StartsWith("first ", StringComparison.Ordinal))
            {
                string kind = what.Substring(6);
                for (int i = _firmsFile.Contracts.Count - 1; i >= 0 && found == null; i--)
                {
                    Contract w = _firmsFile.Contracts[i];
                    found = ((ClientKind)w.Deal).ToString().ToLowerInvariant() == kind && w.Stage <= (int)WorkStage.Active ? w : null;
                }
            }
            if (found == null)
            {
                return "none";
            }
            return "id " + found.Id + " kind " + ((ClientKind)found.Deal).ToString().ToLowerInvariant() + " stage " + ((WorkStage)found.Stage).ToString().ToLowerInvariant() + " points " + found.Points
                   + " done " + found.Done + " value " + found.Value * Prices.Level / 100000 + " paid " + found.Paid * Prices.Level / 100000 + " owed " + found.Owed * Prices.Level / 100000
                   + " lag " + found.Lag + " payday " + found.PayDay + " due " + found.Due + " extra " + found.Extra + " need " + found.NeedSecurity + " " + found.NeedReliability
                   + " brief " + found.Brief + " client " + found.Client.Replace(' ', '_') + " council " + (found.Council ? "yes" : "no");
        }

        /// <summary>
        /// The self-test set-up: an offer of a kind of client ("government", "individual", "company"; "tender" for a department tender) to the first firm that takes the work, with the case
        /// asked for ("late": pays late, "creep": asks for one more change). Returns its id, or 0.
        /// </summary>
        public int SelfOffer(string kind, string flavour)
        {
            if (!_firmsOn || Save == null)
            {
                return 0;
            }
            ReadFirms(Save.Day);
            bool tender = kind == "tender";
            ClientKind ck = kind == "government" || tender ? ClientKind.Government : kind == "individual" ? ClientKind.Individual : ClientKind.Company;
            for (int i = 0; i < _firms.Count; i++)
            {
                int kinds = FirmsCatalogue.TakesWork[_firms[i].File.Kind];
                for (int k = 0; k < (int)WorkKind.Count; k++)
                {
                    if ((kinds & (1 << k)) == 0 || k == (int)WorkKind.Incident || (tender && k != (int)WorkKind.Integration && k != (int)WorkKind.Managed))
                    {
                        continue;
                    }
                    Contract w = ClientRules.Make(_firmsFile, _firms[i].File.Place, ck, (WorkKind)k, tender, Save.Day, 300);
                    if (w == null || (flavour == "creep" && w.Monthly))
                    {
                        continue;
                    }
                    w.Lag = flavour == "late" ? 10 : w.Lag;
                    w.CreepMille = flavour == "creep" ? 1000 : w.CreepMille;
                    _firmsFile.Contracts.Add(w);
                    ChangedTech();
                    return w.Id;
                }
            }
            return 0;
        }

        /// <summary>The self-test set-up: an open offer whose client is the player own business of a barred type (a web development agency or graphic designer), as an old save held it. Returns its id, or 0.</summary>
        public int SelfBarredOffer()
        {
            if (!_firmsOn || Save == null)
            {
                return 0;
            }
            ReadFirms(Save.Day);
            if (_firms.Count == 0)
            {
                return 0;
            }
            List<BuildingRegistration> all = Save.BuildingRegistrations;
            for (int i = 0; all != null && i < all.Count; i++)
            {
                BuildingRegistration reg = all[i];
                if (reg != null && GoingPublic.Mine(reg) && reg.HasEstablishedBusiness && ClientFilter.TypeExcluded(reg.businessTypeName) && FirmsGame.KindOf(reg.businessTypeName) >= 0)
                {
                    var w = new Contract { Id = _firmsFile.Next(), Kind = (int)WorkKind.Website, Stage = (int)WorkStage.Offered, End = Save.Day + FirmsCatalogue.OfferDays, Place = _firms[0].File.Place,
                        Client = TalentGame.NameOf(reg), OwnPlace = TalentGame.Place(reg), Points = 60 };
                    w.Value = FirmsRules.ValueOf(WorkKind.Website, w.Points, false);
                    _firmsFile.Contracts.Add(w);
                    ChangedTech();
                    return w.Id;
                }
            }
            return 0;
        }

        /// <summary>The self-test way into the Take button own handler.</summary>
        public bool SelfTake(int id) => TakeContract(id);

        /// <summary>The self-test way into the Bid button own handler, on the first firm that takes the tender work.</summary>
        public bool SelfBid(int id, int share)
        {
            Contract t = _firmsOn ? _firmsFile.ContractOf(id) : null;
            for (int i = 0; t != null && i < _firms.Count; i++)
            {
                if (FirmsRules.Takes(_firms[i].File.K, t.K))
                {
                    return BidTender(id, _firms[i].File.Place, share);
                }
            }
            return false;
        }
    }
}
