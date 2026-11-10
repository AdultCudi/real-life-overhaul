using System;
using System.Collections.Generic;
using RealLifeOverhaul.Parties;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// The Parties app's pages written from the facts: Host a party (the place, the food and drink, the day and hour, what
    /// it costs, and the guest list with what each would answer), Staff events (the business, the kind, the day and hour,
    /// what it costs, the manager's schedule) and the Diary (what is coming, with its answers and its way out, what is
    /// over, and the rival tycoons' events). One primary action a page. Game-free; the live source and the samples give
    /// it facts and the page is the same.
    /// </summary>
    public static class PartyPages
    {
        public const int TabHost = 0, TabStaff = 1, TabDiary = 2;
        public const int RowParty = 0, RowInvite = 1, RowEvent = 2, RowRival = 3;
        public const string PickKey = "row:";

        public static void Fill(VentModel m, VentView v, PartyFacts f, IPartyActions act, VentWords w)
        {
            m.Title = w.T("reallife_party_app", "Parties");
            m.Icon = UiIcon.ForkKnife;
            m.Prompts = VenPages.Prompts(w);
            var runs = new VentRuns();
            m.Nav = new[]
            {
                new UiNavEntry { Icon = UiIcon.People, Label = w.T("reallife_party_tab_host", "Host a party") },
                new UiNavEntry { Icon = UiIcon.Briefcase, Label = w.T("reallife_party_tab_staff", "Staff events") },
                new UiNavEntry { Icon = UiIcon.Calendar, Label = w.T("reallife_party_tab_diary", "Diary"), Count = f.Coming0.Length > 0 ? f.Coming0.Length.ToString() : null }
            };
            m.Tab = Math.Max(0, Math.Min(m.Nav.Length - 1, v.Tab));
            if (!f.Ready)
            {
                m.Shape = VentShape.Off;
                m.OffTitle = f.OffTitle ?? w.T("reallife_party_off_title", "Parties are switched off");
                m.OffBody = f.OffBody ?? w.T("reallife_party_off_body", "Switch Living population and Parties and company events on in Mod settings. What is planned is kept meanwhile.");
                m.Runs = runs.ToArray();
                return;
            }
            if (m.Tab == TabStaff)
            {
                Staff(m, f, act, w, runs);
            }
            else if (m.Tab == TabDiary)
            {
                Diary(m, v, f, act, w, runs);
            }
            else
            {
                Host(m, f, act, w, runs);
            }
            m.Runs = runs.ToArray();
        }

        // ------------------------------------------------------------------------------------------------- hosting

        private static void Host(VentModel m, PartyFacts f, IPartyActions act, VentWords w, VentRuns runs)
        {
            var left = new List<VentBlock>(14);
            var right = new List<VentBlock>(4);
            m.Next = w.T("reallife_party_host_next", "Ask the people you know to your home or to a place in the city. You must be there when it starts.");
            left.Add(VentBlock.Section(w.T("reallife_party_sec_party", "The party")));
            left.Add(VentBlock.Choice(w.T("reallife_party_f_where", "Where"), w.T(PartyKeys.Place(f.Place), PlaceEnglish(f.Place)), runs.Add(() => { act.NextPlace(); return true; })));
            left.Add(VentBlock.Row(w.T("reallife_party_f_place", "Place"), f.PlaceFound ? f.PlaceName : w.T("reallife_party_f_place_none", "None found"),
                f.PlaceFound ? UiRole.Text : UiRole.DangerText));
            if (f.Place != PartyPlace.Home)
            {
                left.Add(VentBlock.Link(w.T("reallife_party_f_other", "Another place"), runs.Add(() => { act.OtherVenue(); return true; })));
            }
            left.Add(VentBlock.Choice(w.T("reallife_party_f_tier", "Food and drink"), w.T(PartyKeys.Tier(f.Tier), TierEnglish(f.Tier)), runs.Add(() => { act.NextTier(); return true; })));
            int day = runs.Add(() => { act.Day(-1); return true; });
            runs.Add(() => { act.Day(1); return true; });
            left.Add(VentBlock.Stepper(w.T("reallife_party_f_day", "Day"), f.DayText, day, f.CanDayLess, f.CanDayMore));
            int hour = runs.Add(() => { act.Hour(-1); return true; });
            runs.Add(() => { act.Hour(1); return true; });
            left.Add(VentBlock.Stepper(w.T("reallife_party_f_hour", "Starts at"), f.HourText, hour, f.CanHourLess, f.CanHourMore));
            left.Add(VentBlock.Row(w.T("reallife_party_f_lasts", "Lasts"), w.F("reallife_party_f_hours", "{count} hours", "count", f.Hours.ToString())));
            left.Add(VentBlock.Row(w.T("reallife_party_f_room", "Room for"), w.F("reallife_party_f_guests", "{count} guests", "count", f.Capacity.ToString())));
            left.Add(VentBlock.Row(w.T("reallife_party_f_head", "A head"), f.HeadText));
            left.Add(VentBlock.Row(w.T("reallife_party_f_cost", "Cost"), f.CostText));
            string why = f.Stand ?? f.Why;
            if (!string.IsNullOrEmpty(why))
            {
                left.Add(VentBlock.Note(why, UiRole.Warn));
            }
            left.Add(VentBlock.Buttons(new UiActionModel
            {
                Label = w.T("reallife_party_f_send", "Send the invitations"), Kind = UiButtonKind.Primary, Icon = UiIcon.Send, Key = UiAction.Alt,
                Disabled = !string.IsNullOrEmpty(why), Arg = runs.Add(() => { act.Host(); return true; })
            }));
            left.Add(VentBlock.Note(w.T("reallife_party_f_note", "Parties bring people closer up to a point and by so much a week, however many you give. Miss your own and every guest minds.")));

            right.Add(VentBlock.Section(w.F("reallife_party_sec_guests", "Guests: {picked} asked, {coming} would come", "picked", f.Picked.ToString(), "coming", f.Coming.ToString())));
            right.Add(Guests(f, act, w, runs));
            m.Left = left.ToArray();
            m.Right = right.ToArray();
        }

        private static VentBlock Guests(PartyFacts f, IPartyActions act, VentWords w, VentRuns runs)
        {
            var rows = new UiDataRow[f.People.Length];
            var keys = new string[f.People.Length];
            var toggles = new int[f.People.Length];
            for (int i = 0; i < f.People.Length; i++)
            {
                PartyPersonFact p = f.People[i];
                int index = p.Index;
                bool yes = p.Picked && p.Answer == PartyAnswer.Yes;
                rows[i] = new UiDataRow
                {
                    Cells = new[]
                    {
                        p.Name, p.Picked ? w.T(PartyKeys.Answer(p.Answer), AnswerEnglish(p.Answer)) : w.T("reallife_party_g_not_asked", "Not asked"),
                        p.Picked ? w.T("reallife_party_g_take_off", "Take off the list") : w.T("reallife_party_g_ask", "Ask")
                    },
                    Values = new[] { 0, 0, (int)(p.Picked ? UiIcon.Close : UiIcon.Plus) },
                    Tones = new[] { UiRole.None, !p.Picked ? UiRole.Muted : yes ? UiRole.Ok : UiRole.Warn, UiRole.None },
                    Sub = p.Stage, Hue = VentWords.HueOf(p.Name), Ring = p.Ring, Picked = p.Picked
                };
                keys[i] = null;
                toggles[i] = runs.Add(() => { act.Toggle(index); return true; });
            }
            var table = new UiDataTableModel
            {
                Columns = new[]
                {
                    new UiDataColumn { Title = w.T("reallife_party_g_who", "Who"), Kind = UiCellKind.Person, Width = 0f },
                    new UiDataColumn { Title = w.T("reallife_party_g_answer", "Answer"), Kind = UiCellKind.Chip, Width = 232f },
                    new UiDataColumn { Title = w.T("reallife_party_g_list", "List"), Kind = UiCellKind.Action, Width = 72f }
                },
                Rows = rows, RowCount = rows.Length, EmptyIcon = UiIcon.People,
                EmptyTitle = w.T("reallife_party_g_none_title", "Nobody to ask yet"),
                EmptyBody = f.PeopleOff ?? w.T("reallife_party_g_none_body", "Meet people and swap numbers. Everybody in People can be asked.")
            };
            return new VentBlock { Kind = VentBlockKind.Table, Table = table, RowKeys = keys, RowRuns = toggles, Base = UiDataTable.First, Rows = 12 };
        }

        // ------------------------------------------------------------------------------------------------- staff events

        private static void Staff(VentModel m, PartyFacts f, IPartyActions act, VentWords w, VentRuns runs)
        {
            var left = new List<VentBlock>(14);
            var right = new List<VentBlock>(10);
            m.Next = w.T("reallife_party_staff_next", "Take a business's staff out. The business pays, and it is shut for the opening hours the event takes.");
            if (f.Businesses.Length == 0)
            {
                m.Empty = w.T("reallife_party_staff_none", "You have no business with staff yet.");
                return;
            }
            PartyBizFact b = f.Businesses[Math.Max(0, Math.Min(f.Businesses.Length - 1, f.Business))];
            left.Add(VentBlock.Section(w.T("reallife_party_sec_event", "The event")));
            left.Add(VentBlock.Choice(w.T("reallife_party_e_business", "Business"), b.Name, runs.Add(() => { act.NextBusiness(); return true; }), f.Businesses.Length < 2));
            left.Add(VentBlock.Choice(w.T("reallife_party_e_kind", "Kind"), w.T(PartyKeys.Kind(f.Kind), KindEnglish(f.Kind)), runs.Add(() => { act.NextKind(); return true; })));
            int day = runs.Add(() => { act.EventDay(-1); return true; });
            runs.Add(() => { act.EventDay(1); return true; });
            left.Add(VentBlock.Stepper(w.T("reallife_party_f_day", "Day"), f.EventDayText, day, f.CanEventDayLess, f.CanEventDayMore));
            int hour = runs.Add(() => { act.EventHour(-1); return true; });
            runs.Add(() => { act.EventHour(1); return true; });
            left.Add(VentBlock.Stepper(w.T("reallife_party_f_hour", "Starts at"), f.EventHourText, hour, f.CanEventHourLess, f.CanEventHourMore));
            left.Add(VentBlock.Row(w.T("reallife_party_f_lasts", "Lasts"), w.F("reallife_party_f_hours", "{count} hours", "count", f.EventHours.ToString())));
            left.Add(VentBlock.Row(w.T("reallife_party_e_staff", "Staff"), b.Staff.ToString()));
            left.Add(VentBlock.Row(w.T("reallife_party_f_head", "A head"), f.EventHeadText));
            left.Add(VentBlock.Row(w.T("reallife_party_e_cost", "Cost, from the business"), f.EventCostText));
            string why = f.Stand ?? f.EventWhy;
            if (!string.IsNullOrEmpty(why))
            {
                left.Add(VentBlock.Note(why, UiRole.Warn));
            }
            left.Add(VentBlock.Buttons(new UiActionModel
            {
                Label = w.T("reallife_party_e_book", "Hold the staff event"), Kind = UiButtonKind.Primary, Icon = UiIcon.Check, Key = UiAction.Alt,
                Disabled = !string.IsNullOrEmpty(why), Arg = runs.Add(() => { act.Book(); return true; })
            }));

            right.Add(VentBlock.Section(w.T("reallife_party_sec_staff", "The staff")));
            right.Add(VentBlock.Row(w.T("reallife_party_e_low", "Standing low"), b.Low.ToString(), b.Low > 0 ? UiRole.DangerText : UiRole.Text));
            right.Add(VentBlock.Row(w.T("reallife_party_e_underpaid", "Underpaid"), b.Underpaid.ToString(), b.Underpaid > 0 ? UiRole.DangerText : UiRole.Text));
            right.Add(VentBlock.Row(w.T("reallife_party_e_last", "Last event"), string.IsNullOrEmpty(b.Last) ? w.T("reallife_party_e_never", "None yet") : b.Last));
            right.Add(VentBlock.Note(w.T("reallife_party_e_note",
                "An event lifts satisfaction and standing by so much a month, half for somebody underpaid, and the lift fades. It is no substitute for pay.")));
            right.Add(VentBlock.Section(w.T("reallife_party_sec_manager", "The manager's schedule")));
            right.Add(VentBlock.Choice(w.T("reallife_party_e_every", "Hold one"),
                b.Every > 0 ? w.F("reallife_party_e_every_days", "Every {count} days", "count", b.Every.ToString()) : w.T("reallife_party_e_every_off", "Not on a schedule"),
                runs.Add(() => { act.NextSchedule(); return true; }), !string.IsNullOrEmpty(f.Stand)));
            right.Add(VentBlock.Choice(w.T("reallife_party_e_plan_kind", "Of the kind"), w.T(PartyKeys.Kind(b.PlanKind), KindEnglish(b.PlanKind)),
                runs.Add(() => { act.NextPlanKind(); return true; }), b.Every <= 0 || !string.IsNullOrEmpty(f.Stand)));
            right.Add(b.HasManager
                ? VentBlock.Check(w.F("reallife_party_e_manager_is", "{name} books it the day before.", "name", b.Manager ?? string.Empty), true)
                : VentBlock.Check(w.T("reallife_party_e_manager_none", "This business has no manager at work. The schedule waits for one."), false, b.Every <= 0));
            right.Add(VentBlock.Link(w.T("reallife_party_e_managers", "Managers in the Company app"), runs.Add(() => { act.OpenManagers(); return true; })));
            m.Left = left.ToArray();
            m.Right = right.ToArray();
        }

        // ------------------------------------------------------------------------------------------------- the diary

        private static void Diary(VentModel m, VentView v, PartyFacts f, IPartyActions act, VentWords w, VentRuns runs)
        {
            var left = new List<VentBlock>(6);
            var right = new List<VentBlock>(8);
            m.Next = w.T("reallife_party_diary_next", "What is planned, what you are asked to, and what is over. Be at the place when a party starts.");
            left.Add(VentBlock.Section(w.T("reallife_party_sec_coming", "Coming up")));
            left.Add(Rows(f.Coming0, v, runs, w.T("reallife_party_d_none_title", "Nothing planned"),
                w.T("reallife_party_d_none_body", "Host a party or hold a staff event and it stands here, and in the Planner.")));
            if (f.Past.Length > 0)
            {
                left.Add(VentBlock.Section(w.T("reallife_party_sec_past", "Over")));
                left.Add(Rows(f.Past, v, runs, null, null));
            }
            PartyRowFact picked = default;
            bool found = false;
            for (int pass = 0; pass < 2 && !found; pass++)
            {
                PartyRowFact[] rows = pass == 0 ? f.Coming0 : f.Past;
                for (int i = 0; i < rows.Length && !found; i++)
                {
                    if (v.Pick == Key(rows[i]))
                    {
                        picked = rows[i];
                        found = true;
                    }
                }
            }
            if (!found && f.Coming0.Length > 0)
            {
                picked = f.Coming0[0];
                found = true;
            }
            if (found)
            {
                Detail(right, picked, f, act, w, runs);
            }
            right.Add(VentBlock.Section(w.T("reallife_party_sec_city", "In the city")));
            if (f.City.Length == 0)
            {
                right.Add(VentBlock.Note(w.T("reallife_party_d_city_none", "No rival has held a staff event lately.")));
            }
            else
            {
                right.Add(Rows(f.City, v, runs, null, null));
            }
            m.Left = left.ToArray();
            m.Right = right.ToArray();
        }

        private static string Key(PartyRowFact row) => PickKey + row.Kind + ":" + row.Id;

        private static VentBlock Rows(PartyRowFact[] rows, VentView v, VentRuns runs, string emptyTitle, string emptyBody)
        {
            var items = new VentItem[rows.Length];
            for (int i = 0; i < rows.Length; i++)
            {
                PartyRowFact r = rows[i];
                string key = Key(r);
                items[i] = new VentItem
                {
                    Icon = r.Icon, Tint = UiRole.None, Face = r.Face, Hue = r.Face != null ? VentWords.HueOf(r.Face) : 0f, Title = r.Title, Sub = r.Sub, Chip = r.Chip,
                    ChipTone = r.ChipTone, Amount = r.Amount, Run = r.Kind == RowRival ? -1 : runs.Add(() => { v.Pick = key; return true; })
                };
            }
            return new VentBlock { Kind = VentBlockKind.Items, Items = items, EmptyTitle = emptyTitle, EmptyBody = emptyBody, Icon = UiIcon.Calendar };
        }

        private static void Detail(List<VentBlock> r, PartyRowFact row, PartyFacts f, IPartyActions act, VentWords w, VentRuns runs)
        {
            r.Add(VentBlock.Section(row.Title));
            r.Add(VentBlock.Row(w.T("reallife_party_d_when", "When"), row.Sub));
            if (!string.IsNullOrEmpty(row.Amount))
            {
                r.Add(VentBlock.Row(w.T("reallife_party_f_cost", "Cost"), row.Amount));
            }
            if (!string.IsNullOrEmpty(row.Chip))
            {
                r.Add(VentBlock.Row(w.T("reallife_party_d_state", "State"), row.Chip));
            }
            if (!string.IsNullOrEmpty(f.Stand))
            {
                r.Add(VentBlock.Note(f.Stand, UiRole.Warn));
                return;
            }
            var actions = new List<UiActionModel>(3);
            int kind = row.Kind, id = row.Id;
            if (row.CanLocate)
            {
                actions.Add(new UiActionModel
                {
                    Label = w.T("reallife_party_d_locate", "Set destination"), Kind = UiButtonKind.Secondary, Icon = UiIcon.Locate, Arg = runs.Add(() => { act.Locate(kind, id); return true; })
                });
            }
            if (row.CanAnswer)
            {
                actions.Add(new UiActionModel
                {
                    Label = w.T("reallife_party_d_decline", "Decline"), Kind = UiButtonKind.Secondary, Icon = UiIcon.Close, Arg = runs.Add(() => { act.Reply(id, false); return true; })
                });
                actions.Add(new UiActionModel
                {
                    Label = w.T("reallife_party_d_accept", "Accept"), Kind = UiButtonKind.Primary, Icon = UiIcon.Check, Key = UiAction.Alt,
                    Arg = runs.Add(() => { act.Reply(id, true); return true; })
                });
            }
            if (row.CanCancel)
            {
                r.Add(VentBlock.Note(w.F("reallife_party_d_back", "Calling it off now gives {back} back.", "back", row.Back ?? string.Empty)));
                actions.Add(new UiActionModel
                {
                    Label = w.T("reallife_party_d_cancel", "Call it off"), Kind = UiButtonKind.Destructive, Icon = UiIcon.Close, Arg = runs.Add(() => { act.Cancel(kind, id); return true; })
                });
            }
            if (actions.Count > 0)
            {
                r.Add(VentBlock.Buttons(actions.ToArray()));
            }
        }

        // ------------------------------------------------------------------------------------------------- the words while a key is new

        public static string PlaceEnglish(PartyPlace place) => place == PartyPlace.Bar ? "A nightclub's bar" : place == PartyPlace.Restaurant ? "A restaurant" : "Your home";

        public static string TierEnglish(PartyTier tier) => tier == PartyTier.Lavish ? "A meal and drinks" : tier == PartyTier.Good ? "A meal" : "Snacks and soft drinks";

        public static string KindEnglish(StaffEventKind kind) => kind == StaffEventKind.TeamDay ? "A team day" : kind == StaffEventKind.EveningOut ? "An evening out" : "A staff lunch";

        public static string AnswerEnglish(PartyAnswer answer)
        {
            switch (answer)
            {
                case PartyAnswer.Yes: return "Would come";
                case PartyAnswer.Quarrel: return "Still cross with you";
                case PartyAnswer.Asleep: return "Asleep then";
                case PartyAnswer.Working: return "At work then";
                case PartyAnswer.Far: return "Too far for them";
                case PartyAnswer.RoughDay: return "Having a rough day";
                case PartyAnswer.Busy: return "Has something on";
                default: return "Does not know you well";
            }
        }
    }
}
