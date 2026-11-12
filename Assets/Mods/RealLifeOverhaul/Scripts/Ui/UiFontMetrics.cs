namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// One cut of a typeface as the bundle holds it: its line metrics and every listed character's advance, in the font's
    /// own units (UiAssets/scripts/fonts.py writes the numbers into UiFontMetrics.Data.cs). Cost: a binary search a
    /// character over a few hundred codes; nothing allocated.
    /// </summary>
    public sealed class UiFace
    {
        public readonly UiFamily Family;
        public readonly int Weight;
        public readonly int UnitsPerEm;
        public readonly int Ascender;
        public readonly int Descender;   // negative
        public readonly int Gap;
        public readonly int CapHeight;
        public readonly int XHeight;
        private readonly ushort[] _codes;
        private readonly ushort[] _advances;
        private readonly int _average;   // a lower-case letter's mean advance, for a character no face of the bundle draws

        public UiFace(UiFamily family, int weight, int unitsPerEm, int ascender, int descender, int gap, int capHeight, int xHeight,
            ushort[] codes, ushort[] advances)
        {
            Family = family;
            Weight = weight;
            UnitsPerEm = unitsPerEm;
            Ascender = ascender;
            Descender = descender;
            Gap = gap;
            CapHeight = capHeight;
            XHeight = xHeight;
            _codes = codes;
            _advances = advances;
            int sum = 0, n = 0;
            for (char c = 'a'; c <= 'z'; c++)
            {
                int a = Advance(c);
                if (a > 0)
                {
                    sum += a;
                    n++;
                }
            }
            _average = n > 0 ? sum / n : unitsPerEm / 2;
        }

        /// <summary>The characters this cut draws.</summary>
        public int CharacterCount => _codes.Length;

        /// <summary>A line's height in em: ascender to descender and the gap, as TextMeshPro takes it from the font.</summary>
        public float LineEm => (Ascender - Descender + Gap) / (float)UnitsPerEm;

        /// <summary>The mean advance of a lower-case letter, in em.</summary>
        public float AverageEm => _average / (float)UnitsPerEm;

        /// <summary>A character's advance in the font's units, or -1 when this cut does not draw it.</summary>
        public int Advance(char c)
        {
            int lo = 0, hi = _codes.Length - 1;
            while (lo <= hi)
            {
                int mid = (lo + hi) >> 1;
                int code = _codes[mid];
                if (code == c)
                {
                    return _advances[mid];
                }
                if (code < c)
                {
                    lo = mid + 1;
                }
                else
                {
                    hi = mid - 1;
                }
            }
            return -1;
        }

        /// <summary>The listed character at an index, for checks that walk a face's whole table.</summary>
        public char CodeAt(int index) => (char)_codes[index];
    }

    /// <summary>The faces of the bundle, found by family and weight; the table itself is generated (UiFontMetrics.Data.cs).</summary>
    internal static partial class UiFontMetrics
    {
        /// <summary>The cut of a family nearest a weight (600, 700 or 800 are in the bundle).</summary>
        public static UiFace Face(UiFamily family, int weight)
        {
            UiFace best = null;
            int bestGap = int.MaxValue;
            for (int i = 0; i < Faces.Length; i++)
            {
                UiFace f = Faces[i];
                if (f.Family != family)
                {
                    continue;
                }
                int gap = f.Weight > weight ? f.Weight - weight : weight - f.Weight;
                if (gap < bestGap)
                {
                    best = f;
                    bestGap = gap;
                }
            }
            return best;
        }

        /// <summary>Every face, for checks.</summary>
        public static int FaceCount => Faces.Length;

        public static UiFace FaceAt(int index) => Faces[index];
    }
}
