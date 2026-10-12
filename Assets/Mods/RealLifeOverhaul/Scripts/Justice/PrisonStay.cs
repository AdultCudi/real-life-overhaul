using System;
using System.Collections;
using System.Text;
using BAModAPI;
using BigAmbitions.DayNightCycle;
using Entities;
using Helpers;
using UI;
using UI.Notification;
using UnityEngine;
using Random = UnityEngine.Random;

namespace RealLifeOverhaul
{
    /// <summary>
    /// Everything the justice system does to the game while it holds the player: the pause, the
    /// navigation blocker, the fade to black and the resting energy rate. Each is remembered, so
    /// <see cref="ReleaseAll"/> can put back exactly what was changed, from anywhere, any number
    /// of times. <see cref="Beats"/> goes up while whatever is holding the player is
    /// alive; the director's watchdog frees the player when it goes stale.
    /// </summary>
    public sealed class JusticeHold
    {
        private const NavigationBlocker Blocker = NavigationBlocker.JobOfferPanel;   // one of the game's own that nothing of the mod uses

        private readonly IModLogger _logger;
        private PlayerController _blocked;
        private bool _paused, _faded, _resting;

        public JusticeHold(IModLogger logger)
        {
            _logger = logger;
        }

        /// <summary>Counts the holder's signs of life. Counted, not timed: a game left in the background must not look like a dead routine.</summary>
        public int Beats;

        public bool Any => _paused || _faded || _resting || _blocked != null;

        public bool Faded => _faded;

        public void Beat()
        {
            Beats++;
        }

        /// <summary>Game time stops and the player's own speed controls are off, as during the game's time skip. Safe to repeat.</summary>
        public void Pause()
        {
            UIs uis = InstanceBehavior<UIs>.Instance;
            if (uis == null || uis.gameSpeed == null)
            {
                return;
            }
            _paused = true;
            uis.gameSpeed.Set(new GameSpeed(true, TimeSpeed.Pause, false));
            uis.gameSpeed.DisableTimeControl(true);
        }

        public void Unpause()
        {
            if (!_paused)
            {
                return;
            }
            _paused = false;
            UIs uis = InstanceBehavior<UIs>.Instance;
            if (uis != null && uis.gameSpeed != null && (uis.timeMachine == null || !uis.timeMachine.isRunning))
            {
                uis.gameSpeed.DisableTimeControl(false);
                uis.gameSpeed.Reset();
            }
        }

        /// <summary>The game's own time skip has taken over the pause (it sets and resets it itself).</summary>
        public void PauseHandedOver()
        {
            _paused = false;
        }

        public void Block()
        {
            PlayerController player = PlayerHelper.PlayerController;
            if (player == null || ReferenceEquals(player, _blocked))
            {
                return;
            }
            Unblock();
            player.ResetNavigation();
            player.SetNavigationBlocker(Blocker);
            _blocked = player;
        }

        public void Unblock()
        {
            PlayerController player = _blocked;
            _blocked = null;
            if (player != null)
            {
                try
                {
                    player.UnsetNavigationBlocker(Blocker);
                }
                catch (Exception e)
                {
                    _logger?.Error(e);
                }
            }
        }

        public IEnumerator Fade(string infoKey)
        {
            _faded = true;
            yield return UiFader.Fade(0.7f, infoKey);
        }

        public IEnumerator Unfade()
        {
            if (_faded)
            {
                _faded = false;
                yield return UiFader.UnFade();
            }
        }

        /// <summary>A bunk: the game's own slow rest, as in its waiting rooms.</summary>
        public void Rest(bool on)
        {
            if (on != _resting)
            {
                _resting = on;
                EnergyHelper.SetCurrentEnergyRegen(on ? EnergyRegen.Hospital : EnergyRegen.None);
            }
        }

        /// <summary>Everything put back, whatever state it was in. <paramref name="host"/> runs the fade back in if it still can.</summary>
        public void ReleaseAll(MonoBehaviour host)
        {
            // Each step on its own: one that fails must not keep the others from being put back.
            try
            {
                Rest(false);
            }
            catch (Exception e)
            {
                _resting = false;
                _logger?.Error(e);
            }
            Unblock();
            try
            {
                Unpause();
            }
            catch (Exception e)
            {
                _logger?.Error(e);
            }
            if (!_faded)
            {
                return;
            }
            _faded = false;
            try
            {
                if (host != null && host.isActiveAndEnabled)
                {
                    host.StartCoroutine(UiFader.UnFade());
                    return;
                }
            }
            catch (Exception e)
            {
                _logger?.Error(e);
            }
            UiFader.isFading = false;   // the scene is going (or the fade back could not be run): the flag at least is put right
        }
    }

