using System;
using System.Collections.Generic;
using System.Text;
using BAModAPI;
using Dialogs;
using Entities;
using Localizor;
using Streets;
using UI.Smartphone.Apps.Contacts;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>What the driving school (and the licensing office on the phone) offers.</summary>
    internal enum SchoolService
    {
        Card,          // the licence card
        Theory,
        LessonCar,
        TestCar,
        LessonVan,
        TestVan,
        Taxi,
        Renew,
        Motorcycle,    // the ride assessment at the desk
        TheoryHeavy,   // the heavy goods paper
        LessonHeavy,   // in the school's truck
        TestHeavy
    }

    /// <summary>The driving school's rules: what each service costs, and when it can be had, at the desk or on the phone.</summary>
    internal static class DrivingSchool
    {
        public const int OpensAt = 7;
        public const int ClosesAt = 20;
        public const int Classes = 5;       // LicenceClass: car, commercial, taxi, motorcycle, heavy

        private static readonly SchoolService[] WithHeavy =
        {
            SchoolService.Card, SchoolService.Theory, SchoolService.LessonCar, SchoolService.TestCar,
            SchoolService.LessonVan, SchoolService.TestVan, SchoolService.TheoryHeavy, SchoolService.LessonHeavy, SchoolService.TestHeavy,
            SchoolService.Motorcycle, SchoolService.Taxi, SchoolService.Renew
        };

        private static readonly SchoolService[] WithoutHeavy =
        {
            SchoolService.Card, SchoolService.Theory, SchoolService.LessonCar, SchoolService.TestCar,
            SchoolService.LessonVan, SchoolService.TestVan, SchoolService.Motorcycle, SchoolService.Taxi, SchoolService.Renew
        };

        /// <summary>The services in the order the desk and the phone offer them; the heavy class's only while its option is on.</summary>
        public static SchoolService[] Order => Settings.HeavyLicence ? WithHeavy : WithoutHeavy;

        /// <summary>The heavy class's step a service is, if it is one.</summary>
        public static bool HeavyStep(SchoolService service, out HeavyRules.Step step)
        {
            step = service == SchoolService.TheoryHeavy ? HeavyRules.Step.Theory : service == SchoolService.LessonHeavy ? HeavyRules.Step.Lesson : HeavyRules.Step.Test;
            return service == SchoolService.TheoryHeavy || service == SchoolService.LessonHeavy || service == SchoolService.TestHeavy;
        }

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
                case SchoolService.TheoryHeavy: return HeavyRules.Fee(HeavyRules.Step.Theory, LicenceState.Heavy);
                case SchoolService.LessonHeavy: return HeavyRules.Fee(HeavyRules.Step.Lesson, LicenceState.Heavy);
                case SchoolService.TestHeavy: return HeavyRules.Fee(HeavyRules.Step.Test, LicenceState.Heavy);
                default: return 0;
            }
        }

        /// <summary>How many licences could be renewed on a day.</summary>
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

        /// <summary>Whether a service can be had now; if not, the locale key of the reason.</summary>
        public static bool Available(SchoolService service, bool inPerson, out string reasonKey)
        {
            reasonKey = null;
            if (service == SchoolService.Card)
            {
                return true;
            }
            int day = TimeHelper.CurrentDay;
            LicenceStanding car = LicenceState.Standing(LicenceClass.Car, day);
            LicenceStanding van = LicenceState.Standing(LicenceClass.Commercial, day);
            LicenceStanding bike = LicenceState.Standing(LicenceClass.Motorcycle, day);
            bool needsDesk = service != SchoolService.Renew && service != SchoolService.Taxi;
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
            else if (HeavyStep(service, out HeavyRules.Step step))
            {
                LicenceStanding heavy = LicenceState.Standing(LicenceClass.Heavy, day);
                reasonKey = !Settings.HeavyLicence ? "reallife_school_closed"
                    : HeavyRules.Refusal(step, van == LicenceStanding.Valid, Holds(heavy), heavy == LicenceStanding.Valid, LicenceState.Heavy, day);
            }
            else
            {
                switch (service)
                {
                    case SchoolService.Theory:
                        reasonKey = Holds(car) && Holds(bike) ? "reallife_school_no_have"   // one paper serves the car and the motorbike
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
                case SchoolService.TheoryHeavy: return "reallife_school_pitch_theory_heavy";
                case SchoolService.LessonHeavy: return "reallife_school_pitch_lesson_heavy";
                case SchoolService.TestHeavy: return "reallife_school_pitch_test_heavy";
                default: return "reallife_school_pitch_card";
            }
        }

        /// <summary>The licence card as text, for a notification or the phone.</summary>
        public static string CardText()
        {
            int day = TimeHelper.CurrentDay;
            var text = new StringBuilder(256);
            for (int i = 0; i < Classes; i++)
            {
                var kind = (LicenceClass)i;
                if (!OnCard(kind))
                {
                    continue;
                }
                text.Append(Licences.ClassName(kind)).Append(": ").Append(CardLine(kind, day)).Append('\n');
            }
            int points = LicenceState.PointsOn(day);
            text.Append(Licences.T("reallife_lic_card_points", "points", points.ToString(), "limit", LicenceLaw.PointsLimit.ToString()));
            AppendEntries(text, day, "  ");
            if (LicenceState.SuspendedUntil > day)
            {
                text.Append('\n').Append(Licences.T("reallife_lic_card_suspended", "day", LicenceState.SuspendedUntil.ToString()));
            }
            text.Append('\n').Append(Licences.T("reallife_lic_card_today", "day", day.ToString()));
            return text.ToString();
        }

        /// <summary>Whether a class has a line on the card: the heavy class only while its option is on or once held.</summary>
        public static bool OnCard(LicenceClass kind)
        {
            return kind != LicenceClass.Heavy || Settings.HeavyLicence || LicenceState.Of(kind).Held;
        }

        /// <summary>A licence's line on the card: its standing and dates, or never held.</summary>
        public static string CardLine(LicenceClass kind, int day)
        {
            LicenceState.Card card = LicenceState.Of(kind);
            if (!card.Held)
            {
                return Licences.StandingName(LicenceStanding.None);
            }
            return Licences.T("reallife_lic_card_line", "standing", Licences.StandingName(LicenceState.Standing(kind, day)), "issued", card.Issued.ToString())
                .Replace("{expires}", card.Expires.ToString());
        }

        /// <summary>Appends a line for each record entry that still counts.</summary>
        public static void AppendEntries(StringBuilder text, int day, string indent)
        {
            IReadOnlyList<LicenceState.Entry> entries = LicenceState.Entries;
            for (int i = 0; i < entries.Count; i++)
            {
                if (day - entries[i].Day < LicenceLaw.PointsExpireDays)
                {
                    text.Append('\n').Append(indent).Append(entries[i].Points).Append("  ").Append(Licences.T(PointName(entries[i].Code)))
                        .Append("  (").Append(Licences.T("reallife_lic_card_until", "day", (entries[i].Day + LicenceLaw.PointsExpireDays).ToString())).Append(')');
                }
            }
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
    /// The school's desks: a learner-plate board by the door of each school building, three at most, found once per city
    /// load a few buildings a second. Runs only when the director calls it.
    /// </summary>
    internal sealed class LicenceDesks
    {
        private const int Most = 3;
        private const int PerTick = 30;
        internal const float Aside = 4.3f;       // metres along the wall from the door, clear of a job board and the university's door
        private const float FromWall = 0.55f;    // metres
        private const float SignCullRadius = 2f; // metres round a sign's foot: all of it (Performance/ModCulling)
        internal const float Reach = 1.7f;       // metres from a desk's front that count as at it
        private const string SchoolType = "ba:businesstype_school";

        private readonly Transform _parent;
        private readonly IModLogger _logger;
        private readonly List<Transform> _signs = new List<Transform>(Most);
        private readonly List<CullEntry> _culls = new List<CullEntry>(Most);   // the signs, not drawn beyond the game's far band
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

        public Vector3 Front(int index) => _fronts[index];

        public string Address(int index) => _addresses[index];

        /// <summary>The school a desk stands at, lit on the city map.</summary>
        public CityBuildingController Building(int index) => _buildings[index];

        public void Away()
        {
            At = -1;
        }

        /// <summary>The nearest desk to a point. False if none has been found yet.</summary>
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
            for (int i = 0; i < _culls.Count; i++)
            {
                ModCulling.Remove(_culls[i]);
            }
            _culls.Clear();
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

        // On the pavement to one side of the school's door, a step out from the wall, where nothing else stands.
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
                _culls.Add(ModCulling.Add(CullRules.Kind.BuildingSign, sign, at, SignCullRadius));
                _fronts.Add(stand + outward * 0.6f);
                _addresses.Add(building.building.Address.ToFormattedString());
                _buildings.Add(building);
                return;
            }
        }
    }

    /// <summary>The licensing office as a phone contact: the card, renewals and the taxi endorsement, and directions to a desk.</summary>
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
                ContactPictures.Add(ContactId);
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

    /// <summary>The school's services in the game's dialogue, one at a time: its fee and status, then take it or the next.</summary>
    public sealed class DrivingSchoolDialog : Dialog
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
            // Asked again: the hour may have turned or the money gone while they talked.
            if (director == null || !DrivingSchool.Available(service, InPerson, out string reason))
            {
                return PhoneContacts.Closing("reallife_school_not_now");
            }
            string done = director.Take(service);
            return PhoneContacts.Closing(done ?? "reallife_school_not_now");
        }
    }
}
