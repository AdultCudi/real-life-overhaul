using System.Collections.Generic;
using RealLifeOverhaul.GameUi;
using UnityEngine;

namespace RealLifeOverhaul.Economy.Dealership
{
    /// <summary>
    /// The Growth tab: the advertising events a showroom can run, its missions, and its reputation with the perks each
    /// step opens, filtered by which. An event that is picked says what it does and costs, and books it at the showroom
    /// chosen (a featured event for the kind of car chosen); a mission says what it asks and gives; a showroom's
    /// reputation lists its steps. Written with the tab, never otherwise.
    /// </summary>
    public sealed partial class DealerApp
    {
        private const string EventPick = "event:", MissionPick = "mission:", RepPick = "rep:";

        private string _eventYard;          // the showroom an event is booked at
        private int _eventFeature;          // ...and the kind of car a featured one is of
        private readonly List<int> _kinds = new List<int>(6);

        private TableSpec WriteGrowth(InfoBox box, DealerState state, int now)
        {
            int day = now / 24;
            for (int k = 0; k < DealerCatalogue.EventKinds; k++)
            {
                TableRow row = Row(DealerDirector.EventRow(k));
                row.Kind = DealerColumns.GrowthEvents;
                DealerYard running = Running(state, k, day);
                row.Set(0, DealerText.Event(k, 0));
                row.Set(1, running != null ? running.Name : string.Empty);
                row.Set(2, DealerText.T("reallife_dealer_days", "days", DealerCatalogue.EventDays[k].ToString()), DealerCatalogue.EventDays[k]);
                Money(row, 3, DealerDirector.EventCost(k));
                row.Set(4, DealerText.EventShort(k));
                row.Set(5, running != null ? DealerText.T("reallife_dealer_state_event_on", "day", FinanceText.Day(running.EventUntil)) : DealerText.T("reallife_dealer_state_event_ready"),
                    running != null ? TableRow.Green : TableRow.Plain);
            }
            for (int y = 0; y < state.Yards.Count; y++)
            {
                DealerYard yard = state.Yards[y];
                for (int m = 0; m < DealerCatalogue.Missions; m++)
                {
                    TableRow row = Row(DealerDirector.MissionRow(yard, m));
                    row.Kind = DealerColumns.GrowthMissions;
                    bool done = DealerRules.MissionDone(yard, m);
                    int progress = DealerRules.Progress(yard, m);
                    row.Set(0, DealerText.Mission(m));
                    row.Set(1, yard.Name);
                    row.Set(2, DealerText.T("reallife_dealer_progress", "done", progress.ToString(), "target", DealerCatalogue.MissionTarget[m].ToString()), progress);
                    Count(row, 3, DealerCatalogue.MissionReward[m]);
                    row.Set(4, DealerText.MissionAbout(m));
                    row.Set(5, DealerText.T(done ? "reallife_dealer_state_mission_done" : "reallife_dealer_state_mission_open"), done ? TableRow.Green : TableRow.Plain);
                }
            }
            for (int y = 0; y < state.Yards.Count; y++)
            {
                DealerYard yard = state.Yards[y];
                int step = DealerRules.Step(yard.Rep);
                TableRow row = Row(DealerDirector.RepRow(yard));
                row.Kind = DealerColumns.GrowthRep;
                row.Set(0, DealerText.Step(step));
                row.Set(1, yard.Name);
                row.Set(2, yard.Rep.ToString(), yard.Rep);
                row.Set(3, step + 1 < DealerCatalogue.RepSteps.Length ? DealerCatalogue.RepSteps[step + 1].ToString() : "-", step + 1 < DealerCatalogue.RepSteps.Length ? DealerCatalogue.RepSteps[step + 1] : 0);
                row.Set(4, DealerText.Perk(step));
                row.Set(5, DealerText.T("reallife_dealer_state_missions", "done", Done(yard).ToString(), "all", DealerCatalogue.Missions.ToString()));
            }
            if (state.Yards.Count == 0)
            {
                Say(DealerText.T("reallife_dealer_row_no_dealership"));
            }
            if (_picked != null && _picked.StartsWith(EventPick, System.StringComparison.Ordinal))
            {
                int kind = Picked(_picked, EventPick);
                if (kind >= 0 && kind < DealerCatalogue.EventKinds)
                {
                    WriteEvent(box, state, kind, day);
                    return DealerColumns.Growth;
                }
            }
            if (_picked != null && _picked.StartsWith(MissionPick, System.StringComparison.Ordinal) && WriteMission(box, state))
            {
                return DealerColumns.Growth;
            }
            DealerYard repYard = state.Yard(PickedText(_picked, RepPick));
            if (repYard != null)
            {
                WriteRep(box, repYard);
                return DealerColumns.Growth;
            }
            _picked = null;
            box.Title(DealerText.T("reallife_dealer_growth_title"));
            box.Line(DealerText.T("reallife_dealer_growth_events"));
            box.Line(DealerText.T("reallife_dealer_growth_rep"));
            box.Line(DealerText.T("reallife_dealer_growth_missions"));
            Meta.Guide.Point(box, GuideTitle);
            return DealerColumns.Growth;
        }

