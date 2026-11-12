using System.Collections.Generic;
using Localizor;

namespace RealLifeOverhaul.Suppliers
{
    /// <summary>One piece of the City Ledger about a supplier: its locale key and what fills it.</summary>
    public struct SupplyStory
    {
        public string Key;
        public Dictionary<string, string> Data;
    }

    /// <summary>The words of suppliers and contracts that are built from the state: a supplier's name, a cause, a notice for what the engine did, a paper piece, a rival's line.</summary>
    public static class SupplierText
    {
        /// <summary>A supplier's name in the language in use; the city's wholesale stores for 0.</summary>
        public static string Firm(int id)
        {
            SupplierDef def = SupplierCatalogue.Firm(id);
            return (def != null ? def.NameKey : SupplierKeys.WholesalersKey).GetLocalization();
        }

        public static string Kind(int kind) => SupplierCatalogue.KindKeys[SupplierCatalogue.Clamp(kind, 0, SupplierCatalogue.Kinds - 1)].GetLocalization();

        public static string Cause(Cause why) => SupplierKeys.CauseKeys[SupplierCatalogue.Clamp((int)why, 0, SupplierKeys.CauseKeys.Length - 1)].GetLocalization();

        /// <summary>A text with its names filled in: pairs of a name and what goes in its place.</summary>
        public static string Fill(string key, params string[] pairs)
        {
            string text = key.GetLocalization();
            for (int i = 0; i + 1 < pairs.Length; i += 2)
            {
                text = text.Replace("{" + pairs[i] + "}", pairs[i + 1] ?? string.Empty);
            }
            return text;
        }

        /// <summary>A share in thousandths as per cent: 4.5%.</summary>
        public static string Percent(int thousandths)
        {
            return (thousandths / 10f).ToString(thousandths % 10 == 0 ? "0" : "0.#", System.Globalization.CultureInfo.InvariantCulture) + "%";
        }

        /// <summary>
        /// The notice for something the engine did: its key, what fills it and whether it is a warning. Null key for what is only written to the log (a supplier's
        /// trouble the player has no contract with).
        /// </summary>
        public static string Notice(in SupplyEvent e, out Dictionary<string, string> data, out bool warn)
        {
            string firm = Firm(e.Firm), place = e.Place ?? string.Empty;
            warn = false;
            data = null;
            switch (e.Kind)
            {
                case SupplyEventKind.Signed:
                    data = SupplierGame.Data("supplier", firm, "business", place, "amount", SupplierGame.Money(e.A), "day", e.B.ToString());
                    return e.C > 0 ? "reallife_supply_note_signed" : "reallife_supply_note_ordered";
                case SupplyEventKind.Delivered:
                    data = SupplierGame.Data("supplier", firm, "business", place, "amount", SupplierGame.Money(e.A), "units", e.B.ToString());
                    return "reallife_supply_note_delivered";
                case SupplyEventKind.NotTaken:
                    warn = true;
                    data = SupplierGame.Data("supplier", firm, "business", place, "units", e.A.ToString());
                    return "reallife_supply_note_not_taken";
                case SupplyEventKind.Tomorrow:
                    warn = true;
                    Cause cause = (Cause)(e.C & (SupplyEvent.KnewBit - 1));
                    bool theirs = SupplierRules.Theirs(cause), knew = theirs && (e.C & SupplyEvent.KnewBit) != 0;
                    data = SupplierGame.Data("supplier", firm, "business", place, "day", e.B.ToString(), "cause", Cause(cause), "days", e.A.ToString(), "share", Percent((int)e.A));
                    return e.A <= SupplierCatalogue.LateDaysMost ? (knew ? "reallife_supply_note_late_knew" : theirs ? "reallife_supply_note_late_theirs" : "reallife_supply_note_late")
                        : knew ? "reallife_supply_note_short_knew" : theirs ? "reallife_supply_note_short_theirs" : "reallife_supply_note_short";
                case SupplyEventKind.Credit:
                    data = SupplierGame.Data("supplier", firm, "business", place, "amount", SupplierGame.Money(e.A), "n", e.B.ToString(), "most", SupplierCatalogue.FailuresToLeave.ToString());
                    return "reallife_supply_note_credit";
                case SupplyEventKind.MayLeave:
                    warn = true;
                    data = SupplierGame.Data("supplier", firm, "business", place);
                    return "reallife_supply_note_may_leave";
                case SupplyEventKind.Ending:
                    data = SupplierGame.Data("supplier", firm, "business", place, "day", e.B.ToString());
                    return e.A == (int)RenewRule.Off ? "reallife_supply_note_ending" : "reallife_supply_note_ending_rule";
                case SupplyEventKind.Renewed:
                    data = SupplierGame.Data("supplier", firm, "business", place, "amount", SupplierGame.Money(e.A), "day", e.B.ToString());
                    return "reallife_supply_note_renewed";
                case SupplyEventKind.Switched:
                    data = SupplierGame.Data("supplier", firm, "business", place, "amount", SupplierGame.Money(e.A), "day", e.B.ToString(), "old", Firm(e.C));
                    return "reallife_supply_note_switched";
                case SupplyEventKind.Covered:
                    data = SupplierGame.Data("supplier", firm, "business", place, "amount", SupplierGame.Money(e.A), "day", e.B.ToString(), "old", Firm(e.C));
                    return "reallife_supply_note_covered";
                case SupplyEventKind.NoCover:
                    warn = true;
                    data = SupplierGame.Data("supplier", firm, "business", place);
                    return e.A == 1 ? "reallife_supply_note_no_renewal" : "reallife_supply_note_no_cover";
                case SupplyEventKind.OnAccount:
                    warn = true;
                    data = SupplierGame.Data("supplier", firm, "business", place, "amount", SupplierGame.Money(e.A), "day", e.B.ToString());
                    return "reallife_supply_note_on_account";
                case SupplyEventKind.Withheld:
                    warn = true;
                    data = SupplierGame.Data("supplier", firm, "business", place, "amount", SupplierGame.Money(e.A), "n", e.B.ToString(), "most", SupplierCatalogue.WithheldMost.ToString());
                    return "reallife_supply_note_withheld";
                case SupplyEventKind.EndedUnpaid:
                    warn = true;
                    data = SupplierGame.Data("supplier", firm, "business", place, "amount", SupplierGame.Money(e.A), "owed", SupplierGame.Money(e.B));
                    return "reallife_supply_note_ended_unpaid";
                case SupplyEventKind.EndedPlace:
                    warn = true;
                    data = SupplierGame.Data("supplier", firm, "business", place, "amount", SupplierGame.Money(e.A), "owed", SupplierGame.Money(e.B));
                    return e.B > 0 ? "reallife_supply_note_ended_place_owed" : "reallife_supply_note_ended_place";
                case SupplyEventKind.EndedDone:
                    data = SupplierGame.Data("supplier", firm, "business", place, "amount", SupplierGame.Money(e.A));
                    return "reallife_supply_note_ended_done";
                case SupplyEventKind.Broken:
                    data = SupplierGame.Data("supplier", firm, "business", place, "amount", SupplierGame.Money(e.A));
                    return "reallife_supply_note_broken";
                case SupplyEventKind.Left:
                    data = SupplierGame.Data("supplier", firm, "business", place);
                    return "reallife_supply_note_left";
                case SupplyEventKind.FirmTrouble:
                    warn = true;
                    data = SupplierGame.Data("supplier", firm, "trouble", SupplierKeys.TroubleKeys[SupplierCatalogue.Clamp((int)e.A, 0, 2)].GetLocalization(), "day", e.B.ToString());
                    return e.C > 0 ? "reallife_supply_note_firm_trouble" : null;
                case SupplyEventKind.FirmUnder:
                    warn = true;
                    data = SupplierGame.Data("supplier", firm, "day", e.A.ToString());
                    return e.C > 0 ? "reallife_supply_note_firm_under" : null;
                case SupplyEventKind.FirmBack:
                case SupplyEventKind.FirmGrew:
                    return null;
                case SupplyEventKind.OwedPaid:
                    data = SupplierGame.Data("amount", SupplierGame.Money(e.A), "owed", SupplierGame.Money(e.B));
                    return e.B > 0 ? "reallife_supply_note_owed_part" : "reallife_supply_note_owed_paid";
                case SupplyEventKind.CreditBack:
                    data = SupplierGame.Data("supplier", firm, "amount", SupplierGame.Money(e.A));
                    return "reallife_supply_note_credit_back";
                case SupplyEventKind.CreditLost:
                    warn = true;
                    data = SupplierGame.Data("supplier", firm, "amount", SupplierGame.Money(e.A));
                    return "reallife_supply_note_credit_lost";
                default:
                    return null;
            }
        }

