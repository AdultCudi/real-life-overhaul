using System;
using System.Collections.Generic;
using BAModAPI;
using Helpers;
using UI.Notification;
using UnityEngine;
using Random = UnityEngine.Random;

namespace RealLifeOverhaul
{
    /// <summary>
    /// The rules of home life and health: how the three needs move with what the player is
    /// doing, what that does to their mood (the mod's own happiness modifiers), fitness, the
    /// body, sleep, and being ill. Nothing here runs by itself: the director calls
    /// <see cref="Advance"/> once a second with the game minutes that went by, <see cref="Hourly"/>
    /// on the hour and <see cref="NewDay"/> at midnight.
    ///
    /// Illness goes through Life's sickness, not beside it: a cold, the flu and food poisoning
    /// are Life's "sick" (its mood modifier, its energy drain, its doctor on the phone, the
    /// hospital curing it) with a name and a course of their own, and a sickness a life event
    /// hands out is taken up here as a cold. Back pain and burnout are the mod's own two
    /// modifiers. One illness at a time, and none for three days after any.
    /// </summary>
    public static class HomeHealth
    {
        private const string IllDrain = "rlo-home-ill";
        private const string FitDrain = "rlo-home-fit";
        private const string EffortDrain = "rlo-home-effort";

        public static IModLogger Logger;

        private static float _fitDrain = float.NaN;
        private static float _illDrain;
        private static float _effortDrain;
        private static float _noticedAt = -1000f;
        private static bool _bodyBroken;

        public static bool Ill => HomeState.Ill != Illness.None;
        public static bool SickNoteActive => HomeState.Loaded && HomeState.SickNoteUntil > 0f && TimeHelper.NowInMinutes() < HomeState.SickNoteUntil;
        private static float Easy => Settings.HomeEasy ? HomeBalance.Easy : 1f;

        /// <summary>A load or the city going: nothing of ours is left draining energy.</summary>
        public static void Reset()
        {
            SetDrain(IllDrain, ref _illDrain, 0f);
            SetDrain(EffortDrain, ref _effortDrain, 0f);
            if (!float.IsNaN(_fitDrain))
            {
                EnergyHelper.RemoveEnergySpender(FitDrain);
            }
            _fitDrain = float.NaN;
            _noticedAt = -1000f;
        }

        // ---------------------------------------------------------------- minute by minute

