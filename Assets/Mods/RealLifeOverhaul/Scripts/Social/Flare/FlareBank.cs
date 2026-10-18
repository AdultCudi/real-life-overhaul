using System.Collections.Generic;

namespace RealLifeOverhaul.Flare
{
    /// <summary>What a photo of the city's bank shows. Saved as a number in the bank's file.</summary>
    public enum FlarePhotoKind
    {
        None = 0,
        Street = 1,     // a street of a district at eye height, a little of the sky in it
        Front = 2,      // the front of a business, from across the street (its place and kind kept)
        Park = 3,       // a park or a playing field (Madison Park, Sunset Park, the golf course, the tennis courts)
        Event = 4,      // the street event that is on (a market, a parade, a race, a film crew)
        Landmark = 5    // one of the city's sights (the big wheel, the bridge, Times Square, a harbour)
    }

    /// <summary>The light a photo was taken in, by the game's hour (FlareCityPhotoRules.LightAt).</summary>
    public enum FlarePhotoLight
    {
        Day = 0,        // 07:00 to 16:59
        Dusk = 1,       // 06:00 to 06:59 and 17:00 to 20:59: low sun, lamps coming on
        Night = 2       // 21:00 to 05:59
    }

    /// <summary>One photo of the bank: where and what it is, its light and weather, and how many posts carry it.</summary>
    public sealed class FlareBankPhoto
    {
        public int Id;                 // its number in this save's bank: its file is Bank_(id)
        public string District;        // the district it was taken in (the game's own id)
        public FlarePhotoKind Kind;
        public string Place;           // a front: the business ("street~number"); a park or a sight: its id; an event: its label; a street: null
        public int Niche = -1;         // a front: the business's kind (FlareNiche); -1 for anything else
        public FlarePhotoLight Light;
        public bool Wet;               // taken in the rain (wet streets, grey sky)
        public int Day;                // the game day it was taken
        public int Minute;             // ...and the minute of that day
        public int Uses;               // posts that carry it
    }

    /// <summary>A post's photo as it was picked: the bank's photo, or none (-1) when nothing fitted (then it never carries one).</summary>
    public struct FlareBankPick
    {
        public int Post;
        public int Photo;
    }

    /// <summary>The bank of one save's city photos and the posts' picks (kept beside the photos, never in the save).</summary>
    public sealed class FlareBank
    {
        public int NextId = 1;
        public readonly List<FlareBankPhoto> Photos = new List<FlareBankPhoto>(FlareCityPhotoRules.BankMost + 8);
        public readonly List<FlareBankPick> Picks = new List<FlareBankPick>(FlareCityPhotoRules.PicksMost + 8);

        public FlareBankPhoto Photo(int id)
        {
            for (int i = 0; i < Photos.Count; i++)
            {
                if (Photos[i].Id == id)
                {
                    return Photos[i];
                }
            }
            return null;
        }

        /// <summary>The photo picked for a post: its number, -1 when none fitted, -2 when it has not been picked yet.</summary>
        public int PickOf(int post)
        {
            for (int i = Picks.Count - 1; i >= 0; i--)
            {
                if (Picks[i].Post == post)
                {
                    return Picks[i].Photo;
                }
            }
            return -2;
        }
    }

    /// <summary>What a post's photo must be: up to three ways in order of preference, in its district, light and weather.</summary>
    public struct FlarePhotoWish
    {
        public FlarePhotoKind Kind0, Kind1, Kind2;     // the kinds tried in order (None: no more)
        public string Place0, Place1, Place2;          // ...a business or a sight it must be, or null for any
        public int Niche0, Niche1, Niche2;             // ...a business's kind it must be, or -1 for any
        public string District;                        // null for a post that names none; an event is the city's wherever it was
        public FlarePhotoLight Light;
        public bool Wet;
        public int Day;                                // the post's day: an event's photo is of that day, any other at most FreshDays apart

        public int Ways => Kind0 == FlarePhotoKind.None ? 0 : Kind1 == FlarePhotoKind.None ? 1 : Kind2 == FlarePhotoKind.None ? 2 : 3;

        public void Way(int k, out FlarePhotoKind kind, out string place, out int niche)
        {
            kind = k == 0 ? Kind0 : k == 1 ? Kind1 : Kind2;
            place = k == 0 ? Place0 : k == 1 ? Place1 : Place2;
            niche = k == 0 ? Niche0 : k == 1 ? Niche1 : Niche2;
        }

        public void Add(FlarePhotoKind kind, string place = null, int niche = -1)
        {
            if (Kind0 == FlarePhotoKind.None)
            {
                Kind0 = kind;
                Place0 = place;
                Niche0 = niche;
            }
            else if (Kind1 == FlarePhotoKind.None)
            {
                Kind1 = kind;
                Place1 = place;
                Niche1 = niche;
            }
            else if (Kind2 == FlarePhotoKind.None)
            {
                Kind2 = kind;
                Place2 = place;
                Niche2 = niche;
            }
        }
    }

    /// <summary>A business near where the player is, as the bank's pass sees it (its front may be photographed).</summary>
    public struct FlareFrontNear
    {
        public string Place;
        public string District;
        public int Niche;
        public float Metres;
        public bool Posts;    // a business of the city's posting today, or the player's own: its front is the most wanted
    }

    /// <summary>What can be photographed where the player is now (FlareDirector.Photos.cs reads it from the game).</summary>
    public sealed class FlarePhotoScene
    {
        public string District;
        public FlarePhotoLight Light;
        public bool Wet;
        public int Day, Minute;
        public string Event;            // the street event's label when the player is near it, else null
        public string Sight;            // the park's or sight's id when the player stands at one, else null
        public readonly List<FlareFrontNear> Fronts = new List<FlareFrontNear>(16);
    }

    /// <summary>The photo the bank's pass takes now: its kind and what it is of.</summary>
    public struct FlarePhotoShot
    {
        public FlarePhotoKind Kind;
        public string Place;
        public int Niche;
    }
}
