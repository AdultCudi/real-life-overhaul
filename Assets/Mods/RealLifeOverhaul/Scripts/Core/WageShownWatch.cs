using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using AI.Employees.SalaryNegotiation;
using Buildings.Office.Headquarters;
using Entities;
using Extensions;
using Localizor.LanguageChangeEvent;
using UI.Notification;
using UI.Smartphone.Apps.BizMan.HRManagers;
using UI.Smartphone.Apps.Contacts;
using UI.Smartphone.Apps.MyEmployees;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>Which of the game's labels naming one wage a watch keeps.</summary>
    internal enum WageLabel
    {
        EmployeeBox,   // My Employees' white box: the employee or candidate picked
        ContactCard,   // the Contacts app's card over an employee's or a candidate's chat
        PlanAverage    // BizMan's HR manager plan: the average wage of its employees
    }

    /// <summary>
    /// One of the game's labels that name a wage (<see cref="WageLabel"/>) written again at what is paid, each time the
    /// game writes it: the game hands the label new words each time, so a label whose words are not the last this wrote
    /// was written by the game. The Contacts card also tells, once a negotiation, what a candidate's wage costs at the
    /// city's prices, since the offers in the chat are in the game's own figures (the wage the game saves). Sits on the
    /// game's app, so it runs only while that app shows. Going Public: only this player's screens; nothing is sent.
    /// COST: one reference comparison a frame while the app shows; the rest only when the game has written the label.
    /// </summary>
    internal sealed class WageShownWatch : MonoBehaviour
    {
        private static readonly FeatureGuard FrameGuard = Guard.For(null, "WageShownWatch", typeof(WageShownWatch));

        private static readonly FieldInfo BoxLabelField = GameContract.WageBoxLabel.Field;
        private static readonly FieldInfo CardBoxField = GameContract.WageCardBox.Field;
        private static readonly FieldInfo PlanLabelField = GameContract.WagePlanLabel.Field;
        private static readonly FieldInfo PlanField = GameContract.WagePlan.Field;

        private const string BoxKey = "common_hourly_wage";   // the game's own words in each label, and what fills them
        private const string BoxWord = "hourlyWage";
        private const string CardWord = "wage";
        private const string PlanWord = "wage";
        private const float TellSeconds = 14f;

        private static readonly int HotUpdate = HotSpots.Slot("Core: WageShownWatch.Update");

        private WageLabel _kind;
        private MonoBehaviour _app;
        private TextLocalizationComponent _label;
        private object _seen;    // the words last looked at: the game's, or those this wrote

        /// <summary>The watch for one label, put on the game's app; null if the game has no such label.</summary>
        internal static WageShownWatch On(MonoBehaviour app, WageLabel kind)
        {
            TextLocalizationComponent label = Label(app, kind);
            if (label == null)
            {
                WageShown.Logger?.Info("Wages shown: the game's " + kind + " label is not there, so it keeps the game's figure.");
                return null;
            }
            WageShownWatch watch = null;
            foreach (WageShownWatch w in app.GetComponents<WageShownWatch>())
            {
                if (w._kind == kind)
                {
                    watch = w;
                }
            }
            if (watch == null)
            {
                watch = app.gameObject.AddComponent<WageShownWatch>();
            }
            watch._kind = kind;
            watch._app = app;
            watch._label = label;
            watch._seen = null;
            return watch;
        }

        private static TextLocalizationComponent Label(MonoBehaviour app, WageLabel kind)
        {
            switch (kind)
            {
                case WageLabel.EmployeeBox:
                    return BoxLabelField != null ? BoxLabelField.GetValue(app) as TextLocalizationComponent : null;
                case WageLabel.ContactCard:
                    GameObject box = CardBoxField != null ? CardBoxField.GetValue(app) as GameObject : null;
                    Transform line = box != null ? box.transform.Find("WageLabel") : null;   // as ContactsApp.SetUpHeader finds it
                    return line != null ? line.GetComponent<TextLocalizationComponent>() : null;
                case WageLabel.PlanAverage:
                    return PlanField != null && PlanLabelField != null ? PlanLabelField.GetValue(app) as TextLocalizationComponent : null;
                default:
                    return null;
            }
        }

        /// <summary>The watch stopped (the guard switched the wages shown off): the game's next words in the label stand.</summary>
        internal void GiveBack()
        {
            enabled = false;
            _seen = null;
        }

        private void Update()
        {
            using var hotSpot = HotSpots.Time(HotUpdate);
            if (FrameGuard.Off || WageShown.Shown.Off)
            {
                enabled = false;
                return;
            }
            try
            {
                OnUpdate();
            }
            catch (Exception e)
            {
                Guard.Fail(FrameGuard, e, this);
            }
        }

        private void OnUpdate()
        {
            if (_label == null)
            {
                return;
            }
            object words = _label.Arguments;
            if (ReferenceEquals(words, _seen))
            {
                return;
            }
            _seen = words;
            switch (_kind)
            {
                case WageLabel.EmployeeBox:
                    Box();
                    break;
                case WageLabel.ContactCard:
                    Card();
                    break;
                case WageLabel.PlanAverage:
                    Plan();
                    break;
            }
        }

        // Written as MyEmployees.ShowEmployee writes it, with the paid figure.
        private void Box()
        {
            var app = _app as MyEmployees;
            if (app != null && WageShown.Paid(app.SelectedEmployeeInstance, out _, out int paid) && _label.Key == BoxKey)
            {
                Write(BoxWord, paid);
            }
        }

        // The employee the card is about, found as ContactsApp.SetUpHeader finds them: by name among the employees, then
        // among the candidates in a negotiation.
        private void Card()
        {
            var app = _app as ContactsApp;
            Contact contact = app != null ? app.selectedContact : null;
            if (contact == null || !contact.IsEmployeeContact || SaveGameManager.Current == null)
            {
                return;
            }
            EmployeeInstance e = null;
            CandidateSalaryNegotiation talks = null;
            List<EmployeeInstance> all = SaveGameManager.Current.EmployeeInstances;
            for (int i = 0; all != null && i < all.Count && e == null; i++)
            {
                EmployeeInstance x = all[i];
                if (x != null && !x.isBeingReplaced && x.characterData != null && x.characterData.name == contact.id)
                {
                    e = x;
                }
            }
            List<CandidateSalaryNegotiation> open = SaveGameManager.Current.candidateSalaryNegotiations;
            for (int i = 0; e == null && open != null && i < open.Count; i++)
            {
                CandidateSalaryNegotiation n = open[i];
                if (n != null && n.employeeInstance != null && n.employeeInstance.characterData != null && n.employeeInstance.characterData.name == contact.id)
                {
                    e = n.employeeInstance;
                    talks = n;
                }
            }
            if (!WageShown.Paid(e, out int game, out int paid))
            {
                return;
            }
            Write(CardWord, paid);
            if (talks != null && !talks.completed && PaidWage.City(game, Prices.Level) != 0 && WageShown.FirstTell(talks.id))
            {
                Notifications.Show(NotificationType.Info, "reallife_wage_hire_city", new Dictionary<string, string>
                {
                    { "percent", (Prices.Level / 10f).ToString("0.#", CultureInfo.InvariantCulture) + "%" },
                    { "name", contact.id },
                    { "ask", (game / 100f).ToCurrencyFormat() },
                    { "cost", (PaidWage.Cents(game, Prices.Level, 0) / 100f).ToCurrencyFormat() }
                }, TellSeconds, "reallife_wage_hire_city", null, true, false);
            }
        }

        // The average of the plan's employees as HrManagerPlanUI.SetUpBasicData takes it, of what each is paid.
        private void Plan()
        {
            var plan = PlanField.GetValue(_app) as HrManagerPlan;
            List<string> ids = plan != null ? plan.assignedEmployees : null;
            if (ids == null || ids.Count == 0)
            {
                return;   // the game writes a dash
            }
            long paid = 0;
            int count = 0;
            bool differs = false;
            for (int i = 0; i < ids.Count; i++)
            {
                EmployeeInstance e = WageShown.ById(ids[i]);
                if (e == null)
                {
                    continue;
                }
                count++;
                bool other = WageShown.Paid(e, out _, out int p);
                differs |= other;
                paid += other ? p : PaidWage.Base(e.hourlyWage);
            }
            if (count > 0 && differs)
            {
                Write(PlanWord, (int)Math.Min(int.MaxValue / 2, paid / count));
            }
        }

        private void Write(string word, int cents)
        {
            var words = new Dictionary<string, string>(1) { { word, (cents / 100f).ToCurrencyFormat() } };
            _seen = words;
            _label.Arguments = words;
        }

        // The app shows again: whatever the game wrote meanwhile is looked at afresh.
        private void OnEnable()
        {
            _seen = null;
        }
    }
}