        /// <summary>The needs move with what the player is doing. Called once a second; <paramref name="minutes"/> is game time.</summary>
        public static void Advance(float minutes, Doing doing, bool atHome, bool onFootOutside, float boost)
        {
            if (minutes <= 0f)
            {
                return;
            }
            float hours = minutes / 60f;
            bool asleep = doing == Doing.Sleeping;

            if (Settings.HomeNeeds)
            {
                float hygiene = asleep ? HomeBalance.HygieneAsleep : HomeBalance.HygieneAwake;
                float stress = asleep ? HomeBalance.StressSleep : HomeBalance.StressAwake;
                switch (doing)
                {
                    case Doing.Showering: hygiene = HomeBalance.HygieneShower; stress = HomeBalance.StressShower; break;
                    case Doing.Washing: hygiene = HomeState.Hygiene < HomeBalance.SinkMost ? HomeBalance.HygieneSink : 0f; break;
                    case Doing.Screen: stress = HomeBalance.StressScreen; break;
                    case Doing.Reading: stress = HomeBalance.StressReading; break;
                    case Doing.Resting: stress = HomeBalance.StressResting; break;
                    case Doing.Workout: hygiene = HomeBalance.HygieneWorkout; stress = HomeBalance.StressWorkout; break;
                    case Doing.Exercise: hygiene = HomeBalance.HygieneWorkout * 0.7f; stress = HomeBalance.StressExercise; break;
                    case Doing.Sport: hygiene = HomeBalance.HygieneWorkout * 0.5f; stress = HomeBalance.StressSport; break;
                    case Doing.Jogging: hygiene = HomeBalance.HygieneJog; stress = HomeBalance.StressJog * boost; break;
                    case Doing.Working: stress = HomeBalance.StressWorking; break;
                    case Doing.Studying: stress = HomeBalance.StressStudying; break;
                    case Doing.HomeRoutine: stress = HomeBalance.StressRoutine; break;
                }
                if (doing == Doing.Nothing || doing == Doing.Toilet)
                {
                    if (JobDirector.OnShift) stress += HomeBalance.StressJobShift;
                    if (ModState.IsSick || Ill) stress += HomeBalance.StressSick;
                    if (SaveGameManager.Current.Hunger < 15f) stress += HomeBalance.StressHungry;
                    if (atHome && HomeState.Tidy < HomeBalance.MessLow) stress += HomeBalance.StressMess;
                    if (HomeState.SleepDebt > HomeBalance.DebtHigh) stress += HomeBalance.StressTired;
                }
                bool wet = onFootOutside && BigAmbitions.DayNightCycle.RainHelper.AreRainDropsFalling && !PlayerHelper.IsCarryingAnUmbrella();
                if (wet)
                {
                    hygiene += HomeBalance.HygieneRain;
                }
                // What wears a need down is halved on easy; what restores it is not.
                HomeState.Hygiene = HomeState.Clamp(HomeState.Hygiene + hygiene * hours * (hygiene < 0f ? Easy : 1f), 0f, 100f);
                HomeState.Stress = HomeState.Clamp(HomeState.Stress + stress * hours * (stress > 0f ? Easy : 1f), 0f, 100f);
                if (!asleep && HomeTidy.HasHome)
                {
                    HomeTidy.Change((atHome ? HomeBalance.TidyAtHome : HomeBalance.TidyAway) * hours * Easy);
                }
                if (wet && Settings.HomeIllness)
                {
                    float before = HomeState.WetMinutes;
                    HomeState.WetMinutes += minutes;
                    if (before < HomeBalance.WetLimit && HomeState.WetMinutes >= HomeBalance.WetLimit && Random.value < HomeBalance.ColdFromRain * Easy)
                    {
                        Catch(Illness.Cold, "reallife_health_cause_rain", false);
                    }
                }
            }

            // Sleep debt: so much owed for every hour up, an hour paid off for every hour asleep (Slept settles the quality).
            if (Settings.HomeSleep && !asleep)
            {
                HomeState.SleepDebt = HomeState.Clamp(HomeState.SleepDebt + HomeBalance.DebtPerWakingHour * hours * Easy, 0f, HomeBalance.DebtMost);
            }
            if (doing == Doing.Working || JobDirector.OnShift)
            {
                HomeState.WorkHours += hours;
            }

            if (Settings.HomeFitness)
            {
                switch (doing)
                {
                    case Doing.Workout: Trained(minutes, HomeBalance.FitWorkout, true); break;
                    case Doing.Exercise: Trained(minutes, HomeBalance.FitExercise, true); break;
                    case Doing.Sport: Trained(minutes, HomeBalance.FitSport, false); break;
                    case Doing.Jogging: Trained(minutes, HomeBalance.FitJog * boost, false); break;
                }
            }
            float effort = doing == Doing.Jogging ? HomeBalance.JogDrain : doing == Doing.Exercise ? HomeBalance.ExerciseDrain
                : doing == Doing.HomeRoutine ? HomeBalance.RoutineDrain : 0f;
            if (effort > 0f && SaveGameManager.Current.Energy <= HomeBalance.EnergyFloor)
            {
                effort = 0f;   // nothing of ours takes the player down to fainting
            }
            SetDrain(EffortDrain, ref _effortDrain, effort);
        }

