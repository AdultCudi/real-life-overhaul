using System.Collections.Generic;
using System.Globalization;
using BigAmbitions.Items;
using BigAmbitions.Tags;
using Entities;
using Helpers;
using RealLifeOverhaul.Economy;
using UI.Dialog;
using UnityEngine;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// A wholesale store's and an importer's contract forms as lists to choose from, in place of the game's one drop-down.
    /// The wholesale form lists the player's businesses that take deliveries, each with its kind and district, the ones the
    /// game would refuse greyed with why (a contract there with this store already, no storage shelf in it), and under them
    /// the store's fee, the first delivery day and what each delivery costs. The importer's form lists the purchasing agents
    /// the game offers (on a shift, in no partnership yet), each with their workplace, the discount their skill gives and
    /// the skill, then what the partnership needs (a rented warehouse, an agent on a shift) and what it brings. A pick is
    /// chosen through the game's own drop-down (SelectOption), so its listeners run and the game's confirm does the rest.
    /// The goods of a first order stay in BizMan, where the game sets them. Where the game's list and the drop-down differ
    /// (another mod changed one), the form keeps the drop-down. A little work when the form is read; nothing a frame.
    /// </summary>
    internal sealed partial class DialogForm
    {
        private static readonly List<EmployeeInstance> AgentsFound = new List<EmployeeInstance>(16);
        private static readonly EmployeeInstancesQueryInfo AgentQuery = new EmployeeInstancesQueryInfo
        {
            withSkills = new[] { "ba:skill_purchasingagent" },
            excludeBeingReplaced = true
        };

        private string _pickHint;   // the game's words for a list nothing is chosen from yet (Select business)

        private void Contracts(Transform form, DialogController talk, int heading)
        {
            _pickHint = null;
            if (form == null || SaveGameManager.Current == null)
            {
                return;
            }
            if (form.TryGetComponent(out DeliveryContractSettings wholesale))
            {
                Wholesale(wholesale, talk, heading);
            }
            else if (form.TryGetComponent(out ImportPartnershipSettings import))
            {
                Importer(import, heading);
            }
        }

        // The wholesale store's form: the businesses, then its fee, the first delivery day and what each delivery costs.
        private void Wholesale(DeliveryContractSettings settings, DialogController talk, int heading)
        {
            var drop = GameContract.DialogWholesaleDrop.Field?.GetValue(settings) as global::UI.Elements.Dropdown;
            var regs = GameContract.DialogWholesaleList.Field?.GetValue(settings) as List<BuildingRegistration>;
            int row = DropRow(drop, heading);
            List<string> options = drop != null && GameContract.DialogDropOptions.Field != null ? GameContract.DialogDropOptions.Field.GetValue(drop) as List<string> : null;
            if (row < 0 || regs == null || options == null || options.Count != regs.Count)
            {
                return;
            }
            Address store = talk != null && talk.contact != null ? talk.contact.Address : null;
            List<DeliveryContract> contracts = SaveGameManager.Current.DeliveryContracts;
            _pickHint = _rows[row].Hint;
            int chosen = Index(drop);
            _rows.RemoveAt(row);
            _widgets.RemoveAt(row);
            // Under the form's own heading: a second label over the list would say the same thing.
            var picks = new List<UiFormRow>(regs.Count);
            var widgets = new List<Widget>(regs.Count);
            for (int k = 0; k < regs.Count; k++)
            {
                BuildingRegistration reg = regs[k];
                string reason = null;
                if (reg != null && store != null && Contracted(contracts, reg.Address, store))
                {
                    reason = UiText.T("reallife_ui_dialog_has_contract");
                }
                else if (reg != null && !Storage(reg))
                {
                    reason = UiText.T("reallife_ui_dialog_needs_storage");
                }
                picks.Add(new UiFormRow
                {
                    Kind = UiFormKind.Pick, Label = ContactsRules.Plain(options[k] ?? string.Empty), Hint = Where(reg), Icon = UiIcon.Store,
                    On = k == chosen, Disabled = reason != null, Reason = reason
                });
                widgets.Add(new Widget { Drop = drop, Option = k });
            }
            _rows.InsertRange(row, picks);
            _widgets.InsertRange(row, widgets);
            Add(new UiFormRow { Kind = UiFormKind.Figure, Label = UiText.T("reallife_ui_dialog_first_delivery"),
                Value = UiText.Fill("reallife_ui_dialog_contract_day", "day", Day(DeliveryHelper.GetNextDeliveryDay())) }, default);
            Add(new UiFormRow { Kind = UiFormKind.Note, Label = UiText.T("reallife_ui_dialog_each_delivery_note") }, default);
        }

        // The importer's form: the agents, then what a partnership needs and what it brings.
        private void Importer(ImportPartnershipSettings settings, int heading)
        {
            var drop = settings.purchasingAgentDropdown;
            int row = DropRow(drop, heading);
            List<string> options = drop != null && GameContract.DialogDropOptions.Field != null ? GameContract.DialogDropOptions.Field.GetValue(drop) as List<string> : null;
            if (row < 0 || options == null)
            {
                return;
            }
            // The game's own list, made as its form makes it: agents on a shift who buy for no importer yet.
            AgentsFound.Clear();
            EmployeeHelper.GetEmployeeInstances(AgentQuery, AgentsFound);
            int all = AgentsFound.Count;
            List<ImportPartnership> partnerships = SaveGameManager.Current.importPartnerships;
            for (int i = AgentsFound.Count - 1; i >= 0; i--)
            {
                EmployeeInstance e = AgentsFound[i];
                if (e == null || !e.IsAssignedToAnyWorkShift() || Partnered(partnerships, e.id))
                {
                    AgentsFound.RemoveAt(i);
                }
            }
            if (AgentsFound.Count != options.Count)
            {
                return;
            }
            for (int k = 0; k < options.Count; k++)
            {
                if (AgentsFound[k].characterData == null || AgentsFound[k].characterData.name != options[k])
                {
                    return;   // not the drop-down's list: it keeps its own
                }
            }
            _pickHint = _rows[row].Hint;
            int chosen = Index(drop);
            _rows.RemoveAt(row);
            _widgets.RemoveAt(row);
            // Under the form's own heading, which says at its right whom the list holds; a heading of its own only where the
            // form has none above it (a second label over the list would say the same thing).
            var picks = new List<UiFormRow>(options.Count + 1);
            var widgets = new List<Widget>(options.Count + 1);
            string whom = UiText.T("reallife_ui_dialog_agents_note");
            if (row > 0 && _rows[row - 1].Kind == UiFormKind.Heading && string.IsNullOrEmpty(_rows[row - 1].Value))
            {
                UiFormRow above = _rows[row - 1];
                above.Value = whom;
                _rows[row - 1] = above;
            }
            else
            {
                picks.Add(new UiFormRow { Kind = UiFormKind.Heading, Label = UiText.T("reallife_ui_dialog_agents"), Value = whom });
                widgets.Add(default);
            }
            float best = 0f;
            for (int k = 0; k < AgentsFound.Count; k++)
            {
                EmployeeInstance e = AgentsFound[k];
                float skill = e.GetSkillValue("ba:skill_purchasingagent");
                float off = ImportDiscount(skill);
                if (k == chosen)
                {
                    best = off;
                }
                BuildingRegistration work = e.assignedAddress != null ? BuildingHelper.GetBuildingRegistration(e.assignedAddress) : null;
                string place = work != null ? work.GetDisplayName() : null;
                picks.Add(new UiFormRow
                {
                    Kind = UiFormKind.Pick, Label = e.characterData.name,
                    Hint = string.IsNullOrEmpty(place) ? UiText.T("reallife_ui_dialog_agents") : UiText.Fill("reallife_ui_dialog_agent_sub", "place", place),
                    Side = UiText.Fill("reallife_ui_dialog_agent_off", "discount", Percent(off)),
                    SideNote = UiText.Fill("reallife_ui_dialog_agent_skill", "skill", Mathf.RoundToInt(skill).ToString(CultureInfo.CurrentCulture)),
                    On = k == chosen
                });
                widgets.Add(new Widget { Drop = drop, Option = k });
            }
            _rows.InsertRange(row, picks);
            _widgets.InsertRange(row, widgets);

            // What a partnership needs (the game asks both before it shows this form, so both stand met here) and what it brings.
            Add(new UiFormRow { Kind = UiFormKind.Heading, Label = UiText.T("reallife_ui_dialog_import_needs") }, default);
            Add(new UiFormRow { Kind = UiFormKind.Check, Label = UiText.T("reallife_ui_dialog_import_warehouse"), Value = Warehouse(), On = true }, default);
            Add(new UiFormRow
            {
                Kind = UiFormKind.Check, Label = UiText.T("reallife_ui_dialog_import_agent_on_shift"), On = AgentsFound.Count > 0,
                Value = UiText.Fill("reallife_ui_dialog_import_free", "free", AgentsFound.Count.ToString(CultureInfo.CurrentCulture), "all", all.ToString(CultureInfo.CurrentCulture))
            }, default);
            Add(new UiFormRow { Kind = UiFormKind.Break }, default);
            if (chosen >= 0)
            {
                Add(new UiFormRow { Kind = UiFormKind.Figure, Label = UiText.T("reallife_ui_dialog_import_discount"), Value = Percent(best) + "%" }, default);
            }
            Add(new UiFormRow { Kind = UiFormKind.Figure, Label = UiText.T("reallife_ui_dialog_import_orders"), Value = UiText.T("reallife_ui_dialog_import_mondays") }, default);
            Add(new UiFormRow
            {
                Kind = UiFormKind.Figure, Label = UiText.T("reallife_ui_dialog_import_products"), Value = UiText.T("reallife_ui_dialog_import_in_bizman"), Strong = true
            }, default);
        }

        // A pick chosen through the game's own drop-down, so its listener sets the form's choice.
        private bool PickChosen(int row)
        {
            if (row < 0 || row >= Count || Rows[row].Kind != UiFormKind.Pick || Rows[row].Disabled)
            {
                return false;
            }
            Widget w = _widgets[row];
            if (w.Drop == null || w.Option == Index(w.Drop))
            {
                return false;
            }
            w.Drop.SelectOption(w.Option);
            return true;
        }

        // The row the drop-down was read into, from the heading on; -1 where it is not one.
        private int DropRow(global::UI.Elements.Dropdown drop, int heading)
        {
            for (int i = heading; drop != null && i < _rows.Count; i++)
            {
                if (_rows[i].Kind == UiFormKind.Choice && ReferenceEquals(_widgets[i].Drop, drop))
                {
                    return i;
                }
            }
            return -1;
        }

        // The discount an agent's skill gives, in per cent: ImportPartnership.GetDiscount's own sum.
        private static float ImportDiscount(float skill) => Mathf.Lerp(0f, 25f, skill / 100f);

        private static string Percent(float value) => value.ToString("0.#", CultureInfo.CurrentCulture);

        private static bool Contracted(List<DeliveryContract> contracts, Address business, Address store)
        {
            for (int i = 0; contracts != null && i < contracts.Count; i++)
            {
                if (contracts[i] != null && contracts[i].businessAddress == business && contracts[i].wholesaleAddress == store)
                {
                    return true;
                }
            }
            return false;
        }

        // True where the business holds a storage shelf: the game refuses a contract to one without (WholesaleStoreManagerDialog).
        private static bool Storage(BuildingRegistration reg)
        {
            if (reg.itemInstances == null)
            {
                return false;
            }
            foreach (ItemInstance item in reg.itemInstances.Values)
            {
                Item data = item != null ? item.ItemCached : null;
                if (data != null && data.HasTag(TagRef.Itemtag.isbusinessstorage))
                {
                    return true;
                }
            }
            return false;
        }

        private static bool Partnered(List<ImportPartnership> partnerships, string id)
        {
            for (int i = 0; partnerships != null && i < partnerships.Count; i++)
            {
                if (partnerships[i] != null && partnerships[i].employeeInstanceId == id)
                {
                    return true;
                }
            }
            return false;
        }

        // A business's kind and district under its name: "Bakery, Midtown".
        private static string Where(BuildingRegistration reg)
        {
            if (reg == null)
            {
                return null;
            }
            string kind = RotaGame.TypeName(reg), district = GameRegistrations.District(reg);
            return string.IsNullOrEmpty(district) ? kind : string.IsNullOrEmpty(kind) ? district : kind + ", " + district;
        }

        // The player's first rented warehouse in use, as the game finds one (ImportManagerDialog): its name and district.
        private static string Warehouse()
        {
            List<BuildingRegistration> regs = SaveGameManager.Current.BuildingRegistrations;
            for (int i = 0; regs != null && i < regs.Count; i++)
            {
                BuildingRegistration reg = regs[i];
                if (reg != null && reg.RentedByPlayer && GameRegistrations.BuildingType(reg) == "ba:buildingtype_warehouse" && reg.businessTypeName != "ba:businesstype_empty")
                {
                    string district = GameRegistrations.District(reg);
                    return string.IsNullOrEmpty(district) ? reg.GetDisplayName() : reg.GetDisplayName() + ", " + district;
                }
            }
            return null;
        }
    }
}
