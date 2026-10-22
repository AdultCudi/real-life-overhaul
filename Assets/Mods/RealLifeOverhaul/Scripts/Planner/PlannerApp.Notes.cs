using RealLifeOverhaul.GameUi;
using Streets;
using UI;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul.Planner
{
    /// <summary>The Planner's notes part: writing, ticking off and deleting the player's own entries, and the settings page.</summary>
    public sealed partial class PlannerApp
    {
        private const int NoteDaysAhead = 60;   // days a note can be written for

        private static readonly string[] Presets =
        {
            "reallife_planner_preset_shopping", "reallife_planner_preset_call", "reallife_planner_preset_meet", "reallife_planner_preset_bill",
            "reallife_planner_preset_gym", "reallife_planner_preset_collect", "reallife_planner_preset_clean", "reallife_planner_preset_rest"
        };

        // PlannerText.Leads but "when to leave", which is set in the entry's box for one time
        private static readonly ReminderLead[] NoteLeads =
        {
            ReminderLead.None, ReminderLead.QuarterBefore, ReminderLead.HalfHourBefore, ReminderLead.HourBefore, ReminderLead.AtTheTime, ReminderLead.EveningBefore
        };

        private static readonly string[] RepeatKeys = { "reallife_planner_repeat_once", "reallife_planner_repeat_daily", "reallife_planner_repeat_weekly" };

        // what each switch of the settings page shows and hides
        private static readonly PlannerCategory[][] Groups =
        {
            new[] { PlannerCategory.Work },
            new[] { PlannerCategory.Court, PlannerCategory.Licence },
            new[] { PlannerCategory.Family, PlannerCategory.Social },
            new[] { PlannerCategory.Travel },
            new[] { PlannerCategory.City },
            new[] { PlannerCategory.Business, PlannerCategory.Money },
            new[] { PlannerCategory.Health },
            new[] { PlannerCategory.Note }
        };

        private static readonly string[] GroupKeys =
        {
            "reallife_planner_show_work", "reallife_planner_show_court", "reallife_planner_show_people", "reallife_planner_show_travel",
            "reallife_planner_show_city", "reallife_planner_show_business", "reallife_planner_show_health", "reallife_planner_show_notes"
        };

        // the entry being written
        private int _draftId, _draftDay, _draftStart;
        private string _draftTitle, _draftOwn, _draftPlace, _draftStreet;
        private int _draftNumber;
        private ReminderLead _draftLead;
        private PlannerRepeat _draftRepeat;

        private void ToggleDone(int noteId, int day)
        {
            PlannerNote note = PlannerStore.Note(noteId);
            if (note == null)
            {
                return;
            }
            PlannerStore.Falls(note, day, out bool done);
            PlannerStore.SetDone(note, day, !done);
            if (done)
            {
                PlannerStore.Arm(note, PlannerDirector.Now());   // not done after all: its reminder again
            }
            Refresh();
        }

        private void DeleteNote(int noteId)
        {
            GameConfirm.Show("reallife_planner_delete_title", "reallife_planner_delete_body", () => UiClone.Safe(() =>
            {
                PlannerStore.RemoveNote(noteId);
                _selected = default(EntryId);
                _page = Page.Glance;
                Refresh();
            }), null, "filebrowser_delete", "common_cancel", true, true);
        }

        // a new note on a day, or one there; a time of a repeating note opens the whole of it on that day
        private void EditNote(PlannerNote note, int day)
        {
            int today = TimeHelper.CurrentDay;
            if (note == null && PlannerStore.Notes.Count >= PlannerStore.MostNotes)
            {
                Notifications.ShowError("reallife_planner_notes_full", null, false);
                return;
            }
            _draftId = note != null ? note.Id : 0;
            _draftOwn = null;
            _draftTitle = note != null ? note.Title : PlannerText.T(Presets[0]);
            if (note != null && PresetIndex(note.Title) < 0)
            {
                _draftOwn = note.Title;
            }
            _draftRepeat = note != null ? note.Repeat : PlannerRepeat.Once;
            int first = note == null ? day : note.Repeat == PlannerRepeat.Once ? note.Day : Mathf.Max(PlannerRules.NextOccurrence(note.Repeat, note.Day, today), day);
            _draftDay = Mathf.Clamp(first, today, today + NoteDaysAhead - 1);
            _draftStart = note != null ? note.Start : (day == today ? Mathf.Min(23, TimeHelper.CurrentHour + 1) : 9) * 60;
            _draftPlace = note != null ? note.Place : null;
            _draftStreet = note != null ? note.Street : null;
            _draftNumber = note != null ? note.Number : 0;
            _draftLead = note == null ? PlannerStore.DefaultLead
                : note.Repeat == PlannerRepeat.Once ? PlannerStore.LeadOf(PlannerStore.NoteEntry(note, note.Day)) : note.Lead;
            if (System.Array.IndexOf(NoteLeads, _draftLead) < 0)
            {
                _draftLead = ReminderLead.None;   // when to leave is set in the entry's box
            }
            Show(Page.Note);
        }

        private static int PresetIndex(string title)
        {
            for (int i = 0; i < Presets.Length; i++)
            {
                if (PlannerText.T(Presets[i]) == title)
                {
                    return i;
                }
            }
            return -1;
        }

        private void WriteNote(InfoBox box)
        {
            box.Title(PlannerText.T(_draftId == 0 ? "reallife_planner_new_note" : "reallife_planner_note_edit"));
            NoteTitle(box);
            NoteWhen(box);
            NoteWhere(box);
            NoteReminder(box);
            Transform row = box.ButtonRow();
            InfoBox.Button(row, InfoBox.Positive, "menu_save", SaveNote);
            InfoBox.Button(row, InfoBox.Additional, "common_cancel", () => Show(Page.Glance));
        }

        // a ready title, or the player's own typed into the game's text box
        private void NoteTitle(InfoBox box)
        {
            box.Row("reallife_planner_note_title", _draftTitle);
            int own = _draftOwn != null ? 1 : 0;
            int at = _draftOwn != null && _draftTitle == _draftOwn ? 0 : Mathf.Max(0, PresetIndex(_draftTitle)) + own;
            box.Selector(Presets.Length + own, at, i => own == 1 && i == 0 ? _draftOwn : PlannerText.T(Presets[i - own]), i =>
            {
                _draftTitle = own == 1 && i == 0 ? _draftOwn : PlannerText.T(Presets[i - own]);
                Write();
            });
            UIs uis = InstanceBehavior<UIs>.Instance;
            if (uis != null && uis.textInputUi != null)
            {
                Transform type = box.ButtonRow();
                InfoBox.Button(type, InfoBox.Additional, "reallife_planner_note_type", () =>
                    GameUi.TextBox.Ask("reallife_planner_note_placeholder", "common_confirm", text => UiClone.Safe(() =>
                    {
                        string clean = PlannerStore.Clean(text);
                        if (clean.Length > 0)
                        {
                            _draftOwn = clean;
                            _draftTitle = clean;
                        }
                        if (_page == Page.Note)
                        {
                            Write();
                        }
                    }), null, _draftOwn ?? string.Empty, false, PlannerStore.TitleLength));
            }
        }

        private void NoteWhen(InfoBox box)
        {
            int today = TimeHelper.CurrentDay;
            box.Headline("reallife_planner_note_when");
            box.Selector(NoteDaysAhead, Mathf.Clamp(_draftDay - today, 0, NoteDaysAhead - 1), i => PlannerText.Day(today + i, today), i =>
            {
                _draftDay = today + i;
                if (_draftRepeat == PlannerRepeat.Weekly)
                {
                    Write();   // "every week on" names the new day's weekday
                }
            });
            box.Selector(25, _draftStart < 0 ? 0 : _draftStart / 60 + 1, i => i == 0 ? PlannerText.T("reallife_planner_when_all_day") : PlannerText.Clock((i - 1) * 60),
                i => _draftStart = i == 0 ? -1 : (i - 1) * 60);
            box.Selector(RepeatKeys.Length, (int)_draftRepeat, i => PlannerText.T(RepeatKeys[i], "day", PlannerText.Weekday(_draftDay)), i => _draftRepeat = (PlannerRepeat)i);
        }

        private void NoteWhere(InfoBox box)
        {
            box.Headline("reallife_planner_note_where");
            box.Line(_draftPlace ?? PlannerText.T("reallife_planner_note_no_place"));
            Transform place = box.ButtonRow();
            InfoBox.Button(place, InfoBox.Additional, "reallife_planner_note_here", UseThisPlace);
            if (_draftPlace != null)
            {
                InfoBox.Button(place, InfoBox.Negative, "reallife_planner_note_clear_place", () =>
                {
                    _draftPlace = null;
                    _draftStreet = null;
                    Write();
                });
            }
        }

        private void NoteReminder(InfoBox box)
        {
            if (!Settings.PlannerReminders)
            {
                return;
            }
            box.Headline("reallife_planner_reminder_head");
            int at = Mathf.Max(0, System.Array.IndexOf(NoteLeads, _draftLead));
            box.Selector(NoteLeads.Length, at, i => PlannerText.T(PlannerText.LeadKey(NoteLeads[i])), i => _draftLead = NoteLeads[i]);
        }

        // the building the player is in; the street has no address to keep
        private void UseThisPlace()
        {
            BuildingRegistration here = BuildingManager.IsInsideBuilding && InstanceBehavior<BuildingManager>.Instance != null
                ? InstanceBehavior<BuildingManager>.Instance.buildingRegistration : null;
            if (here == null || here.Address == null || string.IsNullOrEmpty(here.Address.streetName))
            {
                Notifications.ShowError("reallife_planner_note_here_street", null, false);
                return;
            }
            _draftStreet = here.Address.streetName;
            _draftNumber = here.Address.streetNumber;
            _draftPlace = here.Address.ToFormattedString();
            Write();
        }

        private void SaveNote()
        {
            PlannerNote note = _draftId != 0 ? PlannerStore.Note(_draftId) : PlannerStore.AddNote(_draftTitle, _draftDay, _draftStart);
            if (note == null)
            {
                Notifications.ShowError("reallife_planner_notes_full", null, false);
                Show(Page.Glance);
                return;
            }
            int now = PlannerDirector.Now();
            PlannerStore.DropReminders(NoteSource.SourceId, note.Id);   // set again below for its new time
            if (note.Repeat != _draftRepeat)
            {
                note.Done = false;
                note.DoneDay = -1;
            }
            note.Title = PlannerStore.Clean(_draftTitle);
            note.Day = _draftDay;
            note.Start = _draftStart;
            note.Repeat = _draftRepeat;
            note.Lead = _draftLead;
            note.Place = _draftPlace;
            note.Street = _draftPlace != null ? _draftStreet : null;
            note.Number = _draftPlace != null ? _draftNumber : 0;
            note.HasPlace = false;
            bool set = true;
            if (Settings.PlannerReminders && _draftLead != ReminderLead.None)
            {
                if (note.Repeat == PlannerRepeat.Once)
                {
                    set = PlannerStore.SetReminder(PlannerStore.NoteEntry(note, note.Day), note.Day, note.Start, _draftLead, now);
                }
                else
                {
                    PlannerStore.Arm(note, now);
                }
            }
            if (!set)
            {
                Notifications.Show(NotificationType.Warning, "reallife_planner_reminder_too_late", null, 6f, null, null, true, false);
            }
            _selected = PlannerStore.NoteEntry(note, note.Day);
            _page = Page.Entry;
            Refresh();
            _director.Seen();
        }

        private void WriteSettings(InfoBox box)
        {
            box.Title(PlannerText.T("bizman_menu_settings"));
            if (box.HasSwitch)
            {
                box.Headline("reallife_planner_settings_show");
                for (int g = 0; g < Groups.Length; g++)
                {
                    PlannerCategory[] group = Groups[g];
                    box.Switch(GroupKeys[g], PlannerStore.Shows(group[0]), on =>
                    {
                        for (int c = 0; c < group.Length; c++)
                        {
                            PlannerStore.Show(group[c], on);
                        }
                        Refresh();
                    });
                }
            }
            if (Settings.PlannerReminders && box.HasSelector)
            {
                // a new entry's reminder: any of NoteLeads but none
                box.Headline("reallife_planner_settings_lead");
                int at = Mathf.Max(1, System.Array.IndexOf(NoteLeads, PlannerStore.DefaultLead)) - 1;
                box.Selector(NoteLeads.Length - 1, at, i => PlannerText.T(PlannerText.LeadKey(NoteLeads[i + 1])), i => PlannerStore.DefaultLead = NoteLeads[i + 1]);
            }
            Transform row = box.ButtonRow();
            InfoBox.Button(row, InfoBox.Additional, "common_close", () => Show(Page.Glance));
        }
    }
}
