using System;
using System.Collections.Generic;
using RealLifeOverhaul.Population;

namespace RealLifeOverhaul.Flare
{
    /// <summary>One of the city's voices as the feed shows them: their resident's district, age, sex and what they are like (FlareLiveRules.ResidentCitizen names them).</summary>
    public struct CityVoice
    {
        public int Person;           // the resident's id, 0 for none
        public int District;         // the population's district, -1 none
        public int Age;
        public bool Female;
        public FlarePersona Persona;
    }

    /// <summary>
    /// Flare in the city by rule, the voices (the brief's first step: "the 400 citizens become residents who post, the most active voices, generated
    /// from the population"): each of the feed's four hundred people ("c0" to "c399") is a resident, drawn from the city's adults with the young and
    /// students more likely; a voice whose resident died or left the city is somebody new. What they are like comes from who they are: a student,
    /// a pensioner, a parent, or by their household's tastes. Game-free. COST: on the first day a draw for each voice; then a look at each a day.
    /// </summary>
    public static partial class FlareCityRules
    {
        private const int SaltVoiceDraw = 61, SaltVoiceAccept = 62;

        /// <summary>
        /// The voices kept up: each slot with a resident who is gone (or never was) is given an adult of the city not yet a voice. The adults index must
        /// be the day's. How many changed.
        /// </summary>
        public static int Voices(FlareCityState c, PopulationState s, int day, int[] personSlot)
        {
            int adults = c.AdultList.Length > 0 && c.AdultStart.Length > 0 ? c.AdultStart[c.AdultStart.Length - 1] : 0;
            if (adults <= 0)
            {
                return 0;
            }
            var taken = new HashSet<int>();
            for (int v = 0; v < c.Voices.Length; v++)
            {
                if (Slot(personSlot, s, c.Voices[v]) >= 0)
                {
                    taken.Add(c.Voices[v]);
                }
            }
            int changed = 0;
            for (int v = 0; v < c.Voices.Length; v++)
            {
                int p = Slot(personSlot, s, c.Voices[v]);
                if (p >= 0 && Age(s, p, day) >= FlareCityCatalogue.AdultAge)
                {
                    continue;
                }
                int pick = -1;
                for (int t = 0; t < FlareCityCatalogue.VoiceTriesMost && pick < 0; t++)
                {
                    int q = c.AdultList[Millionth(c.Seed, v, SaltVoiceDraw, day * 64 + t) % adults];
                    int id = s.People.Id[q];
                    if (id == 0 || taken.Contains(id))
                    {
                        continue;
                    }
                    if (Mille(c.Seed, id, SaltVoiceAccept, t) * VoiceLeanMost / 1000 < VoiceLean(s, q, day))
                    {
                        pick = q;
                    }
                }
                for (int k = 0; pick < 0 && k < adults; k++)
                {
                    int q = c.AdultList[(Millionth(c.Seed, v, SaltVoiceDraw, day) + k) % adults];
                    pick = taken.Contains(s.People.Id[q]) ? -1 : q;   // unlucky every draw: the next adult in order who is not a voice
                }
                if (pick < 0)
                {
                    break;   // fewer adults than voices: the rest stay empty
                }
                c.Voices[v] = s.People.Id[pick];
                taken.Add(c.Voices[v]);
                changed++;
            }
            return changed;
        }

        private const int VoiceLeanMost = FlareCityCatalogue.VoiceYoungLean * FlareCityCatalogue.VoiceStudentLean / 1000;

        // Thousandths: how likely a resident is to be one who posts most (the young twice, students half again).
        private static int VoiceLean(PopulationState s, int p, int day)
        {
            int age = Age(s, p, day);
            int lean = age >= 18 && age < 40 ? FlareCityCatalogue.VoiceYoungLean : 1000;
            if (s.People.Status[p] == (byte)Status.Student)
            {
                lean = lean * FlareCityCatalogue.VoiceStudentLean / 1000;
            }
            return lean;
        }

        /// <summary>A voice as the feed shows them, from their resident; Person 0 when the slot has none.</summary>
        public static CityVoice VoiceOf(FlareCityState c, PopulationState s, int v, int day, int[] personSlot)
        {
            int p = v >= 0 && v < c.Voices.Length ? Slot(personSlot, s, c.Voices[v]) : -1;
            if (p < 0)
            {
                return new CityVoice { District = -1 };
            }
            return new CityVoice { Person = s.People.Id[p], District = DistrictOf(s, p), Age = Age(s, p, day), Female = s.People.Female[p] != 0, Persona = PersonaOf(s, p, day) };
        }

        /// <summary>What a resident is like on the feed: a student, a pensioner, a parent of a child at home, or by their household's tastes and work.</summary>
        public static FlarePersona PersonaOf(PopulationState s, int p, int day)
        {
            var status = (Status)s.People.Status[p];
            int age = Age(s, p, day);
            if (status == Status.Student || (status == Status.Pupil && age >= FlareCityCatalogue.AdultAge))
            {
                return FlarePersona.Student;
            }
            if (status == Status.Retired || age >= PopulationCatalogue.RetireAge)
            {
                return FlarePersona.Retiree;
            }
            int h = s.People.Household[p];
            if (h < 0 || !s.Households.Alive(h))
            {
                return FlarePersona.Commuter;
            }
            var role = (Role)s.People.Role[p];
            if (role == Role.Head || role == Role.Partner)
            {
                for (int m = s.Households.First[h]; m >= 0; m = s.People.Next[m])
                {
                    if ((Role)s.People.Role[m] == Role.Child && Age(s, m, day) < PopulationCatalogue.AdultLeast)
                    {
                        return FlarePersona.Parent;
                    }
                }
            }
            uint tastes = s.Households.Tastes[h];
            if (Cares(tastes, (int)Spend.NightLife) > 0)
            {
                return FlarePersona.NightOwl;
            }
            if (Cares(tastes, (int)Spend.Gym) > 0)
            {
                return FlarePersona.Sporty;
            }
            if (Cares(tastes, (int)Spend.EatingOut) > 0 || Cares(tastes, (int)Spend.Coffee) > 0)
            {
                return FlarePersona.Foodie;
            }
            return status == Status.Worker ? FlarePersona.Commuter : FlarePersona.Grumpy;
        }

        /// <summary>The id-to-slot table of the population's people (rebuilt at each pass; a slot is checked against the id before use).</summary>
        public static int[] PersonSlots(PopulationState s, int[] into)
        {
            int most = Math.Max(s.NextPerson, 1);
            if (into == null || into.Length < most)
            {
                into = new int[most + most / 8 + 64];
            }
            for (int i = 0; i < into.Length; i++)
            {
                into[i] = -1;
            }
            for (int p = 0; p < s.People.Count; p++)
            {
                int id = s.People.Id[p];
                if (id > 0 && id < into.Length)
                {
                    into[id] = p;
                }
            }
            return into;
        }
    }
}
