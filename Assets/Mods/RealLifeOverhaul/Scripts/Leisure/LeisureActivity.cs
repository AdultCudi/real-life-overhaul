using Buildings;
using UnityEngine;

namespace RealLifeOverhaul.Leisure
{
    /// <summary>What an activity is handed: the player, their body, the props and who came along.</summary>
    public sealed class LeisureContext
    {
        public LeisureBody Body;
        public LeisureProps Props;
        public PlayerController Player;
        public ThirdPersonCharacter Companion;   // came along, or null; never moved or animated by leisure
        public Building Building;                // where it is played, or null out of doors

        public Vector3 Position => Player.transform.position;
        public Vector3 Forward => Player.transform.forward;
    }

    /// <summary>
    /// The base of every activity: steps ticked five times a second, <see cref="Frame"/> each frame for what must move
    /// smoothly (allocating nothing), and fields saying what to show, set only on change. <see cref="End"/> runs once.
    /// </summary>
    public abstract class LeisureActivity
    {
        public LeisureContext Ctx;

        public abstract LeisureKind Kind { get; }
        public abstract string TitleKey { get; }

        public string Objective;
        public string Status;
        public string Prompt;              // the panel's button on the interaction key ("E: cast")
        public string QuietPrompt;         // the button's words while OwnsInteractKey and nothing is announced
        public string[] Choices;           // up to three buttons on the panel's other keys, or null
        public bool OwnsInteractKey;       // the key is the activity's even with no Prompt up
        public bool Meter;
        public float MeterCursor, ZoneCentre, ZoneWidth, SweetWidth;
        public string MeterLabel;
        public bool Marked;
        public Vector3 MarkAt;
        public string MarkLabel;

        public bool Indoors;               // played inside Ctx.Building: leaving it ends the activity
        public Vector3 Anchor;
        public float Leash = 6f;           // past this CitySpots.Flat (a squared distance) from Anchor, it ends

        public bool Finished { get; private set; }
        public bool Played { get; private set; }    // to the end: it counts, sets records and gives its mood
        public int Score;
        public string CardTitle, CardBody;          // the result notification; null for none
        public string Mood;                         // a ModState modifier, or null

        /// <summary>Sets it up where the player stands; false with a locale key, nothing left out, if not.</summary>
        public abstract bool Begin(float now, out string refusalKey);

        public abstract void Tick(float now);

        public virtual void Frame(float now, float delta)
        {
        }

        public virtual void Interact(float now)
        {
        }

        public virtual void Choose(int choice, float now)
        {
        }

        /// <summary>Once on every way out, after the body was released: gives back people and props.</summary>
        public abstract void End();

        protected void Finish(bool played)
        {
            Finished = true;
            Played = played;
        }

        protected void Show(string objective, string status = null)
        {
            Objective = objective;
            Status = status;
        }

        protected static string T(string key) => JobText.T(key);
        protected static string T(string key, string name, string value) => JobText.T(key, name, value);
        protected static string T(string key, string name, string value, string name2, string value2) => JobText.T(key, name, value, name2, value2);
    }

    /// <summary>A cursor running back and forth along the meter, stopped with the key: the shared timing.</summary>
    public struct Sweep
    {
        public float Period;      // seconds for one pass
        public float Start;
        public float Centre;      // of the zone, 0..1
        public float Width;       // of the zone
        public float Sweet;       // of the better zone inside it

        public void Begin(float now, float period, float centre, float width, float sweet)
        {
            Start = now;
            Period = Mathf.Max(0.15f, period);
            Centre = centre;
            Width = width;
            Sweet = sweet;
        }

        public float At(float now)
        {
            float phase = (now - Start) / Period;
            float turn = phase - Mathf.Floor(phase * 0.5f) * 2f;   // 0..2
            return turn <= 1f ? turn : 2f - turn;
        }

        /// <summary>2 in the better zone, 1 in the zone, 0 outside.</summary>
        public int Judge(float now)
        {
            float off = Mathf.Abs(At(now) - Centre);
            return off <= Sweet * 0.5f ? 2 : off <= Width * 0.5f ? 1 : 0;
        }
    }
}
