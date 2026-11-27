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
    }
}