    /// <summary>
    /// Custody and prison, both presented as time passing: the screen goes dark, the player is
    /// taken off the street (out of their car, out of any building) and put down on the pavement
    /// outside the courthouse or the precinct, and the clock is moved on with the game's own time
    /// skip, the one its hospital and its taxis use. The player is never put anywhere the base
    /// game does not know: a save made at any moment of it loads without the mod with the player
    /// standing in the street.
    ///
    /// A prison term is served a day at a time. Each morning the panel comes up: the parole board
    /// on its day, then how to spend the day (work detail, exercise, study), the yard (keep to
    /// yourself or mix), and on most days something that happens, dealt from a pool without
    /// repeats. Then the clock runs to the next morning. Good behaviour takes days off; the term,
    /// the days served and what was earned are in the save, so a game loaded part of the way
    /// through carries on from the morning it had reached. While the clock runs, energy and
    /// hunger are held up (a bunk and three meals), so nobody comes out starving.
    /// </summary>
    public sealed class PrisonStay
    {
        private const float SkipMost = 300f;       // real seconds one time skip may take before it is stopped
        private const float NeedsFloor = 55f;      // energy and hunger are not let below this while inside
        private const int Morning = 7;

        private sealed class PrisonEvent
        {
            public string Id, Intro, A, B, OutA, OutB;
            public int BehaviourA, MoneyA, BehaviourB, MoneyB;
            public bool StudyA;         // answer A is a day's reading
            public bool GambleA;        // answer A wins or loses its money
        }

        private static readonly PrisonEvent[] Events =
        {
            new PrisonEvent { Id = "contraband", Intro = "reallife_justice_pe_contraband", A = "reallife_justice_pe_contraband_a", B = "reallife_justice_pe_contraband_b",
                OutA = "reallife_justice_pe_contraband_out_a", OutB = "reallife_justice_pe_contraband_out_b", BehaviourA = 1, BehaviourB = -2, MoneyB = 30 },
            new PrisonEvent { Id = "search", Intro = "reallife_justice_pe_search", A = "reallife_justice_pe_search_a", B = "reallife_justice_pe_search_b",
                OutA = "reallife_justice_pe_search_out_a", OutB = "reallife_justice_pe_search_out_b", BehaviourA = 1, BehaviourB = -1 },
            new PrisonEvent { Id = "fight", Intro = "reallife_justice_pe_fight", A = "reallife_justice_pe_fight_a", B = "reallife_justice_pe_fight_b",
                OutA = "reallife_justice_pe_fight_out_a", OutB = "reallife_justice_pe_fight_out_b", BehaviourA = 1, BehaviourB = -1 },
            new PrisonEvent { Id = "letter", Intro = "reallife_justice_pe_letter", A = "reallife_justice_pe_letter_a", B = "reallife_justice_pe_letter_b",
                OutA = "reallife_justice_pe_letter_out_a", OutB = "reallife_justice_pe_letter_out_b", BehaviourA = 1 },
            new PrisonEvent { Id = "kitchen", Intro = "reallife_justice_pe_kitchen", A = "reallife_justice_pe_kitchen_a", B = "reallife_justice_pe_kitchen_b",
                OutA = "reallife_justice_pe_kitchen_out_a", OutB = "reallife_justice_pe_kitchen_out_b", BehaviourA = 1, MoneyA = 10 },
            new PrisonEvent { Id = "book", Intro = "reallife_justice_pe_book", A = "reallife_justice_pe_book_a", B = "reallife_justice_pe_book_b",
                OutA = "reallife_justice_pe_book_out_a", OutB = "reallife_justice_pe_book_out_b", StudyA = true, MoneyB = 10 },
            new PrisonEvent { Id = "informer", Intro = "reallife_justice_pe_informer", A = "reallife_justice_pe_informer_a", B = "reallife_justice_pe_informer_b",
                OutA = "reallife_justice_pe_informer_out_a", OutB = "reallife_justice_pe_informer_out_b", BehaviourB = 2 },
            new PrisonEvent { Id = "cards", Intro = "reallife_justice_pe_cards", A = "reallife_justice_pe_cards_a", B = "reallife_justice_pe_cards_b",
                OutA = "reallife_justice_pe_cards_out_a", OutB = "reallife_justice_pe_cards_out_b", GambleA = true, MoneyA = 25 },
            new PrisonEvent { Id = "cellmate", Intro = "reallife_justice_pe_cellmate", A = "reallife_justice_pe_cellmate_a", B = "reallife_justice_pe_cellmate_b",
                OutA = "reallife_justice_pe_cellmate_out_a", OutB = "reallife_justice_pe_cellmate_out_b", BehaviourA = 1, MoneyA = -10 },
            new PrisonEvent { Id = "noise", Intro = "reallife_justice_pe_noise", A = "reallife_justice_pe_noise_a", B = "reallife_justice_pe_noise_b",
                OutA = "reallife_justice_pe_noise_out_a", OutB = "reallife_justice_pe_noise_out_b", BehaviourB = -1 },
            new PrisonEvent { Id = "counsellor", Intro = "reallife_justice_pe_counsellor", A = "reallife_justice_pe_counsellor_a", B = "reallife_justice_pe_counsellor_b",
                OutA = "reallife_justice_pe_counsellor_out_a", OutB = "reallife_justice_pe_counsellor_out_b", BehaviourA = 1 },
            new PrisonEvent { Id = "queue", Intro = "reallife_justice_pe_queue", A = "reallife_justice_pe_queue_a", B = "reallife_justice_pe_queue_b",
                OutA = "reallife_justice_pe_queue_out_a", OutB = "reallife_justice_pe_queue_out_b", BehaviourA = 1, BehaviourB = -2 }
        };

