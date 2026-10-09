using BAModAPI;
using BigAmbitions.Characters;
using Buildings;
using UnityEngine;

namespace RealLifeOverhaul.Leisure
{
    /// <summary>What an activity is handed: the player, their body, the props, who came along.</summary>
    public sealed class LeisureContext
    {
        public LeisureDirector Director;
        public LeisureBody Body;
        public LeisureProps Props;
        public PlayerController Player;
        public IModLogger Logger;
        public ThirdPersonCharacter Companion;   // somebody who came along, or null; never moved or animated by leisure
        public Vector3? Place;                   // where the caller wants it played, or null: where the player stands
        public bool Test;                        // started by an options-page button
        public Building Building;                // the building it is played in, or null out of doors

        public Vector3 Position => Player.transform.position;
        public Vector3 Forward => Player.transform.forward;
    }

    /// <summary>
    /// The base every leisure activity derives from. An activity is a switch over its own steps,
    /// ticked five times a second by the director (and once a frame through <see cref="Frame"/>,
    /// which must stay tiny and allocate nothing: a meter's cursor, a float on the water). It
    /// says what to show by setting the fields below, and only when they change: they are
    /// strings, and nothing may build one per tick. The player's body is moved only through
    /// <see cref="LeisureContext.Body"/>; the director undoes all of it on every way out, then
    /// calls <see cref="End"/> exactly once, where the activity gives back whatever else it put
    /// out (a person, a prop).
    /// </summary>
    public abstract class LeisureActivity
    {
        public LeisureContext Ctx;

        public abstract LeisureKind Kind { get; }
        public abstract string TitleKey { get; }

        // ---- what is on screen (set only on change)
        public string Objective;
        public string Status;
        public string Prompt;              // a key prompt: the interaction key is then the activity's
        public string[] Choices;           // three labels: keys 1, 2, 3
        public bool OwnsInteractKey;       // the interaction key is the activity's even with no prompt up
        public bool Meter;
        public float MeterCursor, ZoneCentre, ZoneWidth, SweetWidth;
        public string MeterLabel;
        public bool Marked;
        public Vector3 MarkAt;
        public string MarkLabel;
        public float Shade;                // how dark the screen is, 0..1
        public bool ListDirty;             // the activity changed its list: the director calls DrawList once

        // ---- where it is
        public bool Indoors;               // set by the director at the start: played inside Ctx.Building, and ends on leaving it
        public Vector3 Anchor;             // the middle of it
        public float Leash = 6f;           // the player further than this from the anchor ends it (they walked away)

        // ---- how it ended
        public bool Finished { get; private set; }
        public bool Played { get; private set; }    // to the end: counts as a game, sets records, gives its mood
        public int Score;
        public string CardTitle, CardBody;          // shown for a few seconds after; null: no card
        public string Mood;                         // a ModState modifier, or null

        /// <summary>Sets it up where the player stands. False with a reason (a locale key) if it cannot be played here; nothing may be left out then.</summary>
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

        /// <summary>The list panel, when the activity has one: called by the director after <see cref="ListDirty"/>.</summary>
        public virtual void DrawList(LeisureHud hud)
        {
        }

        /// <summary>Called exactly once on every way out, after the body was released. Gives back people and props; never throws for long.</summary>
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

    /// <summary>
    /// A cursor that runs back and forth along the meter and is stopped with the interaction key:
    /// the one timing interaction every activity shares. Game-free arithmetic; nothing allocated.
    /// </summary>
    public struct Sweep
    {
        public float Period;      // seconds for one pass, end to end
        public float Start;       // when it was started
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

        /// <summary>Where the cursor is, 0..1: out and back, out and back.</summary>
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
