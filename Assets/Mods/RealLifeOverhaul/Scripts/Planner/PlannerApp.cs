using System;
using System.Collections.Generic;
using BAModAPI;
using Entities;
using Extensions;
using Helpers;
using Streets;
using RealLifeOverhaul.GameUi;
using TMPro;
using UI;
using UI.Guiders;
using UI.Notification;
using UI.Smartphone;
using UnityEngine;
using UnityEngine.UI;

namespace RealLifeOverhaul.Planner
{
    /// <summary>
    /// What the Planner shows. On the right two tabs over the table: "Today" and "Upcoming" (the seven days after,
    /// each under its day). On the left the white box, which is one of four pages: the day at a glance, one entry with
    /// what can be done about it, a note being written, or the settings. Everything on it is the game's (GameUi).
    /// The app does nothing while it is closed; it reads the sources when it opens and when the player changes something.
    /// </summary>
    public sealed class PlannerApp
    {
        public const int UpcomingDays = 7;
        private const int NoteDaysAhead = 60;

        public static IModLogger Logger;

        private enum Page
        {
            Glance, Entry, Note, Settings
        }

        private static readonly string[] Presets =
        {
            "reallife_planner_preset_shopping", "reallife_planner_preset_call", "reallife_planner_preset_meet", "reallife_planner_preset_bill",
            "reallife_planner_preset_gym", "reallife_planner_preset_collect", "reallife_planner_preset_clean", "reallife_planner_preset_rest"
        };

        // The switches on the settings page: what each one shows and hides.
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

        private readonly PlannerDirector _director;
        private readonly List<PlannerRow> _rows = new List<PlannerRow>(32);
        private AppShell _shell;
        private PlannerTable _table;
        private FullMenu _menu;
        private int _tab;
        private Page _page;
        private EntryId _selected;

        // the note being written
        private int _draftId, _draftDay, _draftStart;
        private string _draftTitle, _draftOwn, _draftPlace, _draftStreet;
        private int _draftNumber;
        private ReminderLead _draftLead;

        public PlannerApp(PlannerDirector director)
        {
            _director = director;
        }

        // ---------------------------------------------------------------- building it from the game's parts

        /// <summary>The app's panel, asleep; null if the game's parts are not as expected (each has said so in Player.log).</summary>
        public GameObject Build(FullMenu menu, Transform dormant)
        {
            _menu = menu;
            SessionWatch.Mark("planner: the app being built from the game's MyEmployees");   // once per city: a few hundred objects copied in one frame
            _shell = AppShell.Make(menu, dormant, PlannerDirector.AppId);
            if (_shell == null || !PrepareRow(_shell.RowPrefab))
            {
                return null;
            }
            if (!_shell.SetColumns(new[] { "ColumnWhen", "ColumnWhat", "ColumnWhere", "ColumnStatus" },
                    new[] { "reallife_planner_col_when", "reallife_planner_col_what", "reallife_planner_col_where", "bizman_status" },
                    new[] { WhenWidth, WhatWidth, WhereWidth, StatusWidth }, 1))
            {
                return null;
            }
            PlannerCellView.SelectedColour = _shell.RowSelected;
            _table = _shell.List.gameObject.AddComponent<PlannerTable>();
            _table.scroller = _shell.Scroller;
            _table.prefab = _shell.RowPrefab.AddComponent<PlannerCellView>();
            _table.allowRowSelection = true;
            _table.onRowSelected.AddListener(row => UiClone.Safe(() => RowClicked(row)));

            UiClone.Key(_shell.TabLabels[0], "common_today");
            UiClone.Key(_shell.TabLabels[1], "reallife_planner_tab_upcoming");
            UiClone.Rewire(_shell.Tabs[0], () => ShowTab(0));
            UiClone.Rewire(_shell.Tabs[1], () => ShowTab(1));
            if (_shell.TopButtons != null)
            {
                InfoBox.Button(_shell.TopButtons, InfoBox.Positive, "reallife_planner_new_note", () => EditNote(null));
                InfoBox.Button(_shell.TopButtons, InfoBox.Additional, "bizman_menu_settings", () => Show(Page.Settings));
            }
            PlannerText.WantIcons();
            return _shell.Root;
        }