        private static readonly string[] Mornings =
        {
            "reallife_justice_prison_morning_1", "reallife_justice_prison_morning_2", "reallife_justice_prison_morning_3", "reallife_justice_prison_morning_4"
        };

        private static readonly string[] MixGood = { "reallife_justice_prison_mix_good_1", "reallife_justice_prison_mix_good_2" };
        private static readonly string[] MixBad = { "reallife_justice_prison_mix_bad_1", "reallife_justice_prison_mix_bad_2" };

        private readonly JusticeDirector _director;
        private readonly JusticeHold _hold;
        private readonly IModLogger _logger;
        private readonly StringBuilder _text = new StringBuilder(400);
        private readonly PrisonEvent[] _unseen = new PrisonEvent[Events.Length];

        private bool _waiting;          // a page is up and the routine waits for the last answer of the morning
        private bool _paroled;
        private string _note;           // said at the top of the next morning's page (a day off for good behaviour)
        private string _dayText;        // what the day's choices came to, shown before the event
        private VehicleInstance _leftBehind;

        public PrisonStay(JusticeDirector director, JusticeHold hold, IModLogger logger)
        {
            _director = director;
            _hold = hold;
            _logger = logger;
        }

        /// <summary>True from the moment a custody or a term begins until the player is free again.</summary>
        public bool Running { get; private set; }

        // ---------------------------------------------------------------- custody (hours)

        /// <summary>
        /// Some hours in a cell, as a plain time skip: dark, the clock moved on, light again. <paramref name="toCourt"/> puts
        /// a player who is on foot down outside the courthouse (or the precinct) first: someone held until their hearing is brought to it.
        /// </summary>
        public IEnumerator Custody(int hours, bool toCourt, Action done)
        {
            Running = true;
            hours = Mathf.Clamp(hours, 1, JusticeLaw.CustodyCapHours);
            try
            {
                _hold.Beat();
                yield return _hold.Fade("reallife_justice_custody_fade");
                // Someone at the wheel stays with their car for these hours, as in the mod's roadside custody: a car is
                // only ever left behind for a prison term, which remembers it and brings it back.
                var gm = InstanceBehavior<GameManager>.Instance;
                if (toCourt && gm != null && gm.selectedVehicle == null && !PlayerHelper.IsUsingVehicle)
                {
                    yield return TakeOffTheStreet();
                }
                _hold.Block();   // held where they are for the hours that pass
                Timestamp release = TimeHelper.Now();
                release.AddDays(hours / 24);   // (the game's AddHours carries over one day only)
                release.AddHours(hours % 24);
                yield return Skip(release, "reallife_justice_custody_info");
                Needs(70f, 70f);
                _hold.Rest(false);
                yield return _hold.Unfade();
                if (SaveGameManager.Current != null)
                {
                    ModState.AddModifier(ModState.MoodDown);
                    JusticeText.Toast(NotificationType.Info, "reallife_justice_custody_released", "hours", JusticeText.N(hours), 10f);
                }
                _logger?.Info("Justice: " + hours + " h of custody served.");
            }
            finally
            {
                Running = false;
                _hold.ReleaseAll(_director);
            }
            done?.Invoke();
        }

        // ---------------------------------------------------------------- prison (days)

        /// <summary>The term in JusticeState, from the morning it has reached. Ends with the player released.</summary>
        public IEnumerator Serve()
        {
            Running = true;
            _paroled = false;
            _note = null;
            bool released = false;
            try
            {
                _hold.Beat();
                bool fresh = JusticeState.PrisonServed == 0;
                yield return _hold.Fade(fresh ? "reallife_justice_prison_fade" : "reallife_justice_prison_fade_resume");
                yield return TakeOffTheStreet();
                if (_leftBehind != null && string.IsNullOrEmpty(JusticeState.PrisonCar))
                {
                    JusticeState.PrisonCar = _leftBehind.id ?? string.Empty;
                }
                _logger?.Info("Justice: prison, day " + (JusticeState.PrisonServed + 1) + " of " + JusticeState.PrisonTotal + ".");

                while (JusticeState.InPrison && JusticeState.PrisonServed < JusticeState.PrisonTotal && !_paroled)
                {
                    _hold.Pause();
                    _hold.Block();
                    _waiting = true;
                    ShowMorning();
                    while (_waiting)
                    {
                        _hold.Beat();
                        if (!JusticePanel.IsShowing)
                        {
                            throw new InvalidOperationException("the prison panel went away while a day was being decided");
                        }
                        yield return null;
                    }
                    JusticePanel.Instance.Close();
                    if (_paroled)
                    {
                        break;
                    }
                    Timestamp next = TimeHelper.Now();
                    if (next.Hour >= Morning)
                    {
                        next.AddHours(24);
                    }
                    next.Hour = Morning;
                    next.Minute = 0f;
                    yield return Skip(next, "reallife_justice_prison_skip");
                    JusticeState.PrisonServed++;
                    Remit();
                }

                Release();
                released = true;
                _hold.Pause();
                _waiting = true;
                ShowSummary();
                while (_waiting && JusticePanel.IsShowing)
                {
                    _hold.Beat();
                    yield return null;
                }
                if (JusticePanel.Exists)
                {
                    JusticePanel.Instance.Close();
                }
                _hold.Unpause();
                _hold.Unblock();
                yield return _hold.Unfade();
                yield return new WaitForSecondsRealtime(0.5f);
                ReturnCar();
            }
            finally
            {
                Running = false;
                if (!released && JusticeState.InPrison)
                {
                    // Cut short by a fault or by the scene going: whatever was left of the term is let go. Nobody is kept
                    // in a state the game cannot show; the summary is a toast.
                    _logger?.Warn("Justice: the prison term ended early (" + JusticeState.PrisonServed + " of " + JusticeState.PrisonTotal + " days); the player is released.");
                    try
                    {
                        if (JusticePanel.Exists)
                        {
                            JusticePanel.Instance.Close();
                        }
                        Release();
                        JusticeText.Toast(NotificationType.Info, "reallife_justice_prison_released_early", 10f);
                    }
                    catch (Exception e)
                    {
                        _logger?.Error(e);
                        JusticeState.ClearPrison();
                    }
                }
                _hold.ReleaseAll(_director);
            }
        }

