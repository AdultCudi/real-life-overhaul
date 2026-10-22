using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Texts
{
    /// <summary>
    /// What a feature asks of RLO Texts: the intent and branch, the speaker, the values, the thread for a reply, how urgent
    /// and by when. Made by the asker on an event; handed back through <see cref="Done"/> on the game's thread exactly once,
    /// the words or null for the written line.
    /// </summary>
    public sealed class TextRequest
    {
        public TextUse Use;
        public string Key;                  // what it is for, and what kept words go under; the same key asked again replaces the request
        public string Intent;               // "friend.invite.coffee"
        public string Branch;               // the outcome the game decided, for intents with more than one card; null for one
        public readonly TextWho Who = new TextWho();
        public readonly TextSlots Slots = new TextSlots();
        public readonly List<TextSaid> History = new List<TextSaid>(3);   // the last messages of the thread, oldest first: replies only
        public string Speaker;              // who says it, for the repeat check
        public TextPriority Priority = TextPriority.Soon;
        public double Seconds = 4.0;        // real seconds from now until it is shown: the deadline
        public bool Guess;                  // asked before the player clicked; taken with TextsHub.TakeGuess, never handed over by itself
        public bool Keep;                   // the words are kept under Key in the save
        public bool Shared;                 // the host's words for the session's shared feed or paper: kept and sent to the guests
        public bool Flush;                  // handed over at once, the written line, when the game saves
        public int Day;                     // the game day, for kept words
        public Func<bool> Wanted;           // asked when the words come back: false if they are no longer wanted; null: always
        public Action<string> Done;         // on the game's thread: the words, or null for the written line
        public string Why;                  // filled by the hub: why there are no words, for the asker's log; null with words
        public double Took;                 // filled by the hub: seconds the model took over it, 0 when it never ran
        public bool ServiceOnly;            // sent to the player's own AI service only, never the pack: the options' test
        public bool ByService;              // filled by the hub: the words came from the player's own AI service
        public bool Spoken;                 // said face to face while the player waits: with the AI service up it may wait for it
        public double Waits;                // filled by the hub: real seconds from the ask it waits at most
        public TextSource Source;           // filled by the hub: where the words came from
        public string ServiceMiss;          // filled by the hub, with an AI service chosen: why it did not word it; null when it did
        public Action Started;              // on the game's thread, at most once: the AI service has begun to answer it
        public string Person;               // data v6: whose memory the card carries; null: none
        public readonly List<TextFact> Memory = new List<TextFact>(8);   // data v6: what that person remembers, filled by the asker; the hub picks the card's lines
        public string Lang;                 // filled by the hub: the language the card asks for (data v6), or null below
    }

    /// <summary>Where a request's words came from, for the asker's log line.</summary>
    public enum TextSource
    {
        Written = 0,   // none came: the written line
        Pack = 1,      // the RLO Texts pack on this PC
        Service = 2    // the player's own AI service
    }

    /// <summary>The runner as the hub sees it; the checks put a pretend one in.</summary>
    public interface ITextRunner
    {
        TextServerState State { get; }
        bool Quiet { set; }
        void Nudge();
    }
}