        private const float WhenWidth = 300f, WhatWidth = 600f, WhereWidth = 560f, StatusWidth = 300f;

        // The game's employee row has a tick box and ten cells. Four are kept and renamed: the name (the time), the
        // task with its warning icon (what, with the category's icon), the business (where) and the satisfaction,
        // which the game sets to the right (the status).
        private static bool PrepareRow(GameObject row)
        {
            Transform t = row.transform;
            var layout = row.GetComponent<HorizontalLayoutGroup>();
            Transform when = t.Find("EmployeeName"), what = t.Find("TaskInfo"), where = t.Find("BusinessName"), status = t.Find("Satisfaction");
            if (layout == null || when == null || what == null || where == null || status == null || what.Find("Task") == null)
            {
                UiClone.Fallback("the game's employee row is not built as expected, so the Planner is not added");
                return false;
            }
            UiClone.KeepOnly(t, "Background", "RaycastTarget", "EmployeeName", "TaskInfo", "BusinessName", "Satisfaction");
            when.name = "When";
            what.name = "What";
            where.name = "Where";
            status.name = "Status";
            AppShell.RowLayout(layout);
            AppShell.Width(when, WhenWidth, false);
            AppShell.Width(what, WhatWidth, true);
            AppShell.Width(where, WhereWidth, false);
            AppShell.Width(status, StatusWidth, false);
            AppShell.OneLine(when.GetComponent<TMP_Text>());
            AppShell.OneLine(where.GetComponent<TMP_Text>());
            AppShell.OneLine(status.GetComponent<TMP_Text>());
            AppShell.OneLine(what.Find("Task").GetComponent<TMP_Text>());
            Transform holder = what.Find("WarningIcon");
            var picture = holder != null ? holder.GetComponentInChildren<Image>(true) : null;
            if (picture != null)
            {
                // the game's warning sign hangs a little low and to the left of its cell; an icon sits in the middle of it
                var rect = (RectTransform)picture.transform;
                rect.anchoredPosition = new Vector2(0f, 0f);
                rect.sizeDelta = new Vector2(48f, 48f);
                picture.preserveAspect = true;
                picture.raycastTarget = false;
            }
            return true;
        }

        // ---------------------------------------------------------------- opening, reading, the table

        public void Opened()
        {
            if (_page != Page.Note && !(_page == Page.Entry && _selected.Source != null))
            {
                _page = Page.Glance;
            }
            Refresh();
            _director.Seen();
        }

        public void Closed()
        {
            if (_table != null && _page != Page.Note)
            {
                _selected = default(EntryId);
                _page = Page.Glance;
            }
        }

        /// <summary>Opens on one entry (a reminder was clicked).</summary>
        public void Select(EntryId id)
        {
            _selected = id;
            _page = Page.Entry;
            if (_shell != null && _shell.Root != null && _shell.Root.activeInHierarchy)
            {
                Refresh();
            }
        }

        private PlannerEntry Selected => _selected.Source != null ? _director.List.Find(_selected) : null;

        private void Refresh()
        {
            if (_shell == null || _table == null)
            {
                return;
            }
            float started = Time.realtimeSinceStartup;
            int today = TimeHelper.CurrentDay;
            _director.Look(today, today + UpcomingDays, true);
            PlannerEntry selected = Selected;
            if (_page == Page.Entry && selected == null)
            {
                _page = Page.Glance;   // what was open is no longer there (done elsewhere, cancelled, hidden)
            }
            if (selected != null && _page == Page.Entry)
            {
                _tab = selected.Day == today ? 0 : 1;
            }
            Rows(today);
            _shell.ShowTab(_tab);
            _table.Show(_rows, _page == Page.Entry && selected != null ? RowId(selected.Id) : null);
            Headline(today);
            Write();
            if (Meta.Dev.Verbose)
            {
                Logger?.Info("Planner: opened. " + _director.LookSummary() + "; " + _rows.Count + " rows; "
                             + ((Time.realtimeSinceStartup - started) * 1000f).ToString("0.0") + " ms.");
            }
        }