        // Good behaviour: a day off for every four points, never more than a third of the term, never below what is already served.
        private void Remit()
        {
            int earned = JusticeLaw.Remission(JusticeState.PrisonBehaviour, JusticeState.PrisonOriginal) - JusticeState.PrisonRemitted;
            if (earned > 0 && JusticeState.PrisonTotal - earned >= JusticeState.PrisonServed)
            {
                JusticeState.PrisonTotal -= earned;
                JusticeState.PrisonRemitted += earned;
                _note = JusticeText.T("reallife_justice_prison_remitted", "days", JusticeText.N(earned));
                _logger?.Info("Justice: " + earned + " day(s) off for good behaviour; the term is now " + JusticeState.PrisonTotal + ".");
            }
        }

        private JusticePage Page(JusticeFace left, string leftName, JusticeFace right, string rightName, int speaker, string speakerName, string body)
        {
            int day = Mathf.Min(JusticeState.PrisonServed + 1, Mathf.Max(1, JusticeState.PrisonTotal));
            return new JusticePage
            {
                Scene = JusticeScene.Cell,
                Title = JusticeText.T("reallife_justice_prison_title"),
                Subtitle = JusticeText.T("reallife_justice_prison_subtitle", "day", JusticeText.N(day), "days", JusticeText.N(JusticeState.PrisonTotal),
                    "behaviour", JusticeText.N(JusticeState.PrisonBehaviour)),
                Left = left, LeftName = leftName, Right = right, RightName = rightName, Speaker = speaker, SpeakerName = speakerName, Body = body
            };
        }

        private static string Officer => JusticeText.T("reallife_justice_prison_officer");
        private static string Cellmate => JusticeText.T("reallife_justice_prison_cellmate");

        private void ShowMorning()
        {
            int day = JusticeState.PrisonServed + 1;
            if (!JusticeState.PrisonParoleHeard && day == JusticeLaw.ParoleDay(JusticeState.PrisonOriginal))
            {
                ShowParole();
                return;
            }
            ShowActivities();
        }

        private void ShowActivities()
        {
            int day = JusticeState.PrisonServed + 1;
            _text.Length = 0;
            if (_note != null)
            {
                _text.Append(_note).Append("\n\n");
                _note = null;
            }
            _text.Append(JusticeText.T(day == 1 ? "reallife_justice_prison_intake" : Mornings[(day + JusticeState.PrisonCase) % Mornings.Length]));

            // A friend comes on the second day and every third after it, if there is one.
            JusticeFace right = JusticeFace.Inmate;
            string rightName = Cellmate;
            if (day >= 2 && (day - 2) % 3 == 0)
            {
                try
                {
                    if (Justice.Bridges.Social.CloseFriend(out string friend) && !string.IsNullOrEmpty(friend))
                    {
                        right = JusticeFace.Visitor;
                        rightName = friend;
                        JusticeState.PrisonBehaviour += 1;
                        _text.Append("\n\n").Append(JusticeText.T(day == 2 ? "reallife_justice_prison_visit" : "reallife_justice_prison_call", "name", friend));
                        Justice.Bridges.Social.Stood(friend, day == 2);
                    }
                }
                catch (Exception e)
                {
                    _logger?.Error(e);
                }
            }
            _text.Append("\n\n").Append(JusticeText.T("reallife_justice_prison_how"));
            JusticePage page = Page(JusticeFace.Officer, Officer, right, rightName, 0, Officer, _text.ToString())
                .Add(JusticeText.T("reallife_justice_prison_work"), JusticeText.T("reallife_justice_prison_work_hint", "amount", JusticeText.Money(JusticeLaw.PrisonWorkPay)))
                .Add(JusticeText.T("reallife_justice_prison_exercise"), JusticeText.T("reallife_justice_prison_exercise_hint"))
                .Add(JusticeText.T("reallife_justice_prison_study"), JusticeText.T("reallife_justice_prison_study_hint"));
            JusticePanel.Instance.Show(page, choice =>
            {
                if (choice == 0)
                {
                    JusticeState.PrisonEarned += JusticeLaw.PrisonWorkPay;
                    JusticeState.PrisonBehaviour += 1;
                    _dayText = JusticeText.T("reallife_justice_prison_work_done", "amount", JusticeText.Money(JusticeLaw.PrisonWorkPay));
                }
                else if (choice == 1)
                {
                    JusticeState.PrisonExercised++;
                    _dayText = JusticeText.T("reallife_justice_prison_exercise_done");
                }
                else
                {
                    Study(1f);
                    _dayText = JusticeText.T("reallife_justice_prison_study_done");
                }
                ShowYard();
            });
        }