        private static DealerYard Running(DealerState state, int kind, int day)
        {
            for (int y = 0; y < state.Yards.Count; y++)
            {
                if (state.Yards[y].Event == kind && state.Yards[y].EventOn(day))
                {
                    return state.Yards[y];
                }
            }
            return null;
        }

        private static int Done(DealerYard y)
        {
            int n = 0;
            for (int m = 0; m < DealerCatalogue.Missions; m++)
            {
                n += DealerRules.MissionDone(y, m) ? 1 : 0;
            }
            return n;
        }

        // An event picked: what it does and costs, where it is booked (and for which kind of car), and the button.
        private void WriteEvent(InfoBox box, DealerState state, int kind, int day)
        {
            box.Title(DealerText.Event(kind, 0));
            box.Line(DealerText.EventAbout(kind));
            int cost = DealerDirector.EventCost(kind);
            box.Line(DealerText.Fill("reallife_dealer_event_terms", "cost", DealerText.Money(cost), "days", DealerCatalogue.EventDays[kind].ToString(), "rest", DealerCatalogue.EventRest.ToString()));
            box.Line(DealerText.T("reallife_dealer_event_campaigns"));
            if (state.Yards.Count == 0)
            {
                box.Line(DealerText.T("reallife_dealer_refuse_off"));
                InfoBox.Button(box.ButtonRow(), InfoBox.Additional, "common_close", ClosePick);
                return;
            }
            int at = 0;
            for (int i = 0; i < state.Yards.Count; i++)
            {
                at = state.Yards[i].Place == _eventYard ? i : at;
            }
            DealerYard yard = state.Yards[at];
            _eventYard = yard.Place;
            if (state.Yards.Count > 1)
            {
                Choose(box, state.Yards.Count, at, i => DealerText.T("reallife_dealer_event_at", "name", state.Yards[i].Name), i => _eventYard = state.Yards[i].Place);
            }
            else
            {
                box.Line(DealerText.T("reallife_dealer_event_at", "name", yard.Name));
            }
            int feature = 0;
            if (kind == DealerCatalogue.FeaturedKind)
            {
                Kinds(_kinds);
                if (_kinds.Count > 0)
                {
                    int k = Mathf.Max(0, _kinds.IndexOf(_eventFeature));
                    feature = _kinds[k];
                    _eventFeature = feature;
                    Choose(box, _kinds.Count, k, i => DealerText.T("reallife_dealer_event_kind", "kind", DealerText.CarKind(_kinds[i])), i => _eventFeature = _kinds[i]);
                }
            }
            EventRefusal no = DealerRules.CanBook(yard, kind, day);
            if (no == EventRefusal.Running)
            {
                box.Line(DealerText.Fill("reallife_dealer_event_busy", "event", DealerText.Event(yard.Event, yard.Feature), "day", FinanceText.Day(yard.EventUntil)));
            }
            else if (no == EventRefusal.Rested)
            {
                box.Line(DealerText.T("reallife_dealer_event_resting", "day", FinanceText.Day(DealerRules.RestUntil(yard, kind) + 1)));
            }
            Transform buttons = box.ButtonRow();
            if (no == EventRefusal.None && (kind != DealerCatalogue.FeaturedKind || feature > 0))
            {
                string place = yard.Place;
                int chosen = feature;
                InfoBox.Button(buttons, InfoBox.Positive, "reallife_dealer_button_event", () => Confirm("reallife_dealer_q_event_title",
                    DealerText.Fill("reallife_dealer_q_event", "event", DealerText.Event(kind, chosen), "name", yard.Name, "cost", DealerText.Money(cost), "days", DealerCatalogue.EventDays[kind].ToString()),
                    () => _director.BookEvent(place, kind, chosen), false));
            }
            InfoBox.Button(buttons, InfoBox.Additional, "common_close", ClosePick);
            Meta.Guide.Point(box, GuideTitle);
        }