        // Exercise of any kind: fitness with diminishing returns over the day, and what it will do to the body.
        private static void Trained(float minutes, float perHour, bool builds)
        {
            float hours = minutes / 60f;
            float before = HomeState.ExerciseToday;
            float returns = 1f / (1f + before / 60f);          // the second hour of a day is worth half the first
            float gain = perHour * hours * returns * (Ill ? 0.5f : 1f);
            HomeState.Fitness = HomeState.Clamp(HomeState.Fitness + gain, 0f, 100f);
            HomeState.ExerciseToday = before + minutes;
            HomeState.LastWorkoutDay = TimeHelper.CurrentDay;
            if (builds)
            {
                HomeState.ShapeDue += HomeBalance.StrengthPerHour * hours;
            }
            HomeState.FatDue += HomeBalance.FatPerActiveHour * hours;
            if (Settings.HomeIllness && before < 60f && HomeState.ExerciseToday >= 60f && HomeState.Fitness < HomeBalance.FitLow
                && Random.value < HomeBalance.BackFromStrain * Easy)
            {
                Catch(Illness.BackPain, "reallife_health_cause_strain", false);
            }
        }

        // ---------------------------------------------------------------- on the hour

        public static void Hourly(bool atHome, bool inSomeoneElsesPlace)
        {
            float now = TimeHelper.NowInMinutes();

            // ---- being ill
            if (Settings.HomeIllness)
            {
                if (ModState.IsSick && HomeState.Ill == Illness.None)
                {
                    // A life event (or anything else of the mod's) made the player sick: it is a cold from here on.
                    HomeState.Ill = Illness.Cold;
                    HomeState.IllSince = now;
                    HomeState.IllTreated = false;
                }
                if (HomeBalance.Infectious(HomeState.Ill))
                {
                    if (!ModState.IsSick)
                    {
                        Over(false);   // Life has already said so (it ran out, the doctor on the phone, the hospital)
                    }
                    else
                    {
                        HomeState.IllUntil = ModState.SickUntil;
                    }
                }
                else if (Ill && now >= HomeState.IllUntil)
                {
                    if (HomeState.Ill == Illness.Burnout && HomeState.Stress > 60f && now < HomeState.IllSince + 7f * 24f * 60f)
                    {
                        HomeState.IllUntil = now + 6f * 60f;   // it does not lift while the stress is still there
                        ModState.AddModifier(ModState.Burnout, 6);
                    }
                    else
                    {
                        Over(true);
                    }
                }
                if (HomeState.Stress >= HomeBalance.BurnoutAt)
                {
                    HomeState.StressHighHours += 1f;
                    if (HomeState.StressHighHours >= HomeBalance.BurnoutHours / Easy && Catch(Illness.Burnout, "reallife_health_cause_overwork", false))
                    {
                        HomeState.StressHighHours = 0f;
                    }
                }
                else if (HomeState.Stress < HomeBalance.StressHigh)
                {
                    HomeState.StressHighHours = 0f;
                }
            }
            else if (Ill)
            {
                Over(false);
            }
            SetDrain(IllDrain, ref _illDrain, HomeBalance.Drain(HomeState.Ill) * (HomeState.IllTreated ? 0.5f : 1f));

            // ---- fitness as stamina: a fit player tires more slowly at everything
            float fit = Settings.HomeFitness ? Mathf.Lerp(HomeBalance.FitDrainWorst, HomeBalance.FitDrainBest, HomeState.Fitness / 100f) : 0f;
            fit = Mathf.Round(fit * 1000f) / 1000f;
            if (float.IsNaN(_fitDrain) || !Mathf.Approximately(fit, _fitDrain))
            {
                EnergyHelper.RemoveEnergySpender(FitDrain);
                if (!Mathf.Approximately(fit, 0f))
                {
                    EnergyHelper.AddEnergySpender(FitDrain, fit);
                }
                _fitDrain = fit;
            }

            // ---- mood
            bool needs = Settings.HomeNeeds;
            Hold(ModState.Unwashed, needs && HomeState.Hygiene < HomeBalance.HygieneLow);
            Hold(ModState.Stressed, needs && HomeState.Stress > HomeBalance.StressHigh);
            Hold(ModState.Relaxed, needs && HomeState.Stress < HomeBalance.StressCalm);
            Hold(ModState.MessyHome, needs && atHome && HomeState.Tidy < HomeBalance.MessLow);
            Hold(ModState.CosyHome, needs && atHome && HomeState.Tidy >= HomeBalance.TidyCosy);
            Hold(ModState.ShortOfSleep, Settings.HomeSleep && HomeState.SleepDebt > HomeBalance.DebtHigh);
            Hold(ModState.InShape, Settings.HomeFitness && HomeState.Fitness >= HomeBalance.FitHigh);
            float diet = HomeState.DietScore;
            Hold(ModState.GoodDiet, Settings.HomeFitness && diet >= HomeBalance.DietGood);
            Hold(ModState.PoorDiet, Settings.HomeFitness && diet <= HomeBalance.DietPoor);

            // ---- other people
            if (needs && inSomeoneElsesPlace && HomeState.Hygiene < HomeBalance.HygieneNotice && now - _noticedAt > 6f * 60f)
            {
                _noticedAt = now;
                Notifications.Show(NotificationType.Warning, "reallife_home_people_notice", null, 8f);
                ModState.AddModifier(ModState.MoodDown);
            }
        }