        // A day in the library: a point of the game's own lawyer skill.
        private void Study(float points)
        {
            JusticeState.PrisonStudied++;
            try
            {
                EmployeeInstance self = PlayerHelper.GetPlayerEmployeeInstance();
                if (self != null)
                {
                    self.IncreaseSkill("ba:skill_lawyer", points);
                }
            }
            catch (Exception e)
            {
                _logger?.Error(e);
            }
        }

        private void ShowYard()
        {
            JusticePage page = Page(JusticeFace.Officer, Officer, JusticeFace.Inmate, Cellmate, 1, null, _dayText + "\n\n" + JusticeText.T("reallife_justice_prison_yard"))
                .Add(JusticeText.T("reallife_justice_prison_alone"), JusticeText.T("reallife_justice_prison_alone_hint"))
                .Add(JusticeText.T("reallife_justice_prison_mix"), JusticeText.T("reallife_justice_prison_mix_hint"));
            page.Speaker = -1;
            JusticePanel.Instance.Show(page, choice =>
            {
                string outcome;
                if (choice == 0)
                {
                    JusticeState.PrisonBehaviour += 1;
                    outcome = JusticeText.T("reallife_justice_prison_alone_done");
                }
                else
                {
                    float roll = Random.value;
                    if (roll < 0.55f)
                    {
                        JusticeState.PrisonBehaviour += 1;
                        JusticeState.PrisonEarned += 15;
                        outcome = JusticeText.T(MixGood[Random.Range(0, MixGood.Length)]);
                    }
                    else if (roll < 0.80f)
                    {
                        outcome = JusticeText.T("reallife_justice_prison_mix_plain");
                    }
                    else
                    {
                        JusticeState.PrisonBehaviour -= 2;
                        outcome = JusticeText.T(MixBad[Random.Range(0, MixBad.Length)]);
                    }
                }
                PrisonEvent happening = Random.value < 0.7f ? Deal() : null;
                if (happening != null)
                {
                    ShowEvent(happening, outcome);
                }
                else
                {
                    ShowNight(outcome);
                }
            });
        }

        // The next thing that happens, from those this term has not had. Null when the pool is used up.
        private PrisonEvent Deal()
        {
            string seen = "," + JusticeState.PrisonSeen + ",";
            int count = 0;
            for (int i = 0; i < Events.Length; i++)
            {
                if (seen.IndexOf("," + Events[i].Id + ",", StringComparison.Ordinal) < 0)
                {
                    _unseen[count++] = Events[i];
                }
            }
            if (count == 0)
            {
                return null;
            }
            PrisonEvent picked = _unseen[Random.Range(0, count)];
            JusticeState.PrisonSeen = JusticeState.PrisonSeen.Length == 0 ? picked.Id : JusticeState.PrisonSeen + "," + picked.Id;
            return picked;
        }

        private void ShowEvent(PrisonEvent happening, string before)
        {
            JusticePage page = Page(JusticeFace.Officer, Officer, JusticeFace.Inmate, Cellmate, 2, null, before + "\n\n" + JusticeText.T(happening.Intro))
                .Add(JusticeText.T(happening.A))
                .Add(JusticeText.T(happening.B));
            page.Speaker = -1;
            JusticePanel.Instance.Show(page, choice =>
            {
                string outcome;
                if (choice == 0)
                {
                    JusticeState.PrisonBehaviour += happening.BehaviourA;
                    int money = happening.MoneyA;
                    bool won = true;
                    if (happening.GambleA)
                    {
                        won = Random.value < 0.5f;
                        money = won ? money : -money;
                    }
                    JusticeState.PrisonEarned = Mathf.Max(0, JusticeState.PrisonEarned + money);
                    if (happening.StudyA)
                    {
                        Study(0.5f);
                    }
                    outcome = JusticeText.T(happening.GambleA && !won ? "reallife_justice_pe_cards_out_lost" : happening.OutA);
                }
                else
                {
                    JusticeState.PrisonBehaviour += happening.BehaviourB;
                    JusticeState.PrisonEarned = Mathf.Max(0, JusticeState.PrisonEarned + happening.MoneyB);
                    outcome = JusticeText.T(happening.OutB);
                }
                ShowNight(outcome);
            });
        }

