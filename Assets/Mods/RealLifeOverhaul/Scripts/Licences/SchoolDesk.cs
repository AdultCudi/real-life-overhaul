using System;
using System.Text;
using BAModAPI;
using Extensions;
using Helpers;
using RealLifeOverhaul.GameUi;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>
    /// A driving-school desk in the game's own interface. Standing at the sign the game's panel over a place
    /// (<see cref="WorldPanel"/> through <see cref="DoorPanel"/>) says whether the school is open and offers
    /// "Speak to the instructor" on the interact key. That opens the school's list (<see cref="TravelList"/>): an entry
    /// for your licence card, then one for the theory test, each lesson and test, the motorcycle assessment, the taxi
    /// endorsement and renewal, each with the fee and what is needed under its name, and its button off, with the reason,
    /// when it cannot be had now (closed hours, the fee, a test still to pass first). The licence card is a second page of
    /// the same list: a note for each licence, one for the penalty points and one for a suspension, with "Back". A card is
    /// a list of short facts, each with a name, so it suits the list better than a box of running text.
    ///
    /// What the school answers is a notification. Renewals, the taxi endorsement and the motorcycle assessment are done at
    /// once and the list stays up with the new figures; a test or a lesson is paid for, the list closes, and it starts
    /// when the director's step says so, as it did after the conversation. The phone's licensing office is still the
    /// game's dialogue (<c>DrivingSchoolDialog</c>), and so is the desk when the game's window cannot be copied.
    ///
    /// The cost: the panel is asked for once a second by the director and written only when the hour or the place
    /// changes; the list is filled when it opens and after something is bought. Nothing runs per frame.
    /// </summary>
    internal sealed class SchoolDesk
    {
        private const int RowCard = -1, RowBack = -2;
        private const NavigationBlocker Blocker = (NavigationBlocker)7302;   // a value of our own, so nobody else's is ever lifted
        private const string ButtonDo = "reallife_lic_list_do";
        private const string ButtonOpen = "reallife_lic_list_open";

        private static readonly string[] NameKeys =
        {
            "reallife_school_name_card", "reallife_school_name_theory", "reallife_school_name_lesson_car", "reallife_school_name_test_car",
            "reallife_school_name_lesson_van", "reallife_school_name_test_van", "reallife_school_name_taxi", "reallife_school_name_renew", "reallife_school_name_motorcycle"
        };

        private readonly LicenceDirector _director;
        private readonly SchoolScreens _screens;
        private readonly DoorPanel _door;
        private readonly Action _onSpeak;
        private readonly Func<string> _what;
        private readonly Action<int> _onPick;
        private readonly Action _onClosed;
        private readonly StringBuilder _text = new StringBuilder(200);
        private PlayerController _player;
        private bool _held;
        private string _closing;
        private int _doorKey = -1;
        private string _at;

        public SchoolDesk(LicenceDirector director, SchoolScreens screens, IModLogger logger)
        {
            _director = director;
            _screens = screens;
            _door = new DoorPanel(logger, "Licences check (desk panel)");
            _onSpeak = _door.Press(Speak);
            _what = () => _at ?? "a driving school desk";
            _onPick = Picked;
            _onClosed = Closed;
        }

        /// <summary>True while the game's panel is up at a desk, in the frame its button ran, and while the school's list is open.</summary>
        public bool TakesKey => _door.TakesKey || IsOpen;

        /// <summary>True while the school's list is the one on the window.</summary>
        public bool IsOpen => _screens.Window.OpenFor(this);

        // ---------------------------------------------------------------- the panel at the desk

        /// <summary>Once a second: the panel over the desk the player stands at (<paramref name="at"/> is its address), or down.</summary>
        public void Offer(bool on, Vector3 front, Vector3 player, string at)
        {
            if (!on)
            {
                _door.Hide(IsOpen ? "the school's list is open" : "the player walked away or another screen is open");
                _doorKey = -1;
                return;
            }
            _at = at;
            bool open = TimeHelper.CurrentHour >= DrivingSchool.OpensAt && TimeHelper.CurrentHour < DrivingSchool.ClosesAt;
            int key = open ? 1 : 0;
            if (key != _doorKey)
            {
                _doorKey = key;
                WorldPanel panel = _door.Panel;
                panel.Lines(Licences.T("reallife_school_door_title"), Licences.T("reallife_school_door_hours", "from", Hour(DrivingSchool.OpensAt), "to", Hour(DrivingSchool.ClosesAt)),
                    Licences.T(open ? "reallife_school_door_open" : "reallife_school_door_closed"), open ? WorldPanel.Green() : WorldPanel.Red());
                panel.Buttons("reallife_school_button_speak", _onSpeak, true, null, null, false);
                panel.Button(2, null, null, false, false);
                panel.Button(3, null, null, false, false);
            }
            _door.Show(front, player, 1.9f, _what);
        }

        public void HidePanel(string why)
        {
            _door.Hide(why);
            _doorKey = -1;
        }

        private static string Hour(int hour)
        {
            return (hour % 24).GetFormattedTime(0f);
        }

        private void Speak()
        {
            try
            {
                _director.OpenDesk();
            }
            catch (Exception e)
            {
                Licences.Logger?.Error(e);
            }
        }

        // ---------------------------------------------------------------- the list

        /// <summary>Opens the school's list. False if the game's window cannot be made (the caller then speaks through the dialogue panel).</summary>
        public bool Open()
        {
            float began = Time.realtimeSinceStartup;
            TravelList list = _screens.Window.Take(this);
            if (list == null)
            {
                Licences.Logger?.Info("Licences check (desk list): the game's list could not be made; the desk speaks through the dialogue panel: FAIL.");
                return false;
            }
            _closing = null;
            PlayerController player = PlayerHelper.PlayerController;
            if (player != null && !_held)
            {
                _player = player;
                _held = true;
                player.ResetNavigation();
                player.SetNavigationBlocker(Blocker);
            }
            int entries = Fill(list);
            list.Open(_onPick, _onClosed);
            if (Meta.Dev.Verbose)
            {
                Licences.Logger?.Info("Licences check (desk list): the game's list is open: " + entries + " entries, " + list.Made + " entries made so far; "
                                      + ((Time.realtimeSinceStartup - began) * 1000f).ToString("F1") + " ms: PASS.");
            }
            return true;
        }

        /// <summary>The licence card on its own page (the phone and the test button use it). False if the game's window cannot be made.</summary>
        public bool ShowCard()
        {
            TravelList list = _screens.Window.Take(this);
            if (list == null)
            {
                return false;
            }
            _closing = null;
            FillCard(list);
            list.Open(_onPick, _onClosed);
            return true;
        }

        public void Close(string why)
        {
            if (IsOpen)
            {
                _closing = why;
                _screens.Window.Close();
            }
            else
            {
                Release();
            }
        }

        public void Dispose()
        {
            Close("the director went with the city");
            _door.Hide("the director went with the city");
            _screens.Window.Destroy(e => Licences.Logger?.Error(e));
        }

        private void Closed()
        {
            if (Meta.Dev.Verbose)
            {
                Licences.Logger?.Info("Licences check (desk list): closed (" + (_closing ?? "cancelled by the player, or another screen took the window") + ").");
            }
            _closing = null;
            Release();
        }

        private void Release()
        {
            if (_held)
            {
                _held = false;
                try
                {
                    if (_player != null)
                    {
                        _player.UnsetNavigationBlocker(Blocker);
                    }
                }
                catch (Exception e)
                {
                    Licences.Logger?.Error(e);
                }
            }
        }

        // The menu: what each service costs and needs, and why one cannot be had now.
        private int Fill(TravelList list)
        {
            list.Begin(Licences.T("reallife_school_door_title"), Licences.T("reallife_school_list_info", "from", Hour(DrivingSchool.OpensAt), "to", Hour(DrivingSchool.ClosesAt)));
            list.Add(RowCard, Licences.T(NameKeys[0]), Licences.T("reallife_school_list_card"), ButtonOpen, true);
            int count = 1;
            SchoolService[] order = DrivingSchool.Order;
            for (int i = 0; i < order.Length; i++)
            {
                SchoolService service = order[i];
                if (service == SchoolService.Card)
                {
                    continue;
                }
                bool available = DrivingSchool.Available(service, true, out string reason);
                _text.Length = 0;
                _text.Append(Licences.T(DrivingSchool.PitchKey(service)).Replace("{amount}", Licences.Money(DrivingSchool.Fee(service)))
                    .Replace("{status}", available ? Licences.T("reallife_school_ready") : Licences.T(reason)));
                string detail = _text.ToString();
                list.Add((int)service, Licences.T(NameKeys[(int)service]), detail, ButtonDo, available, list.HeightFor(detail.Length, true));
                count++;
            }
            return count;
        }

        // The licence card: a note for each licence, one for the points, one for a suspension.
        private void FillCard(TravelList list)
        {
            int day = TimeHelper.CurrentDay;
            list.Begin(Licences.T("reallife_lic_card_title"), Licences.T("reallife_lic_card_today", "day", day.ToString()));
            list.Add(RowBack, Licences.T("reallife_school_list_back"), Licences.T("reallife_school_list_back_detail"), ButtonOpen, true);
            for (int i = 0; i < DrivingSchool.Classes; i++)
            {
                var kind = (LicenceClass)i;
                LicenceState.Card card = LicenceState.Of(kind);
                string detail = !card.Held ? Licences.StandingName(LicenceStanding.None)
                    : Licences.T("reallife_lic_card_line", "standing", Licences.StandingName(LicenceState.Standing(kind, day)), "issued", card.Issued.ToString()).Replace("{expires}", card.Expires.ToString());
                list.AddNote(Licences.ClassName(kind), detail, 0f);
            }
            _text.Length = 0;
            _text.Append(Licences.T("reallife_lic_card_points", "points", LicenceState.PointsOn(day).ToString(), "limit", LicenceLaw.PointsLimit.ToString()));
            var entries = LicenceState.Entries;
            for (int i = 0; i < entries.Count; i++)
            {
                if (day - entries[i].Day < LicenceLaw.PointsExpireDays)
                {
                    _text.Append('\n').Append(entries[i].Points).Append("  ").Append(Licences.T(DrivingSchool.PointName(entries[i].Code)))
                        .Append("  (").Append(Licences.T("reallife_lic_card_until", "day", (entries[i].Day + LicenceLaw.PointsExpireDays).ToString())).Append(')');
                }
            }
            string points = _text.ToString();
            list.AddNote(Licences.T("reallife_school_list_points"), points, list.HeightFor(points.Length + 30 * (entries.Count + 1), false));
            if (LicenceState.SuspendedUntil > day)
            {
                list.AddNote(Licences.T("reallife_school_list_suspended"), Licences.T("reallife_lic_card_suspended", "day", LicenceState.SuspendedUntil.ToString()), 0f);
            }
        }

        private void Picked(int id)
        {
            try
            {
                TravelList list = _screens.Window.Take(this);
                if (list == null || !list.IsOpen)
                {
                    return;
                }
                if (id == RowCard)
                {
                    FillCard(list);
                    list.Open(_onPick, _onClosed);
                    Step("licence card");
                    return;
                }
                if (id == RowBack)
                {
                    Fill(list);
                    list.Open(_onPick, _onClosed);
                    Step("back to the services");
                    return;
                }
                var service = (SchoolService)id;
                // Asked again now: the hour may have turned or the money gone since the list was written.
                if (!DrivingSchool.Available(service, true, out string reason))
                {
                    Licences.Toast(NotificationType.Warning, reason, 7f);
                    Fill(list);
                    list.Open(_onPick, _onClosed);
                    Step(service + " refused: " + reason);
                    return;
                }
                string done = _director.Take(service);
                bool failed = done == null || done == "reallife_school_no_money";
                Licences.Toast(failed ? NotificationType.Warning : NotificationType.Success, done ?? "reallife_school_not_now", 9f);
                Step(service + (failed ? " not taken (" + done + ")" : " taken"));
                if (!failed && (service == SchoolService.Renew || service == SchoolService.Taxi || service == SchoolService.Motorcycle))
                {
                    Fill(list);
                    list.Open(_onPick, _onClosed);   // the figures have changed
                }
                else if (!failed)
                {
                    Close("a test or a lesson was booked");
                }
            }
            catch (Exception e)
            {
                Licences.Logger?.Error(e);
                Close("an error");
            }
        }

        private static void Step(string what)
        {
            if (Meta.Dev.Verbose)
            {
                Licences.Logger?.Info("Licences check (choice): the desk: " + what + ".");
            }
        }
    }
}
