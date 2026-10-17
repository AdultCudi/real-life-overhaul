using System;
using Entities;
using Helpers;
using UnityEngine;
using Random = UnityEngine.Random;

namespace RealLifeOverhaul
{
    // A day inside: the parole board on its day, then work, exercise or study, the yard, and something that happens.
    public sealed partial class PrisonStay
    {
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

        private readonly PrisonEvent[] _unseen = new PrisonEvent[Events.Length];

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

        private void ShowNight(string outcome)
        {
            JusticePage page = Page(JusticeFace.Officer, Officer, JusticeFace.Inmate, Cellmate, -1, null, outcome + "\n\n" + JusticeText.T("reallife_justice_prison_lights_out"))
                .Add(JusticeText.T("reallife_justice_prison_sleep"));
            JusticePanel.Instance.Show(page, _ => _waiting = false);
        }

        /// <summary>The parole board: behaviour, less the record, plus how the player speaks to it.</summary>
        public void ShowParole()
        {
            JusticeState.PrisonParoleHeard = true;
            int served = JusticeState.PrisonServed;
            int need = Mathf.Max(2, served);
            string chair = JusticeText.T("reallife_justice_parole_chair");
            JusticePage page = Page(JusticeFace.Officer, Officer, JusticeFace.Clerk, chair, 2, chair,
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
                JusticePage result = Page(JusticeFace.Officer, Officer, JusticeFace.Clerk, chair, 2, chair,
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
    }
}