        // The last page of a day: what came of it, then lights out.
        private void ShowNight(string outcome)
        {
            JusticePage page = Page(JusticeFace.Officer, Officer, JusticeFace.Inmate, Cellmate, -1, null, outcome + "\n\n" + JusticeText.T("reallife_justice_prison_lights_out"))
                .Add(JusticeText.T("reallife_justice_prison_sleep"));
            JusticePanel.Instance.Show(page, _ => _waiting = false);
        }

        // ---------------------------------------------------------------- parole

        /// <summary>The board sits: how the player has behaved, less their record, plus how they speak to it. Test button and day two thirds.</summary>
        public void ShowParole()
        {
            JusticeState.PrisonParoleHeard = true;
            int served = JusticeState.PrisonServed;
            int need = Mathf.Max(2, served);
            JusticePage page = Page(JusticeFace.Officer, Officer, JusticeFace.Clerk, JusticeText.T("reallife_justice_parole_chair"), 2, JusticeText.T("reallife_justice_parole_chair"),
                    JusticeText.T("reallife_justice_parole_open", "served", JusticeText.N(served), "days", JusticeText.N(JusticeState.PrisonTotal))
                    + "\n\n" + JusticeText.T("reallife_justice_parole_score", "behaviour", JusticeText.N(JusticeState.PrisonBehaviour), "priors", JusticeText.N(Justice.Priors),
                        "need", JusticeText.N(need)))
                .Add(JusticeText.T("reallife_justice_parole_honest"), JusticeText.T("reallife_justice_parole_honest_hint"))
                .Add(JusticeText.T("reallife_justice_parole_flatter"), JusticeText.T("reallife_justice_parole_flatter_hint"))
                .Add(JusticeText.T("reallife_justice_parole_quiet"), JusticeText.T("reallife_justice_parole_quiet_hint"));
            JusticePanel.Instance.Show(page, choice =>
            {
                int bonus = choice == 0 ? 1 : choice == 1 ? (Random.value < 0.5f ? 3 : -2) : 0;
                int score = JusticeState.PrisonBehaviour - Justice.Priors + bonus;
                bool granted = score >= need;
                _logger?.Info("Justice: parole board: behaviour " + JusticeState.PrisonBehaviour + ", priors " + Justice.Priors + ", bonus " + bonus + ", needed " + need
                              + ": " + (granted ? "granted" : "refused") + ".");
                string said = JusticeText.T(choice == 1 ? (bonus > 0 ? "reallife_justice_parole_flatter_good" : "reallife_justice_parole_flatter_bad")
                    : choice == 0 ? "reallife_justice_parole_honest_done" : "reallife_justice_parole_quiet_done");
                JusticePage result = Page(JusticeFace.Officer, Officer, JusticeFace.Clerk, JusticeText.T("reallife_justice_parole_chair"), 2, JusticeText.T("reallife_justice_parole_chair"),
                        said + "\n\n" + JusticeText.T(granted ? "reallife_justice_parole_granted" : "reallife_justice_parole_refused",
                            "score", JusticeText.N(score), "need", JusticeText.N(need)))
                    .Add(JusticeText.T("reallife_justice_continue"));
                JusticePanel.Instance.Show(result, _ =>
                {
                    if (granted)
                    {
                        _paroled = true;
                        _waiting = false;
                    }
                    else
                    {
                        ShowActivities();
                    }
                });
            });
        }

        // ---------------------------------------------------------------- release

        private int _summaryServed, _summaryOriginal, _summaryRemitted, _summaryEarned, _summaryStudied, _summaryExercised, _summaryProbation;
        private bool _summaryParoled;

        // The state is settled here, in one go, before anything is shown: paid for the work, fed and rested, probation
        // running from today, the term cleared. After this the save no longer says "in prison".
        private void Release()
        {
            int today = TimeHelper.CurrentDay;
            _summaryServed = JusticeState.PrisonServed;
            _summaryOriginal = JusticeState.PrisonOriginal;
            _summaryRemitted = JusticeState.PrisonRemitted;
            _summaryEarned = JusticeState.PrisonEarned;
            _summaryStudied = JusticeState.PrisonStudied;
            _summaryExercised = JusticeState.PrisonExercised;
            _summaryParoled = _paroled;
            int left = Mathf.Max(0, JusticeState.PrisonTotal - JusticeState.PrisonServed);
            JusticeCase c = JusticeState.Find(JusticeState.PrisonCase);
            int probation = Mathf.Max(5, c != null ? c.ProbationImposed : 5) + (_paroled ? left * 2 : 0);
            probation = Mathf.Min(JusticeLaw.ProbationCapDays, probation);
            JusticeState.ProbationUntil = Mathf.Max(JusticeState.ProbationUntil, today + probation);
            _summaryProbation = JusticeState.ProbationUntil;
            if (_summaryEarned > 0 && SaveGameManager.Current != null)
            {
                Crime.Give(_summaryEarned, "reallife_transaction_prison_work");
            }
            Needs(_summaryExercised > 0 ? 100f : 75f, 85f);
            ModState.AddModifier(_summaryExercised > 0 ? ModState.MoodUp : ModState.MoodDown);
            string car = JusticeState.PrisonCar;
            JusticeState.ClearPrison();
            JusticeState.PrisonCar = car;   // kept until the car has been seen to (ReturnCar)
            _director.Touch();
            _logger?.Info("Justice: released from prison after " + _summaryServed + " of " + _summaryOriginal + " days" + (_summaryParoled ? " (parole)" : string.Empty)
                          + ", earned " + _summaryEarned + ", probation to day " + _summaryProbation + ".");
        }