        private void ShowTab(int tab)
        {
            if (tab == _tab)
            {
                return;
            }
            _tab = tab;
            if (_page == Page.Entry)
            {
                _page = Page.Glance;
                _selected = default(EntryId);
            }
            int today = TimeHelper.CurrentDay;
            Rows(today);
            _shell.ShowTab(_tab);
            _table.Show(_rows, null);
            Write();
        }

        private static string RowId(EntryId id)
        {
            return id.Source + ":" + id.A + ":" + id.B;
        }

        private void Rows(int today)
        {
            _rows.Clear();
            List<PlannerEntry> items = _director.List.Items;
            int headed = -1;
            for (int i = 0; i < items.Count; i++)
            {
                PlannerEntry e = items[i];
                if (_tab == 0 ? e.Day != today : e.Day <= today)
                {
                    continue;
                }
                if (_tab == 1 && e.Day != headed)
                {
                    headed = e.Day;
                    _rows.Add(new PlannerRow { Id = "day:" + e.Day, Kind = RowKind.DayHeader, What = e.Day == today + 1 ? PlannerText.T("reallife_planner_day_tomorrow", "date", PlannerText.Date(e.Day)) : PlannerText.Date(e.Day) });
                }
                string status = PlannerText.StatusKey(e);
                _rows.Add(new PlannerRow
                {
                    Id = RowId(e.Id),
                    Kind = RowKind.Entry,
                    Entry = e,
                    When = PlannerText.When(e),
                    What = e.Title,
                    Where = e.Where,
                    Status = status != null ? PlannerText.T(status) : string.Empty,
                    Icon = PlannerText.Icon(e.Category),
                    Tone = e.Clash && e.State != PlannerState.Done || e.State == PlannerState.Missed ? 2 : e.State == PlannerState.Now || e.Next ? 1 : 0
                });
            }
            if (_rows.Count == 0)
            {
                _rows.Add(new PlannerRow { Id = "empty", Kind = RowKind.Info, What = PlannerText.T(_tab == 0 ? "reallife_planner_empty_today" : "reallife_planner_empty_upcoming") });
            }
        }

        // The line over the left column: what is next, in the game's big letters.
        private void Headline(int today)
        {
            PlannerEntry next = null;
            List<PlannerEntry> items = _director.List.Items;
            for (int i = 0; i < items.Count; i++)
            {
                if (items[i].Next)
                {
                    next = items[i];
                    break;
                }
            }
            string words = next == null ? PlannerText.T("reallife_planner_next_none")
                : PlannerText.T(next.State == PlannerState.Now ? "reallife_planner_next_now" : "reallife_planner_next_up", "title", next.Title,
                    "when", next.Day == today ? PlannerText.When(next) : PlannerText.Day(next.Day, today) + ", " + PlannerText.When(next));
            UiClone.Words(_shell.Header, words);
        }

        private void RowClicked(PlannerRow row)
        {
            if (row == null || row.Kind != RowKind.Entry || row.Entry == null)
            {
                _table.ClearSelection();
                return;
            }
            _selected = row.Entry.Id;
            Show(Page.Entry);
        }

        private void Show(Page page)
        {
            _page = page;
            if (page != Page.Entry)
            {
                _selected = default(EntryId);
                _table.ClearSelection();
            }
            Write();
        }

        // ---------------------------------------------------------------- the white box

