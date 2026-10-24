using System;

namespace RealLifeOverhaul.Shops
{
    /// <summary>
    /// The new kinds of shop's rules, game-free: the share of customers at an hour and on a day, the game's help page of a
    /// kind, and each kind's icon, drawn once at the game's start as the game's own are drawn (a white shape on clear, 50
    /// pixels square). Every answer depends only on its input. Nothing here runs per frame.
    /// </summary>
    public static class ShopsRules
    {
        public const int Size = 50;          // pixels across and down: the game's business icons' size
        private const int Fine = 4;          // samples across a pixel, each way, for smooth edges

        /// <summary>The share of the busiest hour at an hour of the day (thousandths); 0 outside every band.</summary>
        public static int HourShare(ShopKind kind, int hour)
        {
            for (int i = 0; kind != null && i < kind.Hours.Length; i++)
            {
                if (hour >= kind.Hours[i].From && hour < kind.Hours[i].To)
                {
                    return kind.Hours[i].Share;
                }
            }
            return 0;
        }

        /// <summary>The share of the busiest day on a day of the week, Monday 1 to Sunday 7 (thousandths); 0 for no such day.</summary>
        public static int DayShare(ShopKind kind, int day)
        {
            return kind != null && day >= 1 && day <= kind.Days.Length ? kind.Days[day - 1] : 0;
        }

        /// <summary>True when the bands run from midnight to midnight, each starting where the last ended.</summary>
        public static bool WholeDay(ShopKind kind)
        {
            if (kind == null || kind.Hours == null || kind.Hours.Length == 0 || kind.Hours[0].From != 0)
            {
                return false;
            }
            for (int i = 0; i < kind.Hours.Length; i++)
            {
                ShopHours band = kind.Hours[i];
                if (band.To <= band.From || band.Share <= 0 || band.Share > 1000 || (i > 0 && band.From != kind.Hours[i - 1].To))
                {
                    return false;
                }
            }
            return kind.Hours[kind.Hours.Length - 1].To == 24;
        }

        /// <summary>A business type's id without its owner, as the game's GetIdWithoutType ("reallife_teabar" is "teabar"); null for none.</summary>
        public static string Id(string type)
        {
            if (string.IsNullOrWhiteSpace(type) || type.IndexOf('_') < 0)
            {
                return null;
            }
            return type.Split('_')[1];
        }

        /// <summary>The game's help page of a business type, as its "at least one product" requirement links it ("businesstypes-teabar").</summary>
        public static string HelpSlug(string type)
        {
            string id = Id(type);
            return id == null ? null : ShopsCatalogue.HelpPrefix + id.ToLowerInvariant();
        }

        /// <summary>The key the game's help reads that page's text from, for a page it has no entry for ("help_businesstypes-teabar_content").</summary>
        public static string HelpContentKey(string type)
        {
            string slug = HelpSlug(type);
            return slug == null ? null : "help_" + slug + "_content";
        }

        /// <summary>The mod's own locale key of that text ("reallife_shops_help_teabar"), given to the game's key at each load of the language.</summary>
        public static string HelpTextKey(string type)
        {
            string id = Id(type);
            return id == null ? null : ShopsCatalogue.HelpTextPrefix + id.ToLowerInvariant();
        }

