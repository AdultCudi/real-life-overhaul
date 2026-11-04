using System;
using System.Collections.Generic;
using Helpers;
using RealLifeOverhaul.GameUi;
using RealLifeOverhaul.Planner;
using Streets;
using UI;
using UI.Guiders;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// The Planner on the kit: its skin over the game. Everything the screen does is <see cref="PlannerFlow"/>'s (game-free, so the
    /// checks press every target); this lays its model out and gives it the game's clock, entries, words, text box, question box and
    /// guider. The old page stays underneath and is what shows when this screen's switch is off. Cost: a look and a layout when the
    /// day, the tab, the page or a note changes.
    /// </summary>
    public sealed class PlannerScreen : IUiScreen
    {
        private readonly PlannerFlow _flow;

        public PlannerScreen(PlannerDirector director)
        {
            _flow = new PlannerFlow(new Host(director), UiText.Words);
        }

        public string Id => "planner";

        public bool Dirty => _flow.Dirty;

        public void Lay(UiScene s, UiFrame frame, bool dark, IUiKeys keys)
        {
            if (_flow.Dirty)
            {
                _flow.Build();
            }
            LifePages.Planner(s, _flow.Model, frame, dark, keys);
        }

        public bool Press(int command, int arg) => _flow.Press(command, arg);

        public void Focused(int command, int arg)
        {
        }

        public bool Back() => _flow.Back();

        public void Tab(int step) => _flow.Tab(step);

        public bool Act(UiAction action) => _flow.Act(action);

        public void Reveal(int list, int index) => _flow.Reveal(list, index);

        public void Wheel(int clip, float notches) => _flow.Wheel(clip, notches);

        /// <summary>The game as the Planner's flow asks for it.</summary>
        private sealed class Host : IPlannerHost
        {
            private readonly PlannerDirector _director;

            public Host(PlannerDirector director)
            {
                _director = director;
            }

            public int Today => TimeHelper.CurrentDay;

            public int Minute => TimeHelper.CurrentHour * 60 + Mathf.FloorToInt(TimeHelper.CurrentMinute);

            public int NowMinutes => PlannerDirector.Now();

            public bool Reminders => Settings.PlannerReminders;

            public bool ByCar => PlannerGame.ByCar();

            public void Look(int fromDay, int toDay, List<PlannerEntry> into)
            {
                if (_director == null)
                {
                    return;
                }
                _director.Look(fromDay, toDay, true);
                into.AddRange(_director.List.Items);
            }

            public string Clock(int minuteOfDay) => PlannerText.Clock(minuteOfDay);

            public string Weekday(int day) => PlannerText.Weekday(day);

            public string DayWord(int day, int today) => PlannerText.Day(day, today);

            public string Span(int minutes) => PlannerText.Span(minutes);

            public string Money(float amount) => PlannerText.Money(amount);

            public void AskTitle(string initial, Action<string> done)
            {
                TextBox.Ask("reallife_planner_note_placeholder", "common_confirm", text => done(text), null, initial ?? string.Empty, false, PlannerStore.TitleLength);
            }

            public void ConfirmDelete(Action yes)
            {
                GameConfirm.Show("reallife_planner_delete_title", "reallife_planner_delete_body", () => UiClone.Safe(yes), null, "filebrowser_delete", "common_cancel", true, true);
            }

            // as the game's BizManBusiness.SetDestination: the guider, the notification, the menu put away
            public void Locate(PlannerEntry e)
            {
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
                UiShell.CloseMenu();
            }

            public bool MetresTo(PlannerEntry entry, out float metres) => PlannerGame.MetresTo(entry, out metres);

            // the building the player is in; the street has no address to keep
            public bool HerePlace(out string street, out int number, out string name)
            {
                street = name = null;
                number = 0;
                BuildingRegistration here = BuildingManager.IsInsideBuilding && InstanceBehavior<BuildingManager>.Instance != null
                    ? InstanceBehavior<BuildingManager>.Instance.buildingRegistration : null;
                if (here == null || here.Address == null || string.IsNullOrEmpty(here.Address.streetName))
                {
                    return false;
                }
                street = here.Address.streetName;
                number = here.Address.streetNumber;
                name = here.Address.ToFormattedString();
                return true;
            }

            public void Notice(string key)
            {
                Notifications.ShowError(key, null, false);
            }

            public void Looked()
            {
                _director?.Seen();
            }
        }
    }
}