        private void ShowSummary()
        {
            _text.Length = 0;
            _text.Append(JusticeText.T(_summaryParoled ? "reallife_justice_release_parole" : "reallife_justice_release_served",
                "served", JusticeText.N(_summaryServed), "days", JusticeText.N(_summaryOriginal)));
            if (_summaryRemitted > 0)
            {
                _text.Append(' ').Append(JusticeText.T("reallife_justice_release_remitted", "days", JusticeText.N(_summaryRemitted)));
            }
            _text.Append('\n');
            if (_summaryEarned > 0)
            {
                _text.Append('\n').Append(JusticeText.T("reallife_justice_release_earned", "amount", JusticeText.Money(_summaryEarned)));
            }
            if (_summaryStudied > 0)
            {
                _text.Append('\n').Append(JusticeText.T("reallife_justice_release_studied", "days", JusticeText.N(_summaryStudied)));
            }
            if (_summaryExercised > 0)
            {
                _text.Append('\n').Append(JusticeText.T("reallife_justice_release_exercised", "days", JusticeText.N(_summaryExercised)));
            }
            _text.Append('\n').Append(JusticeText.T("reallife_justice_release_probation", "day", JusticeText.N(_summaryProbation)));
            _text.Append('\n').Append(JusticeText.T(string.IsNullOrEmpty(JusticeState.PrisonCar) ? "reallife_justice_release_no_car" : "reallife_justice_release_car"));
            _text.Append("\n\n").Append(JusticeText.T("reallife_justice_release_where", "place", _director.ReleasePlaceName()));
            var page = new JusticePage
            {
                Scene = JusticeScene.Office, Title = JusticeText.T("reallife_justice_release_title"),
                Subtitle = JusticeText.T("reallife_justice_release_subtitle", "day", JusticeText.N(TimeHelper.CurrentDay)),
                Body = _text.ToString()
            };
            page.Add(JusticeText.T("reallife_justice_release_walk"));
            JusticePanel.Instance.Show(page, _ => _waiting = false);
        }

        // The car the player was taken from: handed to whoever looks after such things, else towed to them with a storage charge.
        private void ReturnCar()
        {
            string id = JusticeState.PrisonCar;
            JusticeState.PrisonCar = string.Empty;
            if (string.IsNullOrEmpty(id) || BuildingManager.IsInsideBuilding || SaveGameManager.Current == null)
            {
                return;
            }
            try
            {
                VehicleInstance car = null;
                foreach (VehicleController vehicle in VehicleHelper.AllPlayerVehicles)
                {
                    if (vehicle != null && vehicle.vehicleInstance != null && vehicle.vehicleInstance.id == id)
                    {
                        car = vehicle.vehicleInstance;
                        break;
                    }
                }
                if (car == null)
                {
                    _logger?.Info("Justice: the car left behind is no longer on the street; nothing to return.");
                    return;
                }
                Vector3 near = PlayerHelper.GetPosition();
                if (Justice.Bridges.Transport.ReturnCar(car, near, _summaryServed))
                {
                    return;
                }
                int storage = JusticeLaw.Bound(Prices.At(25) * Mathf.Max(1, _summaryServed), Crime.Cash, 0.10f, 0);
                storage = Mathf.Min(storage, 300);
                if (storage > 0)
                {
                    GameManager.ChangeMoneySafe(-storage, new TransactionInfo("reallife_transaction_impound"), null, null, force: true);
                    JusticeText.Toast(NotificationType.Info, "reallife_justice_release_impound", "amount", JusticeText.Money(storage), 10f);
                }
                VehicleTow.BringNear(car, near);
            }
            catch (Exception e)
            {
                _logger?.Error(e);
            }
        }

        // ---------------------------------------------------------------- shared steps

