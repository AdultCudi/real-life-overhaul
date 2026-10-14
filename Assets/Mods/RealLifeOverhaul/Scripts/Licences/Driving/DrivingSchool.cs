using System;
using System.Collections.Generic;
using System.Text;
using BAModAPI;
using Dialogs;
using Entities;
using Localizor;
using UI.Smartphone.Apps.Contacts;
using Streets;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>What the driving school (and the licensing office on the phone) offers.</summary>
    internal enum SchoolService
    {
        Card,          // read the licence card out
        Theory,
        LessonCar,
        TestCar,
        LessonVan,
        TestVan,
        Taxi,
        Renew,
        Motorcycle     // the ride assessment, recorded at the desk
    }

    /// <summary>
    /// The driving school's rules: what each service costs, when it can be had and why not.
    /// The same list is read out at the school's desk and by the licensing office on the phone;
    /// tests and lessons can only be started at a desk.
    /// </summary>
    internal static class DrivingSchool
    {
        public const int OpensAt = 7;
        public const int ClosesAt = 20;
        public const int Classes = 4;       // LicenceClass: car, commercial, taxi, motorcycle

        public static readonly SchoolService[] Order =
        {
            SchoolService.Card, SchoolService.Theory, SchoolService.LessonCar, SchoolService.TestCar,
            SchoolService.LessonVan, SchoolService.TestVan, SchoolService.Motorcycle, SchoolService.Taxi, SchoolService.Renew
        };

        public static int Fee(SchoolService service)
        {
            switch (service)
            {
                case SchoolService.Theory: return LicenceLaw.TheoryFee;
                case SchoolService.LessonCar:
                case SchoolService.LessonVan: return LicenceLaw.LessonFee;
                case SchoolService.TestCar: return LicenceState.RetestDue[0] ? LicenceLaw.RetestFee : LicenceLaw.CarTestFee;
                case SchoolService.TestVan: return LicenceState.RetestDue[1] ? LicenceLaw.RetestFee : LicenceLaw.CommercialTestFee;
                case SchoolService.Taxi: return LicenceLaw.TaxiFee;
                case SchoolService.Motorcycle: return LicenceLaw.MotorcycleFee;
                case SchoolService.Renew: return LicenceLaw.RenewalFee * Mathf.Max(1, Renewable(TimeHelper.CurrentDay));
                default: return 0;
            }
        }

        /// <summary>How many of the licences (car, commercial, taxi, motorcycle) could be renewed today.</summary>
        public static int Renewable(int day)
        {
            int count = 0;
            for (int i = 0; i < Classes; i++)
            {
                count += LicenceState.Renewable((LicenceClass)i, day) ? 1 : 0;
            }
            return count;
        }

        public static bool TheoryInDate(int day)
        {
            return LicenceState.TheoryPassedDay[0] > 0 && day - LicenceState.TheoryPassedDay[0] <= LicenceLaw.TheoryValidDays;
        }

        /// <summary>Whether a service can be had right now; if not, a line that says why.</summary>
        public static bool Available(SchoolService service, bool inPerson, out string reasonKey)
        {
            reasonKey = null;
            int day = TimeHelper.CurrentDay;
            LicenceStanding car = LicenceState.Standing(LicenceClass.Car, day);
            LicenceStanding van = LicenceState.Standing(LicenceClass.Commercial, day);
            LicenceStanding bike = LicenceState.Standing(LicenceClass.Motorcycle, day);
            bool needsDesk = service != SchoolService.Card && service != SchoolService.Renew && service != SchoolService.Taxi;
            if (service == SchoolService.Card)
            {
                return true;
            }
            if (needsDesk && !Settings.DrivingSchool)
            {
                reasonKey = "reallife_school_closed";
            }
            else if (needsDesk && !inPerson)
            {
                reasonKey = "reallife_school_no_in_person";
            }
            else if (needsDesk && (TimeHelper.CurrentHour < OpensAt || TimeHelper.CurrentHour >= ClosesAt))
            {
                reasonKey = "reallife_school_no_hours";
            }
            else if (needsDesk && LicenceState.SuspendedUntil > day)
            {
                reasonKey = "reallife_school_no_suspended";
            }
            else
            {
                switch (service)
                {
                    case SchoolService.Theory:
                        reasonKey = Holds(car) && Holds(bike) ? "reallife_school_no_have"   // (the paper is for whichever of the car and the motorbike is still to get)
                            : TheoryInDate(day) ? "reallife_school_no_theory_passed"
                            : day < LicenceState.TheoryRetryDay ? "reallife_school_no_wait"
                            : null;
                        break;
                    case SchoolService.LessonCar:
                        reasonKey = car == LicenceStanding.Valid ? null : car == LicenceStanding.Expired ? "reallife_school_no_have"
                            : !TheoryInDate(day) ? "reallife_school_no_need_theory" : null;
                        break;
                    case SchoolService.TestCar:
                        reasonKey = car == LicenceStanding.Valid || car == LicenceStanding.Expired ? "reallife_school_no_have"
                            : !TheoryInDate(day) ? "reallife_school_no_need_theory"
                            : LicenceState.Lessons[0] < 1 ? "reallife_school_no_need_lesson"
                            : day < LicenceState.RetestDay[0] ? "reallife_school_no_wait"
                            : null;
                        break;
                    case SchoolService.LessonVan:
                        reasonKey = car != LicenceStanding.Valid ? "reallife_school_no_need_car" : null;
                        break;
                    case SchoolService.TestVan:
                        reasonKey = van == LicenceStanding.Valid || van == LicenceStanding.Expired ? "reallife_school_no_have"
                            : car != LicenceStanding.Valid ? "reallife_school_no_need_car"
                            : LicenceState.Lessons[1] < 1 ? "reallife_school_no_need_lesson"
                            : day < LicenceState.RetestDay[1] ? "reallife_school_no_wait"
                            : null;
                        break;
                    case SchoolService.Taxi:
                        reasonKey = LicenceState.Standing(LicenceClass.Taxi, day) == LicenceStanding.Valid ? "reallife_school_no_have"
                            : car != LicenceStanding.Valid ? "reallife_school_no_need_car"
                            : day - LicenceState.Of(LicenceClass.Car).Issued < LicenceLaw.TaxiHeldDays ? "reallife_school_no_taxi_held"
                            : LicenceState.PointsOn(day) > LicenceLaw.TaxiMostPoints ? "reallife_school_no_taxi_points"
                            : null;
                        break;
                    case SchoolService.Renew:
                        reasonKey = Renewable(day) == 0 ? "reallife_school_no_renew" : null;
                        break;
                    case SchoolService.Motorcycle:
                        reasonKey = Holds(bike) ? "reallife_school_no_have"
                            : !TheoryInDate(day) ? "reallife_school_no_need_theory"
                            : null;
                        break;
                }
            }
            if (reasonKey == null && !Licences.Affords(Fee(service)))
            {
                reasonKey = "reallife_school_no_money";
            }
            return reasonKey == null;
        }

        private static bool Holds(LicenceStanding standing)
        {
            return standing == LicenceStanding.Valid || standing == LicenceStanding.Expired;
        }

        public static string PitchKey(SchoolService service)
        {
            switch (service)
            {
                case SchoolService.Motorcycle: return "reallife_school_pitch_motorcycle";
                case SchoolService.Theory: return "reallife_school_pitch_theory";
                case SchoolService.LessonCar: return "reallife_school_pitch_lesson_car";
                case SchoolService.TestCar: return "reallife_school_pitch_test_car";
                case SchoolService.LessonVan: return "reallife_school_pitch_lesson_van";
                case SchoolService.TestVan: return "reallife_school_pitch_test_van";
                case SchoolService.Taxi: return "reallife_school_pitch_taxi";
                case SchoolService.Renew: return "reallife_school_pitch_renew";
                default: return "reallife_school_pitch_card";
            }
        }

        /// <summary>The licence card as text: each licence with its dates, the points, a suspension. Built when asked for.</summary>
        public static string CardText()
        {
            int day = TimeHelper.CurrentDay;
            var text = new StringBuilder(256);
            for (int i = 0; i < Classes; i++)
            {
                var kind = (LicenceClass)i;
                LicenceState.Card card = LicenceState.Of(kind);
                LicenceStanding standing = LicenceState.Standing(kind, day);
                text.Append(Licences.ClassName(kind)).Append(": ");
                if (!card.Held)
                {
                    text.Append(Licences.StandingName(LicenceStanding.None));
                }
                else
                {
                    text.Append(Licences.T("reallife_lic_card_line", "standing", Licences.StandingName(standing), "issued", card.Issued.ToString())
                        .Replace("{expires}", card.Expires.ToString()));
                }
                text.Append('\n');
            }
            int points = LicenceState.PointsOn(day);
            text.Append(Licences.T("reallife_lic_card_points", "points", points.ToString(), "limit", LicenceLaw.PointsLimit.ToString()));
            IReadOnlyList<LicenceState.Entry> entries = LicenceState.Entries;
            for (int i = 0; i < entries.Count; i++)
            {
                if (day - entries[i].Day < LicenceLaw.PointsExpireDays)
                {
                    text.Append('\n').Append("  ").Append(entries[i].Points).Append("  ").Append(Licences.T(PointName(entries[i].Code)))
                        .Append("  (").Append(Licences.T("reallife_lic_card_until", "day", (entries[i].Day + LicenceLaw.PointsExpireDays).ToString())).Append(')');
                }
            }
            if (LicenceState.SuspendedUntil > day)
            {
                text.Append('\n').Append(Licences.T("reallife_lic_card_suspended", "day", LicenceState.SuspendedUntil.ToString()));
            }
            text.Append('\n').Append(Licences.T("reallife_lic_card_today", "day", day.ToString()));
            return text.ToString();
        }

        public static string PointName(PointCode code)
        {
            switch (code)
            {
                case PointCode.RedLight: return "reallife_lic_point_redlight";
                case PointCode.Speeding: return "reallife_lic_point_speeding";
                case PointCode.Reckless: return "reallife_lic_point_reckless";
                case PointCode.WrongSide: return "reallife_lic_point_wrongside";
                case PointCode.FleeingStop: return "reallife_lic_point_fleeing";
                case PointCode.HitAndRun: return "reallife_lic_point_hitrun";
                case PointCode.InjuredPerson: return "reallife_lic_point_injured";
                case PointCode.KilledPerson: return "reallife_lic_point_killed";
                case PointCode.EvadedCheck: return "reallife_lic_point_evaded";
                case PointCode.DrunkDriving: return "reallife_lic_point_drunk";
                default: return "reallife_lic_point_nolicence";
            }
        }
    }

    /// <summary>
    /// Where the driving school can be walked up to: a learner-plate sandwich board on the
    /// pavement beside the door of each of the city's school buildings (three at most), found
    /// once per city load, a few buildings a second. Standing at one and pressing the
    /// interaction key opens the school's list. A board is one object with shared meshes and no
    /// collider or script. Nothing here runs unless the director calls it, once a second.
    /// </summary>
    internal sealed class LicenceDesks
    {
        private const int Most = 3;
        private const int PerTick = 30;
        private const float Aside = 4.3f;        // metres along the wall from the door (a help-wanted board may stand at 2.4)
        private const float FromWall = 0.55f;
        private const float Reach = 1.7f;
        private const string SchoolType = "ba:businesstype_school";

        private readonly Transform _parent;
        private readonly IModLogger _logger;
        private readonly List<Transform> _signs = new List<Transform>(Most);
        private readonly List<Vector3> _fronts = new List<Vector3>(Most);
        private readonly List<string> _addresses = new List<string>(Most);
        private readonly List<CityBuildingController> _buildings = new List<CityBuildingController>(Most);
        private int _next;
        private bool _searched;
        private bool _failed;

        public LicenceDesks(Transform parent, IModLogger logger)
        {
            _parent = parent;
            _logger = logger;
        }

        /// <summary>The desk the player stands at, or -1.</summary>
        public int At { get; private set; } = -1;

        public int Count => _fronts.Count;
        public bool Searched => _searched;

        public Vector3 Front(int index) => _fronts[index];

        /// <summary>The address of the school a desk stands at, for the city map (World/MapLinks).</summary>
        public string Address(int index) => _addresses[index];

        /// <summary>The school a desk stands at (it lights up on the city map while its row is ticked; its card stays the game's).</summary>
        public CityBuildingController Building(int index) => _buildings[index];

        public void Away()
        {
            At = -1;
        }

        /// <summary>The nearest desk to a point, for directions. False if none was found (yet).</summary>
        public bool Nearest(Vector3 from, out Vector3 front, out string address)
        {
            front = default;
            address = null;
            float best = float.MaxValue;
            for (int i = 0; i < _fronts.Count; i++)
            {
                float sqr = (_fronts[i] - from).sqrMagnitude;
                if (sqr < best)
                {
                    best = sqr;
                    front = _fronts[i];
                    address = _addresses[i];
                }
            }
            return address != null;
        }

        /// <summary>Once a second while the player is on foot out of doors.</summary>
        public void Tick(Vector3 player)
        {
            if (_failed)
            {
                At = -1;
                return;
            }
            try
            {
                if (!_searched)
                {
                    Search();
                }
                int at = -1;
                for (int i = 0; i < _fronts.Count && at < 0; i++)
                {
                    Vector3 offset = _fronts[i] - player;
                    if (offset.y > -2f && offset.y < 2f && offset.x * offset.x + offset.z * offset.z < Reach * Reach)
                    {
                        at = i;
                    }
                }
                At = at;
            }
            catch (Exception e)
            {
                _failed = true;
                At = -1;
                _logger?.Error(e);
                _logger?.Warn("Licences: the driving school's desks are off for this session after the error above; the licensing office on the phone still answers.");
            }
        }

        public void Dispose()
        {
            for (int i = 0; i < _signs.Count; i++)
            {
                if (_signs[i] != null)
                {
                    UnityEngine.Object.Destroy(_signs[i].gameObject);
                }
            }
            _signs.Clear();
            _fronts.Clear();
            _addresses.Clear();
            _buildings.Clear();
            At = -1;
        }

        private void Search()
        {
            CityManager city = InstanceBehavior<CityManager>.Instance;
            CityBuildingController[] buildings = city != null ? city.cityBuildingControllers : null;
            if (buildings == null || !Shapes.Available)
            {
                return;
            }
            for (int looked = 0; looked < PerTick && _next < buildings.Length && _fronts.Count < Most; looked++, _next++)
            {
                CityBuildingController building = buildings[_next];
                if (building == null || building.building == null || building.building.SpecialService == null
                    || building.building.SpecialService.businessTypeName != SchoolType || GameMembers.Fenced(building.building))
                {
                    continue;
                }
                Place(building);
            }
            if (_next >= buildings.Length || _fronts.Count >= Most)
            {
                _searched = true;
                _logger?.Info("Licences: the driving school has " + _fronts.Count + " desk(s): " + string.Join("; ", _addresses) + ".");
            }
        }

        // On the pavement a few steps to one side of the school's door, a step out from the wall, where nothing else stands.
        private void Place(CityBuildingController building)
        {
            if (!JobSpots.Front(building, out Vector3 front) || !CitySpots.Along(front, out Vector3 along))
            {
                return;
            }
            for (int side = -1; side <= 1; side += 2)
            {
                if (!CitySpots.Shopfront(front + along * (side * Aside), FromWall + 0.45f, 0.6f, out Vector3 stand, out Vector3 outward))
                {
                    continue;
                }
                Vector3 at = stand - outward * 0.45f;
                if (!CitySpots.Ground(at, out float ground) || Mathf.Abs(ground - stand.y) > 0.25f || JobSpots.Flat(at, front) < 2.4f)
                {
                    continue;
                }
                at.y = ground;
                Transform sign = LicenceProps.Make(LicenceProps.SchoolSign, _parent);
                if (sign == null)
                {
                    return;
                }
                sign.SetPositionAndRotation(at, Quaternion.LookRotation(outward, Vector3.up));
                _signs.Add(sign);
                _fronts.Add(stand + outward * 0.6f);
                _addresses.Add(building.building.Address.ToFormattedString());
                _buildings.Add(building);
                return;
            }
        }
    }

    /// <summary>
    /// The licensing office as a phone contact: the licence card read out, renewals and the
    /// taxi endorsement over the phone, and directions to the driving school for everything that
    /// has to be done in person. Registered the way the mod's other contacts are.
    /// </summary>
    internal static class LicencePhone
    {
        public const string ContactId = "reallife-dmv";
        private const string DialogTypeName = "reallife_dmv_calldialogtype";

        public static void Setup()
        {
            if (!Settings.PhoneContacts || !Settings.DrivingLicence)
            {
                return;
            }
            try
            {
                ContactPictures.Add(ContactId);   // the game's own logo shapes or portrait camera (Phone/Contacts)
                Contact contact = Contact.GetContact(ContactId, ContactCategoryName.General, "reallife_contact_dmv_desc", null, false, true);
                var dialogType = (CallDialogType)ModEnumHash.GetSafeHash(DialogTypeName);
                contact.callDialogTypeOverride = dialogType;   // not saved by the game, so set on every load
                CallDialogFactory.RegisterDialog(dialogType, Guard.Maker(() => new DrivingSchoolDialog()));
                if (contact.messagesQueue == null || contact.messagesQueue.Count == 0)
                {
                    contact.SendMessage(new TextMessage("reallife_text_dmv_welcome"), notify: false);
                }
            }
            catch (Exception e)
            {
                Licences.Logger?.Error(e);
                Licences.Logger?.Warn("Licences: the licensing office contact could not be added; the driving school's desks still work.");
            }
        }

        /// <summary>The same conversation face to face at a desk, in the game's own dialogue panel.</summary>
        public static void OpenInPerson(Action closed)
        {
            var dialogType = (CallDialogType)ModEnumHash.GetSafeHash(DialogTypeName);
            CallDialogFactory.RegisterDialog(dialogType, Guard.Maker(() => new DrivingSchoolDialog()));
            InstanceBehavior<UI.UIs>.Instance.playerHUD.dialogUI.ShowDialog(dialogType, NavigationBlocker.SpecialEmployeeDialog, null, Guard.Way(closed));
        }

    }

    /// <summary>The school's list, one service at a time: what it costs, where the player stands with it, "take it" or "what else".</summary>
    public class DrivingSchoolDialog : Dialog
    {
        public DrivingSchoolDialog()
        {
            npcNameKey = LicencePhone.ContactId;
            if (LicenceDirector.Instance == null || !LicenceState.Loaded || !Settings.DrivingLicence)
            {
                DialogController.current.ShowEntry(Guard.Entry(PhoneContacts.Closing("reallife_school_closed")));
                return;
            }
            DialogController.current.ShowEntry(Guard.Entry(Offer(0)));
        }

        private static bool InPerson => DialogController.current.dialogType != DialogType.PhoneCall;

        private static DialogEntry Offer(int index)
        {
            SchoolService[] order = DrivingSchool.Order;
            SchoolService service = order[index % order.Length];
            int next = (index + 1) % order.Length;
            bool inPerson = InPerson;
            bool available = DrivingSchool.Available(service, inPerson, out string reason);
            string status;
            if (service == SchoolService.Card)
            {
                status = DrivingSchool.CardText();
            }
            else if (available)
            {
                status = "reallife_school_ready".GetLocalization();
            }
            else if (reason == "reallife_school_no_in_person" && LicenceDirector.Instance.NearestDesk(out string address))
            {
                status = Licences.T("reallife_school_no_in_person_at", "address", address);
            }
            else
            {
                status = reason.GetLocalization();
            }
            var entry = new DialogEntry
            {
                messageData = DrivingSchool.PitchKey(service).Localize(new
                {
                    amount = Licences.Money(DrivingSchool.Fee(service)),
                    status
                }),
                OnCancel = PhoneContacts.HangUp
            };
            if (available && service != SchoolService.Card)
            {
                entry.ConfirmTextOverride = "reallife_school_take".Localize();
                entry.OnConfirm = () => Take(service);
                entry.SecondOptionTextOverride = "reallife_school_next";
                entry.OnSecondOption = () => Offer(next);
            }
            else
            {
                entry.ConfirmTextOverride = "reallife_school_next".Localize();
                entry.OnConfirm = () => Offer(next);
            }
            return entry;
        }

        private static DialogEntry Take(SchoolService service)
        {
            LicenceDirector director = LicenceDirector.Instance;
            // Asked again now: the hour may have turned or the money gone while they talked.
            if (director == null || !DrivingSchool.Available(service, InPerson, out string reason))
            {
                return PhoneContacts.Closing("reallife_school_not_now");
            }
            string done = director.Take(service);
            return PhoneContacts.Closing(done ?? "reallife_school_not_now");
        }
    }
}
