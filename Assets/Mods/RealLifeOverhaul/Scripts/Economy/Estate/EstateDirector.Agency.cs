using UI.Notification;

namespace RealLifeOverhaul.Economy
{
    /// <summary>The estate agency's day (AgencyRules) and what the player does with it; its Planner rows are the property part's.</summary>
    public sealed partial class EstateDirector
    {
        private void AgencyDay(int day)
        {
            AgencyFile agency = _state.Agency;
            if (!agency.Running && agency.Open.Count == 0)
            {
                return;
            }
            Instruction arrived = AgencyRules.Arrive(agency, _state.Seed, day, _climate, _state.Real);
            if (arrived != null)
            {
                Tell(NotificationType.Info, arrived.Sale ? "reallife_estate_note_instruction_sale" : "reallife_estate_note_instruction_let",
                    Data("district", EstateText.District(arrived.District), "amount", EstateText.Money(arrived.Value), "day", arrived.Day.ToString()), TabAgency, "ins:" + arrived.Id);
            }
            _events.Clear();
            AgencyRules.Day(agency, _state.Seed, day, _climate, _events);
            for (int i = 0; i < _events.Count; i++)
            {
                AgencyEvent e = _events[i];
                string district = EstateText.District(e.District);
                switch (e.Kind)
                {
                    case AgencyEvent.Completed:
                        Pay(e.Amount, EstateGame.CommissionKey, district, agency.Running ? agency.Office : null, "commission on an instruction that completed");
                        Tell(NotificationType.Success, "reallife_estate_note_commission", Data("district", district, "amount", EstateText.Money(e.Amount)), TabAgency, null);
                        break;
                    case AgencyEvent.Agreed:
                        Tell(NotificationType.Info, "reallife_estate_note_agreed", Data("district", district, "amount", EstateText.Money(e.Amount)), TabAgency, "ins:" + e.Id);
                        break;
                    case AgencyEvent.NoDeal:
                    case AgencyEvent.FellThrough:
                    case AgencyEvent.Lapsed:
                        string key = e.Kind == AgencyEvent.NoDeal ? "reallife_estate_note_no_deal"
                            : e.Kind == AgencyEvent.Lapsed ? "reallife_estate_note_instruction_lost" : "reallife_estate_note_fell_through";
                        Tell(NotificationType.Warning, key, Data("district", district), TabAgency, null);
                        break;
                }
                if (Verbose)
                {
                    Logger?.Info("Estate check (agency, instruction " + e.Id + " in " + e.District + "): outcome " + e.Kind + ", " + e.Amount
                                 + (e.Kind == AgencyEvent.Completed ? " commission paid" : string.Empty) + "; " + agency.Capacity / 10f + " agent's hour(s) a day from " + agency.Negotiators
                                 + " negotiator(s): PASS");
                }
            }
        }

        /// <summary>The agency opens at an office the player holds, or moves there.</summary>
        public void OpenAgency(string place)
        {
            if (!On(EstatePart.Property) || string.IsNullOrEmpty(place))
            {
                return;
            }
            _state.Agency.Office = place;
            EstateGame.ReadAgency(_state.Agency);
            Changed();
        }

        /// <summary>The agency shuts: what it had in hand lapses by its own days.</summary>
        public void CloseAgency()
        {
            if (On(EstatePart.Property))
            {
                _state.Agency.Office = string.Empty;
                _state.Agency.Capacity = _state.Agency.Negotiators = 0;
                Changed();
            }
        }

        /// <summary>An instruction is taken on: its marketing is paid for now.</summary>
        public void TakeInstruction(int id)
        {
            Instruction i = On(EstatePart.Property) ? _state.Agency.Find(id) : null;
            int cost = AgencyRules.TakeCost(i);
            if (cost <= 0)
            {
                return;
            }
            string office = _state.Agency.Running ? _state.Agency.Office : null;
            if (!Cost(cost, EstateGame.MarketingKey, EstateText.District(i.District), office, "marketing an instruction worth " + i.Value))
            {
                Notifications.ShowInsufficientMoney();
                return;
            }
            AgencyRules.Take(i, Save.Day);
            Changed();
        }

        public void DeclineInstruction(int id)
        {
            Instruction i = On(EstatePart.Property) ? _state.Agency.Find(id) : null;
            if (i != null && i.Stage == (int)InstructionStage.Offered)
            {
                _state.Agency.Open.Remove(i);
                Changed();
            }
        }
    }
}