        private void Write()
        {
            InfoBox box = _shell.Box;
            box.Clear();
            switch (_page)
            {
                case Page.Entry:
                    PlannerEntry entry = Selected;
                    if (entry != null)
                    {
                        WriteEntry(box, entry);
                        break;
                    }
                    _page = Page.Glance;
                    WriteGlance(box);
                    break;
                case Page.Note:
                    WriteNote(box);
                    break;
                case Page.Settings:
                    WriteSettings(box);
                    break;
                default:
                    WriteGlance(box);
                    break;
            }
            box.Done();
        }

        private static string Pair(string labelKey, string value)
        {
            return PlannerText.T(labelKey) + " <b>" + value + "</b>";
        }

        private void WriteGlance(InfoBox box)
        {
            int today = TimeHelper.CurrentDay;
            box.Title(PlannerText.Date(today));
            int toDo = 0, done = 0, missed = 0, week = 0;
            List<PlannerEntry> items = _director.List.Items;
            for (int i = 0; i < items.Count; i++)
            {
                PlannerEntry e = items[i];
                if (e.Day != today)
                {
                    week += e.Day > today ? 1 : 0;
                }
                else if (e.State == PlannerState.Done)
                {
                    done++;
                }
                else if (e.State == PlannerState.Missed)
                {
                    missed++;
                }
                else if (e.State != PlannerState.Over)
                {
                    toDo++;
                }
            }
            box.Line(Pair("reallife_planner_glance_todo", toDo.ToString()));
            box.Line(Pair("reallife_planner_glance_done", done.ToString()));
            if (missed > 0)
            {
                box.Line(Pair("reallife_planner_glance_missed", missed.ToString()));
            }
            box.Line(Pair("reallife_planner_glance_week", week.ToString()));
            box.Headline("reallife_planner_calendar");
            box.Line(Pair("reallife_planner_calendar_week", PlannerText.T("reallife_planner_calendar_week_value", "day", (PlannerTime.Weekday(today) + 1).ToString())));
            int perYear = SaveGameManager.Current != null && SaveGameManager.Current.gameVariables != null ? SaveGameManager.Current.gameVariables.daysPerYear : 0;
            if (perYear > 0)
            {
                int year = PlannerTime.Year(today, perYear);
                int next = PlannerTime.NextYearStart(today, perYear);
                box.Line(Pair("reallife_planner_calendar_year", PlannerText.T("reallife_planner_calendar_year_value", "year", (year + 1).ToString(), "days", perYear.ToString())));
                box.Line(Pair("reallife_planner_calendar_new_year", PlannerText.Date(next)));
            }
            box.Line(PlannerText.T("reallife_planner_glance_hint"));
        }

