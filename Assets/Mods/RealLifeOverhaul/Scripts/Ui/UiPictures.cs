using System;
using UnityEngine;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// Where a picture item's key finds its sprite: the game's own pictures of goods, furniture, vehicles and buildings, each family
    /// answered by the part of the mod that gives its keys (the city map's "map:", the phone's "phone:", a swatch's "swatch:"). Each
    /// part adds its answer once (<see cref="Answer"/>) and the parts are asked in turn (UiAnswers), so no part ever holds or calls
    /// another's and none can wrap another round in a ring. The layout keeps only the key and the picture's shape, so it stays game-free;
    /// the renderer asks here once per bind. Cost: a call per part a picture shown, on a layout only.
    /// </summary>
    public static class UiPictures
    {
        private const int Parts = 20;   // the parts that answer pictures, with room: about fifteen in the mod

        private static readonly UiAnswers<Sprite> Answers = new UiAnswers<Sprite>("UiPictures.Find", Parts);

        /// <summary>Adds a part's answer: the sprite for its own keys and null for every other, asked before the parts added earlier. Added once; the same answer again is not added twice.</summary>
        public static void Answer(Func<string, Sprite> part)
        {
            Answers.Add(part);
        }

        /// <summary>A key's sprite, or null when no part has one: the item then shows nothing.</summary>
        public static Sprite Find(string key) => Answers.Ask(key);
    }
}