        /// <summary>A supplier's news as a story of the paper.</summary>
        public static SupplyStory Story(SupplyNote n)
        {
            string firm = Firm(n.Firm);
            switch (n.Kind)
            {
                case NoteKind.Trouble:
                    return new SupplyStory
                    {
                        Key = "reallife_supply_paper_trouble",
                        Data = SupplierGame.Data("supplier", firm, "trouble", SupplierKeys.TroubleKeys[SupplierCatalogue.Clamp(n.A, 0, 2)].GetLocalization(), "day", n.B.ToString())
                    };
                case NoteKind.Under:
                    return new SupplyStory { Key = "reallife_supply_paper_under", Data = SupplierGame.Data("supplier", firm, "n", n.A.ToString()) };
                case NoteKind.Back:
                    return new SupplyStory { Key = "reallife_supply_paper_back", Data = SupplierGame.Data("supplier", firm) };
                case NoteKind.Grew:
                    return new SupplyStory { Key = "reallife_supply_paper_grew", Data = SupplierGame.Data("supplier", firm) };
                default:
                    return new SupplyStory { Key = "reallife_supply_paper_short", Data = SupplierGame.Data("n", n.A.ToString()) };
            }
        }

        /// <summary>A line of the page for a supplier's news.</summary>
        public static string NewsLine(SupplyNote n)
        {
            SupplyStory story = Story(n);
            string text = story.Key.GetLocalization();
            foreach (KeyValuePair<string, string> pair in story.Data)
            {
                text = text.Replace("{" + pair.Key + "}", pair.Value);
            }
            return text;
        }

        /// <summary>What supply has done to a rival tycoon's outlets this month, for the rich list and their panel; empty for nothing to say.</summary>
        public static string RivalLine(string name, RivalSupply r)
        {
            if (string.IsNullOrEmpty(name) || r == null || r.MonthOf < 0 || (r.Month == 0 && r.Failures == 0))
            {
                return string.Empty;
            }
            return Fill(r.Month >= 0 ? "reallife_supply_rival_line_more" : "reallife_supply_rival_line_less", "rival", name, "supplier", Firm(r.Firm),
                "amount", SupplierGame.Money(System.Math.Abs(r.Month) * 100), "n", r.Failures.ToString());
        }
    }
}