        // Out of the car (remembered), out of any building, and down on the pavement where they will be let go.
        // Every step is the game's own call and every wait has an end; whatever cannot be done is skipped.
        private IEnumerator TakeOffTheStreet()
        {
            var gm = InstanceBehavior<GameManager>.Instance;
            _leftBehind = null;
            if (gm == null || gm.playerController == null)
            {
                yield break;
            }
            if (gm.selectedVehicle != null)
            {
                _leftBehind = gm.selectedVehicle.vehicleType != null && !gm.selectedVehicle.vehicleType.spawnInPlayerObject ? gm.selectedVehicle.vehicleInstance : null;
                float waited = 0f;
                while (gm.selectedVehicle != null && gm.selectedVehicle.CurrentSpeed > 1f && waited < 8f)
                {
                    waited += Time.unscaledDeltaTime;
                    _hold.Beat();
                    yield return null;
                }
                if (gm.selectedVehicle != null)
                {
                    gm.selectedVehicle.ExitVehicle();
                    yield return new WaitForSecondsRealtime(0.5f);
                    // As after the mod's hospital trip: leaving the car clears the saved active vehicle, but the selected
                    // vehicle only goes when its HUD panel closes.
                    var hud = InstanceBehavior<UIs>.Instance;
                    if (gm.selectedVehicle != null && !PlayerHelper.IsUsingVehicle && hud != null && hud.playerHUD != null && hud.playerHUD.itemPanelUI != null)
                    {
                        hud.playerHUD.itemPanelUI.Toggle(false);
                    }
                }
            }
            if (BuildingManager.IsInsideBuilding)
            {
                InstanceBehavior<BuildingManager>.Instance.ExitFromBuilding(0, playFadeAnimation: false);
                float waited = 0f;
                while ((BuildingManager.IsInsideBuilding || InstanceBehavior<BuildingManager>.Instance.exitingBuilding) && waited < 10f)
                {
                    waited += Time.unscaledDeltaTime;
                    _hold.Beat();
                    yield return null;
                }
                yield return new WaitForSecondsRealtime(0.3f);
            }
            else if (Parking.UndergroundParking.UndergroundParkingManager.IsInsideParking)
            {
                Parking.UndergroundParking.UndergroundParkingManager.ExitParking(playFadeAnimation: false);   // as the game's own hospital trip does
                float waited = 0f;
                while (Parking.UndergroundParking.UndergroundParkingManager.IsInsideParking && waited < 10f)
                {
                    waited += Time.unscaledDeltaTime;
                    _hold.Beat();
                    yield return null;
                }
                yield return new WaitForSecondsRealtime(0.3f);
            }
            // Some ways out of a building (the Hamptons houses) fade back in by themselves: dark again before anything is seen.
            if (_hold.Faded && !UiFader.isFading)
            {
                yield return UiFader.Fade(0.2f);
            }
            _hold.Beat();
            if (PlayerHelper.IsUsingVehicle || gm.selectedVehicle != null)
            {
                _leftBehind = null;   // still at the wheel (the game put them back in): no car was left anywhere
                yield break;
            }
            if (BuildingManager.IsInsideBuilding || Parking.UndergroundParking.UndergroundParkingManager.IsInsideParking || !_director.ReleaseSpot(out Vector3 spot))
            {
                yield break;   // stays where they are: the time passes all the same
            }
            PlayerController player = gm.playerController;
            if (player.Character != null && player.Character.navmeshAgent != null && player.Character.navmeshAgent.isOnNavMesh)
            {
                player.ResetNavigation();
                if (player.Character.navmeshAgent.Warp(spot))
                {
                    _logger?.Info("Justice: the player is put down at " + _director.ReleasePlaceName() + ".");
                }
            }
        }

        // The game's own time skip to a moment, with energy and hunger held up while it runs. Ends a second after the clock stops:
        // the game puts its own interface and speed back in that second, and nothing of ours must be in its way.
        private IEnumerator Skip(Timestamp until, string infoKey)
        {
            UIs uis = InstanceBehavior<UIs>.Instance;
            if (uis == null || uis.timeMachine == null || SaveGameManager.Current == null || until.GetTotalMinutes() <= TimeHelper.NowInMinutes() + 1f)
            {
                yield break;
            }
            _hold.Rest(true);
            _hold.PauseHandedOver();
            uis.timeMachine.StartTimeMachine(until, disableCancel: true, infoKey);
            float waited = 0f, nextNeeds = 0f;
            while (uis != null && uis.timeMachine != null && uis.timeMachine.isRunning && waited < SkipMost)
            {
                waited += Time.unscaledDeltaTime;
                _hold.Beat();
                if (waited >= nextNeeds)
                {
                    nextNeeds = waited + 0.25f;
                    var save = SaveGameManager.Current;
                    if (save != null)
                    {
                        if (save.Energy < NeedsFloor)
                        {
                            save.Energy = NeedsFloor;
                        }
                        if (save.Hunger < NeedsFloor)
                        {
                            save.Hunger = NeedsFloor;
                        }
                    }
                }
                yield return null;
            }
            if (uis != null && uis.timeMachine != null && uis.timeMachine.isRunning)
            {
                _logger?.Warn("Justice: a time skip took over " + SkipMost + " s and was stopped.");
                uis.timeMachine.StopTimeMachine(0f);
            }
            // At least 1.3 s, and until the game's own stop routine has finished (it resets the game speed last); 4 s at most.
            float settle = 0f;
            while (settle < 1.3f || (settle < 4f && uis != null && uis.timeMachine != null && uis.timeMachine.isBlockingUi))
            {
                settle += Time.unscaledDeltaTime;
                _hold.Beat();
                yield return null;
            }
        }

        private static void Needs(float energy, float hunger)
        {
            var save = SaveGameManager.Current;
            if (save == null)
            {
                return;
            }
            if (save.Energy < energy)
            {
                save.Energy = energy;   // as the game's own hospital stay sets it
            }
            if (save.Hunger < hunger)
            {
                EnergyHelper.SetCurrentHunger(hunger);
            }
        }
    }
}