        // Keeps one of the mod's modifiers on for as long as its cause lasts, and takes it off when the cause goes.
        private static void Hold(string type, bool on)
        {
            List<HappinessModifierData> list = SaveGameManager.Current.happinessModifiers;
            HappinessModifierData found = null;
            for (int i = 0; list != null && i < list.Count; i++)
            {
                if (list[i].type == type)
                {
                    found = list[i];
                    break;
                }
            }
            if (on)
            {
                if (found == null)
                {
                    ModState.AddModifier(type, 12);
                }
                else if (found.hoursLeft <= 3)
                {
                    found.hoursLeft = 12;   // renewed quietly: adding it again would play the game's "happier" face each time
                }
            }
            else if (found != null)
            {
                ModState.RemoveModifier(type);
            }
        }

        private static void SetDrain(string id, ref float current, float wanted)
        {
            if (Mathf.Approximately(current, wanted))
            {
                return;
            }
            try
            {
                EnergyHelper.RemoveEnergySpender(id);
                if (wanted > 0f)
                {
                    EnergyHelper.AddEnergySpender(id, wanted);
                }
                current = wanted;
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }

        // ---------------------------------------------------------------- at midnight

        public static void NewDay(int passed)
        {
            int day = TimeHelper.CurrentDay;
            HomeState.WorkHours *= 0.5f;
            if (Settings.HomeFitness)
            {
                if (day - HomeState.LastWorkoutDay >= 3)
                {
                    HomeState.Fitness = HomeState.Clamp(HomeState.Fitness + HomeBalance.FitIdlePerDay * passed * Easy, 0f, 100f);
                }
                float diet = HomeState.DietScore;
                if (diet <= HomeBalance.DietPoor && day - HomeState.LastWorkoutDay >= 2)
                {
                    HomeState.FatDue += HomeBalance.FatPerLazyDay * passed;
                }
                else if (diet >= HomeBalance.DietGood)
                {
                    HomeState.FatDue += HomeBalance.FatPerGoodDay * passed;
                }
            }
            if (!Settings.HomeIllness || Ill || ModState.IsSick || day < 4)
            {
                return;
            }
            float flu = HomeBalance.FluADay * (HomeState.DietScore <= HomeBalance.DietPoor ? 2f : 1f) * (HomeState.SleepDebt > HomeBalance.DebtHigh ? 2f : 1f);
            if (Random.value < flu * Easy)
            {
                Catch(Illness.Flu, HomeState.DietScore <= HomeBalance.DietPoor ? "reallife_health_cause_diet" : "reallife_health_cause_going_round", false);
            }
            else if (HomeState.SleepDebt > HomeBalance.DebtHigh && Random.value < HomeBalance.ColdWhenRunDown * Easy)
            {
                Catch(Illness.Cold, "reallife_health_cause_no_sleep", false);
            }
            else if (HomeState.Hygiene < HomeBalance.HygieneNotice && Random.value < HomeBalance.ColdWhenUnwashed * Easy)
            {
                Catch(Illness.Cold, "reallife_health_cause_unwashed", false);
            }
        }

        // ---------------------------------------------------------------- sleep

        /// <summary>
        /// A sleep ended. Works out how good it was (the bed, the hour, the state of the flat and of the
        /// sleeper), pays off sleep debt accordingly, sets the morning's mood, lets rest work on an
        /// illness and shows on the body what the last days did to it. Returns the quality, 0..100.
        /// </summary>
        public static int Slept(float hours, int bedQuality, bool inCar, int bedHour, bool atHome)
        {
            float score = 55f;
            score += inCar ? -25f : (Mathf.Clamp(bedQuality, 0, 100) - 50f) * 0.3f;
            if (bedHour >= HomeBalance.LateFrom && bedHour < HomeBalance.LateTo) score -= 15f;        // far too late
            else if (bedHour >= 8 && bedHour < 18) score -= 10f;                                       // the day's noise
            if (atHome && HomeState.Tidy < HomeBalance.MessLow) score -= 8f;
            if (HomeState.Stress > HomeBalance.StressHigh) score -= 10f;
            if (SaveGameManager.Current.Hunger < 20f) score -= 6f;
            if (Ill || ModState.IsSick) score -= 10f;
            score += hours >= 7f ? 10f : hours >= 6f ? 0f : hours >= 5f ? -8f : -20f;
            score = Mathf.Clamp(score, 0f, 100f);

            HomeState.SleepDebt = HomeState.Clamp(HomeState.SleepDebt - hours * (0.7f + 0.6f * score / 100f), 0f, HomeBalance.DebtMost);
            HomeState.LastSleepHours = hours;
            HomeState.LastWokeAt = TimeHelper.NowInMinutes();
            if (hours < 3f)
            {
                return (int)score;   // a nap: the debt, and nothing else
            }
            HomeState.SleepQuality = score >= 68f ? 3 : score >= 45f ? 2 : 1;
            ModState.RemoveModifier(ModState.WellRested);
            ModState.RemoveModifier(ModState.Groggy);
            if (HomeState.SleepQuality == 3)
            {
                ModState.AddModifier(ModState.WellRested);
            }
            else if (HomeState.SleepQuality == 1)
            {
                ModState.AddModifier(ModState.Groggy);
            }
            if (Ill && hours >= 6f)
            {
                Shorten(HomeBalance.RestCure * 60f);
            }
            if (Settings.HomeIllness && inCar && Random.value < HomeBalance.BackFromCar * Easy)
            {
                Catch(Illness.BackPain, "reallife_health_cause_car", false);
            }
            if (Settings.HomeBody)
            {
                ShowOnBody();
            }
            Logger?.Info("Home: slept " + hours.ToString("F1") + " h, quality " + (int)score + " (bed " + bedQuality + (inCar ? ", in the car" : string.Empty)
                         + ", to bed at " + bedHour + "h), debt now " + HomeState.SleepDebt.ToString("F1") + " h.");
            return (int)score;
        }

        // What exercise and food did to the body, a little at a time and only on waking: one rebuild of the player's look at most.
        private static void ShowOnBody()
        {
            if (_bodyBroken)
            {
                return;
            }
            float strength = Mathf.Clamp(HomeState.ShapeDue, -HomeBalance.ShapePerNight, HomeBalance.ShapePerNight);
            float fat = Mathf.Clamp(HomeState.FatDue, -HomeBalance.ShapePerNight, HomeBalance.ShapePerNight);
            strength = Mathf.Clamp(HomeState.Strength + strength, -HomeBalance.ShapeMost, HomeBalance.ShapeMost) - HomeState.Strength;
            fat = Mathf.Clamp(HomeState.Fat + fat, -HomeBalance.ShapeMost, HomeBalance.ShapeMost) - HomeState.Fat;
            if (Mathf.Abs(strength) < 0.004f && Mathf.Abs(fat) < 0.004f)
            {
                return;   // not enough to see yet: it keeps adding up
            }
            try
            {
                PlayerController player = PlayerHelper.PlayerController;
                AppearanceSetter look = player != null && player.Character != null ? player.Character.appearanceSetter : null;
                if (look == null || look.data == null)
                {
                    return;
                }
                float wasStrength = look.data.strength, wasFat = look.data.fatness;
                look.data.strength = Mathf.Clamp01(wasStrength + strength);
                look.data.fatness = Mathf.Clamp01(wasFat + fat);
                HomeState.Strength += look.data.strength - wasStrength;
                HomeState.Fat += look.data.fatness - wasFat;
                HomeState.ShapeDue -= strength;
                HomeState.FatDue -= fat;
                look.UpdateVisuals();
                Logger?.Info("Home: body strength " + wasStrength.ToString("F3") + " -> " + look.data.strength.ToString("F3") + ", fatness "
                             + wasFat.ToString("F3") + " -> " + look.data.fatness.ToString("F3") + ".");
            }
            catch (Exception e)
            {
                _bodyBroken = true;
                Logger?.Error(e);
                Logger?.Warn("Home: the body could not be updated; body changes are off for this session.");
            }
        }

        // ---------------------------------------------------------------- illness

        /// <summary>The player falls ill, if they are well and not just over something. A test forces it.</summary>
        public static bool Catch(Illness ill, string causeKey, bool forced)
        {
            if (ill == Illness.None || LifeEvents.Instance == null)
            {
                return false;
            }
            if (!forced && (Ill || ModState.IsSick || TimeHelper.CurrentDay < HomeState.ImmuneUntilDay || !Settings.HomeIllness))
            {
                return false;
            }
            if (Ill)
            {
                Over(false);
            }
            float now = TimeHelper.NowInMinutes();
            int hours = HomeBalance.Hours(ill, Random.value);
            HomeState.Ill = ill;
            HomeState.IllSince = now;
            HomeState.IllUntil = now + hours * 60f;
            HomeState.IllTreated = false;
            if (HomeBalance.Infectious(ill))
            {
                LifeEvents.Instance.MakeSick(hours, null);   // Life's sickness: its modifier, its drain, its doctor
                if (ill == Illness.FoodPoisoning)
                {
                    EnergyHelper.SetCurrentHunger(Mathf.Max(SaveGameManager.Current.Hunger - 25f, 5f));
                }
            }
            else
            {
                ModState.AddModifier(ill == Illness.BackPain ? ModState.BackPain : ModState.Burnout, hours);
            }
            SetDrain(IllDrain, ref _illDrain, HomeBalance.Drain(ill));
            var data = new Dictionary<string, string>(3)
            {
                { "illness", JobText.T(NameKey(ill)) },
                { "cause", JobText.T(causeKey ?? "reallife_health_cause_unknown") },
                { "days", Days(hours * 60f) }
            };
            Notifications.Show(NotificationType.Warning, "reallife_health_caught", data, 12f);
            Logger?.Info("Health: " + ill + " for " + hours + " h (" + (causeKey ?? "no cause") + (forced ? ", test" : string.Empty) + ").");
            return true;
        }

        /// <summary>Medicine or a doctor: half of what is left, half the symptoms.</summary>
        public static bool Treat()
        {
            if (!Ill || HomeState.IllTreated)
            {
                return false;
            }
            HomeState.IllTreated = true;
            float now = TimeHelper.NowInMinutes();
            float left = Mathf.Max(60f, (HomeState.IllUntil - now) * 0.5f);
            SetUntil(now + left);
            SetDrain(IllDrain, ref _illDrain, HomeBalance.Drain(HomeState.Ill) * 0.5f);
            return true;
        }

        /// <summary>Rest (a night's sleep, stretching a bad back) takes time off what is left.</summary>
        public static void Shorten(float minutes)
        {
            if (Ill)
            {
                SetUntil(Mathf.Max(TimeHelper.NowInMinutes() + 30f, HomeState.IllUntil - minutes));
            }
        }

        private static void SetUntil(float until)
        {
            HomeState.IllUntil = until;
            int hours = Mathf.Max(1, Mathf.CeilToInt((until - TimeHelper.NowInMinutes()) / 60f));
            if (HomeBalance.Infectious(HomeState.Ill))
            {
                ModState.SickUntil = until;
                ModState.AddModifier(ModState.Sick, hours);
            }
            else
            {
                ModState.AddModifier(HomeState.Ill == Illness.BackPain ? ModState.BackPain : ModState.Burnout, hours);
            }
        }

        /// <summary>Well again, by whatever road.</summary>
        public static void Over(bool tell)
        {
            Illness was = HomeState.Ill;
            HomeState.Ill = Illness.None;
            HomeState.IllUntil = 0f;
            HomeState.IllTreated = false;
            HomeState.ImmuneUntilDay = TimeHelper.CurrentDay + HomeBalance.ImmuneDays;
            ModState.RemoveModifier(ModState.BackPain);
            ModState.RemoveModifier(ModState.Burnout);
            SetDrain(IllDrain, ref _illDrain, 0f);
            if (HomeBalance.Infectious(was) && ModState.IsSick && LifeEvents.Instance != null)
            {
                LifeEvents.Instance.Cure(null);
            }
            if (tell && was != Illness.None)
            {
                var data = new Dictionary<string, string>(1) { { "illness", JobText.T(NameKey(was)) } };
                Notifications.Show(NotificationType.Success, "reallife_health_over", data, 8f);
            }
            if (was != Illness.None)
            {
                Logger?.Info("Health: over " + was + ".");
            }
        }

        public static string NameKey(Illness ill)
        {
            switch (ill)
            {
                case Illness.Cold: return "reallife_health_ill_cold";
                case Illness.Flu: return "reallife_health_ill_flu";
                case Illness.FoodPoisoning: return "reallife_health_ill_food";
                case Illness.BackPain: return "reallife_health_ill_back";
                case Illness.Burnout: return "reallife_health_ill_burnout";
                default: return "reallife_health_ill_none";
            }
        }

        /// <summary>What it does, in a sentence: for the doctor and the phone page.</summary>
        public static string SymptomKey(Illness ill)
        {
            switch (ill)
            {
                case Illness.Cold: return "reallife_health_symptom_cold";
                case Illness.Flu: return "reallife_health_symptom_flu";
                case Illness.FoodPoisoning: return "reallife_health_symptom_food";
                case Illness.BackPain: return "reallife_health_symptom_back";
                case Illness.Burnout: return "reallife_health_symptom_burnout";
                default: return "reallife_health_symptom_none";
            }
        }

        /// <summary>True if the player is in no state for hard exercise; stretching is always allowed.</summary>
        public static bool TooIllToTrain => HomeState.Ill == Illness.Flu || HomeState.Ill == Illness.BackPain || HomeState.Ill == Illness.FoodPoisoning;

        public static string Days(float minutes)
        {
            float days = Mathf.Max(0f, minutes) / (24f * 60f);
            return days < 0.95f ? Mathf.Max(1, Mathf.RoundToInt(days * 24f)) + " h" : days.ToString("F1") + " d";
        }
    }
}
