using System;
using BigAmbitions.Characters;
using Character;
using UnityEngine;
using Random = UnityEngine.Random;

namespace RealLifeOverhaul
{
    /// <summary>The pool's shelf: who is handed out for a look, and looks made ready one at a time between outings.</summary>
    public sealed partial class CityPeople
    {
        // Somebody on the shelf dressed for the part: sooner one whose look is not due a change, then whoever came back first.
        private CityPerson Ready(CityLook look)
        {
            CityPerson ready = null;
            for (int i = _people.Count - 1; i >= 0; i--)
            {
                CityPerson person = _people[i];
                if (!person.Valid)
                {
                    _people.RemoveAt(i);   // destroyed with a scene the mod did not unload
                    continue;
                }
                if (person.InUse || person.Actor.InUse || person.Look != look)
                {
                    continue;
                }
                if (ready == null || (ready.Stale && !person.Stale) || (ready.Stale == person.Stale && person.ReleasedAt < ready.ReleasedAt))
                {
                    ready = person;
                }
            }
            return ready;
        }

        private void Want(CityLook look)
        {
            if (!_wanted.Contains(look) && _wanted.Count < 4)
            {
                _wanted.Add(look);
            }
            Tend();
        }

        private void Tend()
        {
            if (_tending == null && gameObject.activeInHierarchy)
            {
                _tending = StartCoroutine(Guard.Routine(Tending()));
            }
        }

        // Only while the shelf has something to see to: one look at a time, when People/Looks allows one. A look asked for is
        // made at the pace of Looks (a site waits for it); a worn one is only for variety, so it waits for a screen that hides
        // the merge: up to 0.52.1 it was made in play, one to three a minute (the owner's session of 25 Oct 2026).
        private System.Collections.IEnumerator Tending()
        {
            while (_wanted.Count > 0 || AnyWorn())
            {
                if (_wanted.Count == 0 && !Looks.Hidden)
                {
                    AskDoor();
                    break;   // nothing a frame until then
                }
                yield return null;
                if (!Looks.Free)
                {
                    continue;
                }
                try
                {
                    TendOne();
                }
                catch (Exception e)
                {
                    CityLifeDirector.Logger?.Error(e);
                    _wanted.Clear();
                    for (int i = 0; i < _people.Count; i++)
                    {
                        _people[i].Stale = false;   // not tried again: they keep the look they have
                    }
                }
            }
            _tending = null;
        }

        private void AskDoor()
        {
            if (!_doorAsked)
            {
                _doorAsked = true;
                ScenePrewarm.Again("city life: the street people's worn looks changed (behind the black screen)", _atDoor ??= AtDoor);
            }
        }

        // Behind the black screen of a walk out (Core/ScenePrewarm): every worn look on the shelf changed, at most a few.
        private bool AtDoor()
        {
            _doorAsked = false;
            if (this == null)
            {
                return true;   // gone with its city
            }
            try
            {
                for (int i = 0, made = 0; i < _people.Count && made < WornAtDoor; i++)
                {
                    CityPerson person = _people[i];
                    if (person.Stale && person.Valid && !person.InUse && !person.Actor.InUse)
                    {
                        Redress(person, CityLook.Civilian);
                        made++;
                    }
                }
            }
            catch (Exception e)
            {
                CityLifeDirector.Logger?.Error(e);
                for (int i = 0; i < _people.Count; i++)
                {
                    _people[i].Stale = false;   // not tried again: they keep the look they have
                }
            }
            return true;
        }

        private bool AnyWorn()
        {
            for (int i = 0; i < _people.Count; i++)
            {
                CityPerson person = _people[i];
                if (person.Stale && person.Valid && !person.InUse && !person.Actor.InUse)
                {
                    return true;
                }
            }
            return false;
        }

        private void TendOne()
        {
            // First what was asked for and is still not on the shelf.
            while (_wanted.Count > 0)
            {
                CityLook look = _wanted[0];
                if (Ready(look) != null)
                {
                    _wanted.RemoveAt(0);   // somebody dressed for it has come back meanwhile
                    continue;
                }
                CityPerson spare = Spare();
                if (spare == null)
                {
                    return;   // everybody is out: when somebody comes back
                }
                _wanted.RemoveAt(0);
                Redress(spare, look);
                return;
            }
            // Then a new look for a civilian whose own is worn.
            for (int i = 0; i < _people.Count; i++)
            {
                CityPerson person = _people[i];
                if (person.Stale && person.Valid && !person.InUse && !person.Actor.InUse)
                {
                    Redress(person, CityLook.Civilian);
                    return;
                }
            }
        }

        // Whoever on the shelf can best be spared: of the look there are most of, sooner one due a change, then whoever came
        // back longest ago.
        private CityPerson Spare()
        {
            CityPerson spare = null;
            int most = 0;
            for (int i = 0; i < _people.Count; i++)
            {
                CityPerson person = _people[i];
                if (!person.Valid || person.InUse || person.Actor.InUse)
                {
                    continue;
                }
                int alike = 0;
                for (int k = 0; k < _people.Count; k++)
                {
                    CityPerson other = _people[k];
                    alike += other.Valid && !other.InUse && !other.Actor.InUse && other.Look == person.Look ? 1 : 0;
                }
                if (spare == null || alike > most || (alike == most && ((person.Stale && !spare.Stale) || (person.Stale == spare.Stale && person.ReleasedAt < spare.ReleasedAt))))
                {
                    spare = person;
                    most = alike;
                }
            }
            return spare;
        }

        // A look for someone on the shelf, awake only while it is put on.
        private void Redress(CityPerson person, CityLook look)
        {
            GameObject body = person.Character.gameObject;
            body.SetActive(true);
            try
            {
                person.Reset(_props);
                Dress(person, look);
            }
            finally
            {
                body.SetActive(false);
            }
        }

        // An idle character already dressed this way, else any idle one, to be re-dressed.
        private CityPerson Choose(CityLook look)
        {
            CityPerson spare = null;
            for (int i = _people.Count - 1; i >= 0; i--)
            {
                CityPerson person = _people[i];
                if (!person.Valid)
                {
                    _people.RemoveAt(i);   // destroyed with a scene the mod did not unload
                    continue;
                }
                if (person.InUse || person.Actor.InUse)
                {
                    continue;   // out, or still with the casualty system
                }
                if (person.Look == look && !person.Stale)
                {
                    return person;
                }
                if (spare == null || person.Look == look)
                {
                    spare = person;
                }
            }
            return spare;
        }

        // Who they are (age, build, face, hair) is rolled for everyone; workers then get their work clothes.
        private static void Dress(CityPerson person, CityLook look)
        {
            Looks.Count("city life: a character's look being built (a mesh merge)");   // named in the session record's long-frame line
            person.Look = look;
            person.Stale = false;
            person.Uses = 0;
            person.DressedAt = Time.unscaledTime;
            AppearanceSetter appearance = person.Character.appearanceSetter;
            CityOutfits.Restore(person.Dyes);
            appearance.SetRandomAge();
            Gender gender = Random.value < 0.5f ? Gender.Male : Gender.Female;
            appearance.RandomizeElements(gender, SkinColorHelper.GetRandom(), CasualWear);
            // The game holds a heavy character's arms wider by an animator layer that our clips replace: hands and tools made for
            // an average build would sink into a big belly.
            appearance.data.fatness = Mathf.Min(appearance.data.fatness, MaxBuild);
            CityOutfits.Wear(appearance, look, person.Dyes);
            appearance.UpdateVisuals();   // the one mesh merge for this outfit
            Looks.Done();
        }
    }
}