        private void WriteEntry(InfoBox box, PlannerEntry e)
        {
            int today = TimeHelper.CurrentDay;
            EntryId id = e.Id;
            box.Title(e.Title);
            box.Line(Pair("reallife_planner_detail_when", PlannerText.WhenLong(e, today)));
            if (!string.IsNullOrEmpty(e.Where))
            {
                box.Line(Pair("reallife_planner_detail_where", e.Where));
            }
            string status = PlannerText.StatusKey(e);
            if (status != null)
            {
                box.Line(Pair("reallife_planner_detail_status", PlannerText.T(status)));
            }
            if (e.Clash && e.State != PlannerState.Done)
            {
                string other = ClashWith(e);
                if (other != null)
                {
                    box.Line(PlannerText.T("reallife_planner_detail_clash", "title", other));
                }
            }
            if (!string.IsNullOrEmpty(e.What))
            {
                box.Line(e.What);
            }
            if (!string.IsNullOrEmpty(e.Money))
            {
                box.Line(e.Money);
            }
            if (!string.IsNullOrEmpty(e.IfMissed))
            {
                box.Line(e.IfMissed);
            }

            bool open = e.State == PlannerState.Upcoming || e.State == PlannerState.Now;
            int now = PlannerDirector.Now();
            if (open && Settings.PlannerReminders && box.HasSelector)
            {
                box.Headline("reallife_planner_reminder_head");
                ReminderLead set = PlannerStore.LeadOf(id);
                box.Selector(4, (int)set, i => PlannerText.T(PlannerText.LeadKey((ReminderLead)i)), i =>
                {
                    PlannerEntry live = _director.List.Find(id);
                    if (live == null)
                    {
                        return;
                    }
                    if (!PlannerStore.SetReminder(id, live.Day, live.Deadline && live.End >= 0 ? live.End : live.Start, (ReminderLead)i, PlannerDirector.Now()))
                    {
                        Notifications.ShowError("reallife_planner_reminder_too_late", null, false);
                        Write();   // the selector goes back to "no reminder"
                    }
                });
            }

            Transform row = box.ButtonRow();
            if (e.HasPlace || !string.IsNullOrEmpty(e.Street))
            {
                InfoBox.Button(row, InfoBox.Positive, "common_setdestination", () => SetDestination(id));
            }
            if (e.Own)
            {
                InfoBox.Button(row, InfoBox.Additional, e.Done ? "reallife_planner_note_not_done" : "reallife_planner_note_done", () => ToggleDone(id.A));
                Transform second = box.ButtonRow();
                InfoBox.Button(second, InfoBox.Additional, "reallife_planner_note_edit", () => EditNote(PlannerStore.Note(id.A)));
                InfoBox.Button(second, InfoBox.Negative, "filebrowser_delete", () => DeleteNote(id.A));
            }
            else if (!e.HasPlace && string.IsNullOrEmpty(e.Street))
            {
                InfoBox.Button(row, InfoBox.Additional, "common_close", () => Show(Page.Glance));
            }
        }

        private string ClashWith(PlannerEntry e)
        {
            List<PlannerEntry> items = _director.List.Items;
            for (int i = 0; i < items.Count; i++)
            {
                PlannerEntry o = items[i];
                if (o != e && o.Clash && o.Day == e.Day && o.Begins < e.Ends && e.Begins < o.Ends)
                {
                    return o.Title + ", " + PlannerText.When(o);
                }
            }
            return null;
        }

        // As the game's own "Set destination" buttons do (BizManBusiness.SetDestination): the destination guider is
        // pointed at the place, the game's notification says so, and the menu closes.
        private void SetDestination(EntryId id)
        {
            PlannerEntry e = _director.List.Find(id);
            if (e == null || SaveGameManager.Current == null || InstanceBehavior<GuidersManager>.Instance == null)
            {
                return;
            }
            string name;
            if (!string.IsNullOrEmpty(e.Street))
            {
                var address = new Address(e.Street, e.Number);
                name = address.ToFormattedString();
                SaveGameManager.Current.customDestination = address;
                GuidersManager.SetGuiderTarget(address, DirectionGuiderType.Destination);
            }
            else
            {
                name = !string.IsNullOrEmpty(e.Where) ? e.Where : e.Title;
                Sprite icon = PlannerText.Icon(e.Category) ?? GameSprites.Get("Icon-SetDestination");
                GuidersManager.SetGuiderTarget(new Vector3(e.X, e.Y, e.Z), name, icon, GuidersManager.GetGuiderColor(DirectionGuiderType.Destination), DirectionGuiderType.Destination);
            }
            Notifications.Show(NotificationType.Success, "notification_destination_set", new Dictionary<string, string> { { "address", name } });
            if (_menu != null)
            {
                _menu.Toggle(false);
            }
        }

        // ---------------------------------------------------------------- the player's own notes

        private void ToggleDone(int noteId)
        {
            PlannerNote note = PlannerStore.Note(noteId);
            if (note != null)
            {
                note.Done = !note.Done;
                if (note.Done)
                {
                    PlannerStore.SetReminder(new EntryId(NoteSource.SourceId, noteId), 0, 0, ReminderLead.None, 0);
                }
                Refresh();
            }
        }