        // The kinds of car the market deals in, each once, in the catalogue's order.
        private void Kinds(List<int> into)
        {
            into.Clear();
            IReadOnlyList<MarketModel> models = _director.Models;
            for (int k = 1; k <= 6; k++)
            {
                for (int m = 0; m < models.Count; m++)
                {
                    if (models[m].Kind == k)
                    {
                        into.Add(k);
                        break;
                    }
                }
            }
        }

        private bool WriteMission(InfoBox box, DealerState state)
        {
            // "mission:<n>:<place>"
            string rest = _picked.Substring(MissionPick.Length);
            int colon = rest.IndexOf(':');
            if (colon <= 0 || !int.TryParse(rest.Substring(0, colon), out int m) || m < 0 || m >= DealerCatalogue.Missions)
            {
                return false;
            }
            DealerYard yard = state.Yard(rest.Substring(colon + 1));
            if (yard == null)
            {
                return false;
            }
            box.Title(DealerText.Mission(m));
            box.Line(DealerText.MissionAbout(m));
            box.Line(DealerText.Fill("reallife_dealer_mission_where", "name", yard.Name, "done", DealerRules.Progress(yard, m).ToString(), "target", DealerCatalogue.MissionTarget[m].ToString(),
                "points", DealerCatalogue.MissionReward[m].ToString()));
            box.Line(DealerText.T(DealerRules.MissionDone(yard, m) ? "reallife_dealer_mission_was_done" : "reallife_dealer_mission_how"));
            InfoBox.Button(box.ButtonRow(), InfoBox.Additional, "common_close", ClosePick);
            Meta.Guide.Point(box, GuideTitle);
            return true;
        }

        private void WriteRep(InfoBox box, DealerYard yard)
        {
            int step = DealerRules.Step(yard.Rep);
            box.Title(yard.Name);
            box.Line(DealerText.Fill("reallife_dealer_yard_rep", "rep", yard.Rep.ToString(), "step", DealerText.Step(step),
                "next", step + 1 < DealerCatalogue.RepSteps.Length ? DealerText.T("reallife_dealer_rep_next", "points", DealerCatalogue.RepSteps[step + 1].ToString()) : DealerText.T("reallife_dealer_rep_top")));
            for (int s = 1; s < DealerCatalogue.RepSteps.Length; s++)
            {
                box.Line(DealerText.Fill(s <= step ? "reallife_dealer_rep_step_had" : "reallife_dealer_rep_step", "step", DealerText.Step(s), "points", DealerCatalogue.RepSteps[s].ToString(),
                    "perk", DealerText.Perk(s)));
            }
            box.Line(DealerText.T("reallife_dealer_rep_how"));
            box.Line(DealerText.T("reallife_dealer_rep_standing"));
            InfoBox.Button(box.ButtonRow(), InfoBox.Additional, "common_close", ClosePick);
            Meta.Guide.Point(box, GuideTitle);
        }
    }
}