        /// <summary>
        /// A kind's icon: <see cref="Size"/> by <see cref="Size"/> alpha values, row 0 at the top. 0 an ice cream cone, 1 a
        /// cup of tea with its straw and pearls, 2 a cigarette with its smoke; anything else is empty.
        /// </summary>
        public static byte[] Glyph(int glyph)
        {
            var alpha = new byte[Size * Size];
            int samples = Fine * Fine;
            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    int inside = 0;
                    for (int sy = 0; sy < Fine; sy++)
                    {
                        for (int sx = 0; sx < Fine; sx++)
                        {
                            float px = x + (sx + 0.5f) / Fine, py = y + (sy + 0.5f) / Fine;
                            inside += Inside(glyph, px, py) ? 1 : 0;
                        }
                    }
                    alpha[y * Size + x] = (byte)((inside * 255 + samples / 2) / samples);
                }
            }
            return alpha;
        }

        // Each shape is what is drawn, less what is cut out of it, in the icon's pixels.
        private static bool Inside(int glyph, float x, float y)
        {
            switch (glyph)
            {
                case 0:
                    {
                        bool cone = Triangle(x, y, 14f, 25f, 36f, 25f, 25f, 48f) && !Lattice(x, y);
                        bool scoop = Circle(x, y, 25f, 17f, 11.5f) && !(y > 23.2f && y < 25.6f);
                        bool cherry = Circle(x, y, 25f, 4f, 3f);
                        return cone || scoop || cherry;
                    }
                case 1:
                    {
                        bool cup = Quad(x, y, 13f, 17f, 37f, 17f, 34f, 48f, 16f, 48f);
                        bool lid = Box(x, y, 11f, 11f, 39f, 15f);
                        bool straw = Box(x, y, 26f, 1f, 30f, 11f);
                        bool pearls = Circle(x, y, 20.5f, 42f, 2.6f) || Circle(x, y, 25f, 43.5f, 2.6f) || Circle(x, y, 29.5f, 42f, 2.6f)
                                      || Circle(x, y, 22.8f, 37.5f, 2.6f) || Circle(x, y, 27.2f, 37.5f, 2.6f);
                        return (cup && !pearls) || lid || straw;
                    }
                case 2:
                    {
                        bool stick = Box(x, y, 3f, 33f, 47f, 41f);
                        bool cuts = Box(x, y, 14f, 33f, 15.6f, 41f) || Box(x, y, 40.4f, 33f, 42f, 41f);
                        bool smoke = Ring(x, y, 41f, 24f, 4.5f, 2.2f) || Ring(x, y, 36f, 14f, 5f, 2.2f) || Ring(x, y, 42f, 5f, 3.8f, 2f);
                        return (stick && !cuts) || smoke;
                    }
                default:
                    return false;
            }
        }

        // The cone's criss-cross, cut as thin diagonal lines.
        private static bool Lattice(float x, float y)
        {
            float a = (x + y) % 7f, b = (x - y + 70f) % 7f;
            return y > 27f && (a < 1.1f || b < 1.1f);
        }

        private static bool Circle(float x, float y, float cx, float cy, float r)
        {
            float dx = x - cx, dy = y - cy;
            return dx * dx + dy * dy <= r * r;
        }

        private static bool Ring(float x, float y, float cx, float cy, float r, float width)
        {
            float dx = x - cx, dy = y - cy, d = (float)Math.Sqrt(dx * dx + dy * dy);
            return d <= r && d >= r - width;
        }

        private static bool Box(float x, float y, float x0, float y0, float x1, float y1)
        {
            return x >= x0 && x <= x1 && y >= y0 && y <= y1;
        }

        private static bool Triangle(float x, float y, float ax, float ay, float bx, float by, float cx, float cy)
        {
            float d1 = Side(x, y, ax, ay, bx, by), d2 = Side(x, y, bx, by, cx, cy), d3 = Side(x, y, cx, cy, ax, ay);
            bool negative = d1 < 0f || d2 < 0f || d3 < 0f, positive = d1 > 0f || d2 > 0f || d3 > 0f;
            return !(negative && positive);
        }

        private static bool Quad(float x, float y, float ax, float ay, float bx, float by, float cx, float cy, float dx, float dy)
        {
            return Triangle(x, y, ax, ay, bx, by, cx, cy) || Triangle(x, y, ax, ay, cx, cy, dx, dy);
        }

        private static float Side(float x, float y, float ax, float ay, float bx, float by)
        {
            return (x - bx) * (ay - by) - (ax - bx) * (y - by);
        }
    }
}