        private void DeleteNote(int noteId)
        {
            // The game's own question box, with its red "negative" look.
            HudConfirm.Show("reallife_planner_delete_title", "reallife_planner_delete_body", () => UiClone.Safe(() =>
            {
                PlannerStore.RemoveNote(noteId);
                PlannerStore.SetReminder(new EntryId(NoteSource.SourceId, noteId), 0, 0, ReminderLead.None, 0);
                _selected = default(EntryId);
                _page = Page.Glance;
                Refresh();
            }), null, "filebrowser_delete", "common_cancel", true, true);
        }

        private void EditNote(PlannerNote note)
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
            _draftDay = note != null ? Mathf.Max(note.Day, today) : today;
            _draftStart = note != null ? note.Start : Mathf.Min(23, TimeHelper.CurrentHour + 1) * 60;
            _draftPlace = note != null ? note.Place : null;
            _draftStreet = note != null ? note.Street : null;
            _draftNumber = note != null ? note.Number : 0;
            _draftLead = note != null ? PlannerStore.LeadOf(new EntryId(NoteSource.SourceId, note.Id)) : PlannerStore.DefaultLead;
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
            int today = TimeHelper.CurrentDay;
            box.Title(PlannerText.T(_draftId == 0 ? "reallife_planner_new_note" : "reallife_planner_note_edit"));
            box.Line(Pair("reallife_planner_note_title", _draftTitle));
            // The title: one of the ready ones, or the player's own words typed into the game's text box.
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
                    uis.textInputUi.Show("reallife_planner_note_placeholder", "common_confirm", text => UiClone.Safe(() =>
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

            box.Headline("reallife_planner_note_when");
            box.Selector(NoteDaysAhead, Mathf.Clamp(_draftDay - today, 0, NoteDaysAhead - 1), i => PlannerText.Day(today + i, today), i => _draftDay = today + i);
            box.Selector(25, _draftStart < 0 ? 0 : _draftStart / 60 + 1, i => i == 0 ? PlannerText.T("reallife_planner_when_all_day") : PlannerText.Clock((i - 1) * 60),
                i => _draftStart = i == 0 ? -1 : (i - 1) * 60);

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

            if (Settings.PlannerReminders)
            {
                box.Headline("reallife_planner_reminder_head");
                box.Selector(4, (int)_draftLead, i => PlannerText.T(PlannerText.LeadKey((ReminderLead)i)), i => _draftLead = (ReminderLead)i);
            }
            Transform row = box.ButtonRow();
            InfoBox.Button(row, InfoBox.Positive, "menu_save", SaveNote);
            InfoBox.Button(row, InfoBox.Additional, "common_cancel", () => Show(Page.Glance));
        }

        // "Here" is the building the player is in (the game knows its address); in the street there is no address to keep.
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
            note.Title = PlannerStore.Clean(_draftTitle);
            note.Day = _draftDay;
            note.Start = _draftStart;
            note.Place = _draftPlace;
            note.Street = _draftPlace != null ? _draftStreet : null;
            note.Number = _draftPlace != null ? _draftNumber : 0;
            note.HasPlace = false;
            var id = new EntryId(NoteSource.SourceId, note.Id);
            if (Settings.PlannerReminders && !PlannerStore.SetReminder(id, note.Day, note.Start, _draftLead, PlannerDirector.Now()) && _draftLead != ReminderLead.None)
            {
                Notifications.Show(NotificationType.Warning, "reallife_planner_reminder_too_late", null, 6f, null, null, true, false);
            }
            _selected = id;
            _page = Page.Entry;
            Refresh();
            _director.Seen();
        }

        // ---------------------------------------------------------------- settings

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
                box.Headline("reallife_planner_settings_lead");
                box.Selector(3, (int)PlannerStore.DefaultLead - 1, i => PlannerText.T(PlannerText.LeadKey((ReminderLead)(i + 1))), i => PlannerStore.DefaultLead = (ReminderLead)(i + 1));
            }
            Transform row = box.ButtonRow();
            InfoBox.Button(row, InfoBox.Additional, "common_close", () => Show(Page.Glance));
        }
    }
}
