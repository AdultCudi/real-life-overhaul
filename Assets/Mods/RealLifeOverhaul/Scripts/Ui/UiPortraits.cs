using System;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// A face for a person without a picture: a flat avatar painted in code, two or three cel tones a surface lit from the
    /// upper left, on a cool slate disc. Shoulders in one of six necklines, a neck under the chin's shade, a head with ears,
    /// brows, eyes with whites and irises, a nose, a closed smile and one of sixteen hairstyles (eight for men, eight for
    /// women), with a beard, glasses or earrings on some. The first name says whether to paint a man or a woman (a title
    /// such as Uncle, then a list of common first names in many languages, then the name's ending, else the hash), and the
    /// name's hash picks the rest, so the same person always looks the same. The disc is lighter behind deeper skin, so
    /// every face stands at least 1.8:1 off it. Painted by signed distances, soft over one texel at every edge, into an
    /// RGBA buffer, and the view makes it a texture once a name. Game-free.
    /// Cost: about 0.8 ms a face once at 128 texels (the afro, the heaviest, about 1.2), nothing allocated.
    /// </summary>
    public static class UiPortraits
    {
        /// <summary>The number of hairstyles a face may wear: eight for men, eight for women.</summary>
        public const int Styles = 16;

        private const int Crop = 0, SidePart = 1, Quiff = 2, Buzz = 3, Receding = 4, CurlyTop = 5, Bald = 6, ShortAfro = 7;
        private const int Long = 8, Bob = 9, Ponytail = 10, Bun = 11, Curls = 12, Afro = 13, Braids = 14, Pixie = 15;
        private const int Crew = 0, VNeck = 1, Collar = 2, Blazer = 3, Hoodie = 4, Turtle = 5;
        private const int Solid = 0, Rim = 1, Glint = 2;
        private const float HeadScale = 1.12f;   // the head's size over its 128 layout: a bust that fills the disc
        private const float Pivot = 62f;         // layout texels down: the point the head grows round

        // Skin light to deep, and the disc's OKLab lightness behind each: a mid slate behind light skin, darker behind tan,
        // lighter behind brown and deep skin, because one disc cannot stand off every tone at once.
        private static readonly uint[] Skins = { 0xF6D3BCFF, 0xECBE9AFF, 0xD9A076FF, 0xBF8152FF, 0x9A6240FF, 0x774A2EFF, 0x553321FF };
        private static readonly float[] DiscL = { 0.56f, 0.56f, 0.55f, 0.49f, 0.70f, 0.66f, 0.61f };

        // Natural hair: black, browns, auburn, copper, blonde, grey, silver. The odds favour dark hair on deeper skin.
        private static readonly uint[] Hairs = { 0x211C1FFF, 0x3B2A22FF, 0x5C3D2BFF, 0x8A6446FF, 0x8F3F25FF, 0xB5643AFF, 0xD2AC66FF, 0x9B9897FF, 0xCBC8C4FF };
        private static readonly byte[] LightHairOdds = { 14, 18, 18, 12, 8, 5, 13, 7, 5 };
        private static readonly byte[] TanHairOdds = { 34, 30, 18, 5, 2, 0, 1, 6, 4 };
        private static readonly byte[] DeepHairOdds = { 52, 30, 6, 0, 0, 0, 0, 7, 5 };

        // Clothes in soft colours, none the disc's slate, never a white or a neon, and the shirts a blazer goes over.
        private static readonly uint[] Cloths =
        {
            0xC4694FFF, 0xD3A447FF, 0x8BAA7CFF, 0x3F7A5DFF, 0x2F8E89FF, 0x7D4C77FF, 0xC98694FF, 0x8C3647FF, 0x3C4149FF, 0x2E4068FF,
            0xB98B5FFF, 0x9688C8FF, 0xDF7F69FF, 0x737D3FFF
        };
        private static readonly uint[] Shirts = { 0xE0B25AFF, 0x9DB88DFF, 0xD69AA3FF, 0xA99BD6FF, 0x7FB5B0FF, 0xE2A07EFF };
        private static readonly byte[] ManNecks = { 25, 10, 20, 15, 15, 15 };
        private static readonly byte[] WomanNecks = { 25, 20, 15, 15, 10, 15 };

        private static readonly uint[] Irises = { 0x3A2416FF, 0x5E3A1EFF, 0x6A5530FF, 0x46663FFF, 0x3F6690FF, 0x5E6C78FF };
        private static readonly byte[] LightIrisOdds = { 20, 25, 15, 13, 17, 10 };
        private static readonly byte[] DeepIrisOdds = { 70, 25, 5, 0, 0, 0 };
        private static readonly uint[] Frames = { 0x24242BFF, 0x5E3A24FF, 0xB8954DFF, 0x6D7682FF };
        private static readonly byte[] ManOdds = { 16, 15, 12, 12, 10, 12, 9, 9 };
        private static readonly byte[] WomanOdds = { 16, 14, 13, 12, 12, 8, 9, 10 };

        // Curls and puffs as x, y pairs (or x, y, radius) in the 128 layout, laid out once so the pixel loop turns no sine.
        private static readonly float[] TopCurls = Ring(64f, 46f, 18f, 18.5f, 195f, 345f, 8);
        private static readonly float[] CrownCurls = Ring(64f, 46.5f, 21.5f, 21.5f, 202f, 338f, 6);
        private static readonly float[] ShortPuffs = Ring(64f, 43.5f, 24f, 21f, 190f, 350f, 9);
        private static readonly float[] AfroPuffs = Ring(64f, 45f, 29.5f, 29.5f, 0f, 337.5f, 16);
        private static readonly float[] AfroLights = { 50f, 30f, 3.2f, 57f, 22f, 2.8f, 44f, 40f, 2.6f, 66f, 19f, 2.4f };
        private static readonly float[] RightScallops = { 92.5f, 56f, 5f, 94.5f, 65.5f, 5.6f, 93.5f, 75.5f, 5.8f, 95f, 85.5f, 5.4f, 93f, 95.5f, 5.6f, 88.5f, 103.5f, 5f, 81.5f, 105.5f, 4.4f };
        private static readonly float[] LeftScallops = { 35.5f, 57f, 5.2f, 33.5f, 67f, 5.6f, 35f, 77f, 5.8f, 33f, 87f, 5.4f, 35f, 97f, 5.6f, 39.5f, 104.5f, 5f, 46.5f, 106f, 4.2f };
        private static readonly float[] RightMarks = { 86f, 64f, 88.5f, 80f, 85.5f, 95f };
        private static readonly float[] LeftMarks = { 41f, 66f, 39f, 82f, 42f, 96f };
        private static readonly float[] Tail = { 84f, 46f, 7.4f, 88.5f, 57f, 7f, 90.5f, 68f, 6.4f, 90f, 79f, 5.4f, 88f, 88f, 4.2f, 85.5f, 94f, 2.8f };

        // First names, hashed once so a lookup is a binary search: no string is cut or built.
        private static readonly int[] Women = Keys(
            "rosa maya ava priya elena hana dana ines mary anna emma olivia sophia sofia isabella mia amelia charlotte grace lily chloe zoe nora ella ellie lucy ruby molly " +
            "holly jade poppy daisy freya isla imogen phoebe tessa clara claire kate katie jenny jane june joy iris ivy alice amy laura sarah sara rachel rebecca hannah leah " +
            "naomi ruth esther miriam emily jessica jennifer linda susan karen nancy lisa betty helen sandra donna carol sharon michelle kimberly deborah stephanie angela " +
            "melissa nicole samantha katherine catherine christine diane julie joan judith megan martha gloria teresa doris abigail julia judy denise amber beverly danielle " +
            "theresa diana natalie victoria margaret elizabeth patricia barbara dorothy evelyn heather hazel violet stella aurora penelope scarlett eleanor harriet daphne " +
            "wendy lorna ursula bridget gemma esme maeve saoirse siobhan niamh aoife fiona carmen lucia valeria ximena camila isabel beatriz marisol paloma mariana gabriela " +
            "daniela adriana alejandra fernanda juliana leticia raquel catalina pilar dolores mercedes yolanda paula marta nuria ana alba luz anika ananya aditi kavya divya " +
            "pooja neha shreya sneha riya isha meera lakshmi radha nisha deepa sunita anjali asha rani aisha fatima noor zara sana ayesha rania leila layla yasmin nadia farah " +
            "amira salma selma mira rhea tara uma yuki yui aiko akiko emi haruka hina keiko kumiko mai mayu miki misaki rin sakura yoko yumi yuna kaori megumi nanami ayaka " +
            "mei xiu ying hui jing fang ling qing xia yue lan hua jia xin ting yun olga natasha natalia svetlana irina tatiana anya anastasia ekaterina katya masha galina " +
            "ludmila oksana yulia daria alina polina ksenia vera nina zoya milena jelena ivana katarina petra agnieszka magda ewa kasia astrid ingrid sigrid saga linnea elsa " +
            "ebba maja alva liv signe karin britt solveig hilda greta frida siri tove annika kaia amara imani thandi adaeze chioma ngozi nia zuri amina aaliyah abena ama efua " +
            "esi folake kemi nkechi nneka sade zola zawadi aminata fatou mariama keisha tasha tanisha ebony jasmine destiny precious chantal odette camille juliette margot " +
            "elodie amelie manon celine sophie lea brigitte monique giulia francesca chiara alessia bianca elif zeynep talia noa yael tamar shira dina delia dalia marie rose " +
            "aunt auntie aunty mrs ms miss madam madame mum mom mother grandma granny gran nana sister lady queen princess senora frau");
        private static readonly int[] Men = Keys(
            "luis marcus tom jonah theo fred james john robert michael william david richard joseph thomas charles christopher daniel matthew anthony mark donald steven paul " +
            "andrew joshua kenneth kevin brian george timothy ronald edward jason jeffrey ryan jacob gary nicholas eric jonathan stephen larry justin scott brandon benjamin " +
            "samuel gregory alexander frank patrick raymond jack dennis jerry tyler aaron adam nathan henry douglas zachary peter kyle ethan walter noah jeremy christian " +
            "keith roger terry gerald harold sean austin carl arthur lawrence dylan bryan billy joe bruce gabriel logan albert alan wayne elijah randy roy vincent ralph " +
            "eugene russell bobby mason philip louis harry oliver leo liam lucas tommy tim ben bob bill will jim jimmy mike matt dan danny dave steve rick ricky richie richy " +
            "rich nick pete ray walt freddie frederick ted eddie ed ron don ken stan hal hank earl gus chuck chris josh jake luke max toby archie alfie oscar finn harvey " +
            "reggie stanley howard declan ronan callum eamon gareth wesley silas zane caleb nate joel isaac felix hugo victor emil anton malik tariq omar andre nico dev jun " +
            "kofi bao jose juan carlos diego miguel alejandro javier mateo pablo rafael sergio fernando ricardo eduardo andres alvaro jorge manuel francisco antonio pedro " +
            "raul ramon hector rodrigo santiago tomas joao thiago gustavo bruno felipe marco mario dario emilio enrique esteban gonzalo ignacio julio lorenzo mauricio nicolas " +
            "vicente xavier arjun rohan vikram raj rahul amit sanjay ravi vijay anil sunil deepak karan aditya akash arun ashok ganesh gaurav kunal manish mohan nikhil pranav " +
            "rakesh rajesh sachin sameer samir sandeep suresh varun vivek yash imran faisal hassan ali umar yusuf ahmed mohammed muhammad mustafa zain bilal hamza idris kabir " +
            "ishaan aarav kenji hiro hiroshi takeshi taro kenta haruto yuto sota ren riku kaito daiki takumi shota ryo ryota kazuki daisuke kenichi masato naoki satoshi " +
            "shinji tatsuya yusuke koji jiro ichiro kazuo osamu wei hao lei ming jian tao bo gang peng qiang yong cheng feng jie kai rui xiang zhi ivan dmitri dmitry sergei " +
            "sergey mikhail alexei alexey nikolai vladimir boris igor oleg pavel yuri andrei viktor vlad vasily piotr tomasz marek jakub krzysztof lukasz milan goran zoran " +
            "stefan nikola luka filip bogdan ilya maxim roman artem denis kirill leonid lars henrik erik anders bjorn sven nils olaf olav magnus axel gustav johan karl leif " +
            "rolf thor ivar jens mads mikkel rasmus soren kasper viggo oskar elias mikko matti kwame kwesi kojo yaw femi ade tunde chinedu chidi emeka obinna ikenna uche " +
            "tendai sipho thabo themba mandla bongani jabari juma baraka omari kamau ousmane mamadou moussa ibrahim abdul seun segun dayo kayode olu kelechi nnamdi luca " +
            "matteo giovanni giuseppe alessandro francesco pierre antoine julien mathieu remy baptiste francois olivier sebastien klaus hans jurgen dieter wolfgang florian " +
            "lukas tobias jonas moritz fabian niklas kemal mehmet emre burak murat ahmet eitan avi yosef moshe noam ari levi ezra asher eli marko darko mirko janko branko " +
            "uncle mr mister sir dad daddy father grandpa granddad grandad gramps brother lord king prince senor monsieur herr duke");
        private static readonly int[] Titles = Keys(
            "dr doctor prof professor officer detective coach captain judge rev reverend the sgt sergeant chef agent inspector mayor boss lt major");

        /// <summary>
        /// A stable seed from a name, the same in every session and in the offline checks. Its two low bits carry the look
        /// the first name suggests: 1 a woman, 2 a man, 0 when the name does not say. A hash and a binary search, nothing allocated.
        /// </summary>
        public static int Seed(string name)
        {
            unchecked
            {
                int h = (int)2166136261;
                for (int i = 0; name != null && i < name.Length; i++)
                {
                    h = (h ^ name[i]) * 16777619;
                }
                return (h & 0x7FFFFFFC) | Presented(name);
            }
        }

        /// <summary>
        /// A seed from a name for a person whose sex the mod records: the name's hash with the sex in its two low bits, so a
        /// recorded woman is never painted a man because her name is one the list does not know.
        /// </summary>
        public static int SeedFor(string name, bool female)
        {
            return (Seed(name) & 0x7FFFFFFC) | (female ? 1 : 2);
        }

        /// <summary>Paints the face for a seed into a buffer of size x size RGBA texels, rows from the bottom as a texture holds them.</summary>
        public static void Paint(int seed, int size, byte[] rgba, float hue)
        {
            Paint(seed, size, rgba, hue, 0);
        }

        /// <summary>The same, for a person of a recorded age in years (0 for none known): from <see cref="UiFaceRules.GreyFrom"/> their hair is grey.</summary>
        public static void Paint(int seed, int size, byte[] rgba, float hue, int age)
        {
            var f = new Face();
            Pick(seed, hue, age, ref f);
            float k = size / 128f, inv = 128f / size, rim = 64f - 0.5f * inv;
            Load(f.Ground, out float r0, out float g0, out float b0);
            for (int py = 0; py < size; py++)
            {
                float y = (size - py - 0.5f) * inv;   // y down from the top of a 128 layout, as the shapes are laid out
                for (int px = 0; px < size; px++)
                {
                    float x = (px + 0.5f) * inv;
                    int at = (py * size + px) * 4;
                    float cover = Cover((Len(x - 64f, y - 64f) - rim) * k);
                    // Outside the disc the ground's colour stays under no alpha, so the smaller mips do not pull a dark rim in.
                    float r = r0, g = g0, b = b0;
                    if (cover > 0f)
                    {
                        Texel(ref f, x, y, k, ref r, ref g, ref b);
                    }
                    rgba[at] = Byte(r);
                    rgba[at + 1] = Byte(g);
                    rgba[at + 2] = Byte(b);
                    rgba[at + 3] = Byte(cover);
                }
            }
        }

        // Everything one face needs in the pixel loop, worked out once.
        private struct Face
        {
            public bool Woman, Back, Lashes;
            public int Style, Neck, Beard, Glasses, Rings, Nose;
            public uint Ground, Skin, SkinLit, SkinShade, SkinDeep, Cheek, Lip, Mouth;
            public uint Hair, HairLit, HairShade, HairDeep, Brow, Stubble, Buzz, BuzzShade;
            public uint Cloth, ClothLit, ClothShade, ClothDeep, Inner, InnerShade, Cord;
            public uint White, Iris, Lash, Frame, Metal;
            public float Crown, Jaw, Smile, NeckW, Top, Slope, SlopeNorm, Wide, EyeH, Arch;
        }

        private static void Pick(int seed, float hue, int age, ref Face f)
        {
            uint s = Math.Max(1u, ((uint)seed >> 2) * 2654435761u ^ 0x5BD1E995u);
            int look = seed & 3;
            f.Woman = look == 1 || (look != 2 && Roll(ref s, 2) == 0);
            int skin = Roll(ref s, Skins.Length);
            f.Skin = Skins[skin];
            f.Ground = Ground(skin, hue);
            int hair = Odds(ref s, skin <= 2 ? LightHairOdds : skin == 3 ? TanHairOdds : DeepHairOdds);
            hair = age >= UiFaceRules.GreyFrom ? 7 + (int)(s & 1u) : hair;   // grey or silver whatever the roll gave; the rolls after it are the same
            // Grey on a light disc would melt into it, and dark brown on the deepest skin reads as one shape with the face.
            hair = hair >= 7 && Contrast(Hairs[hair], f.Ground) < 1.25f ? 1 : hair;
            hair = skin >= 4 && hair < 7 && Contrast(Hairs[hair], Skins[skin]) < 1.45f ? 0 : hair;
            f.Hair = Hairs[hair];
            int style = Odds(ref s, f.Woman ? WomanOdds : ManOdds) + (f.Woman ? Long : 0);
            // Afro hair sits on the deeper skin tones.
            f.Style = style = skin >= 3 ? style : style == ShortAfro ? CurlyTop : style == Afro ? Curls : style == Braids ? Long : style;
            f.Back = style == Long || style == Bob || style == Ponytail || style == Curls || style == Afro || style == Braids;
            f.Neck = Odds(ref s, f.Woman ? WomanNecks : ManNecks);
            int cloth = Roll(ref s, Cloths.Length);
            for (int i = 0; i < Cloths.Length && Contrast(Cloths[cloth], f.Ground) < 1.4f; i++)
            {
                cloth = (cloth + 1) % Cloths.Length;
            }
            f.Cloth = Cloths[cloth];
            f.Inner = Shirts[Roll(ref s, Shirts.Length)];
            f.Iris = Irises[Odds(ref s, skin <= 3 ? LightIrisOdds : DeepIrisOdds)];
            f.Glasses = Roll(ref s, 100) < 22 ? 1 + Roll(ref s, 2) : 0;
            f.Frame = Frames[Roll(ref s, Frames.Length)];
            int beard = Roll(ref s, 100);
            f.Beard = f.Woman ? 0 : beard < 50 ? 0 : beard < 66 ? 1 : beard < 82 ? 2 : beard < 90 ? 3 : 4;
            f.Rings = f.Woman && Roll(ref s, 100) < 40 ? 1 + Roll(ref s, 2) : 0;
            f.Metal = Roll(ref s, 2) == 0 ? 0xD8B456FFu : 0xC8CCD2FFu;
            f.Lashes = f.Woman;
            f.Nose = Roll(ref s, 2);
            f.Crown = 20.6f + Roll(ref s, 3) * 0.5f;
            f.Jaw = (f.Woman ? 15.6f : 16.8f) + Roll(ref s, 3) * 0.6f;
            f.Smile = 6.4f + Roll(ref s, 3) * 0.4f;

            f.SkinLit = Tone(f.Skin, 0.045f, -0.05f, 4f, 0f);
            f.SkinShade = Tone(f.Skin, -0.075f, 0.1f, -6f, 0f);
            f.SkinDeep = Tone(f.Skin, -0.15f, 0.18f, -10f, 0f);
            f.Mouth = Tone(f.Skin, -0.27f, 0.25f, -14f, 0f);
            f.Cheek = Tone(f.Skin, -0.01f, 0.4f, -14f, 0f);
            f.Lip = f.Woman ? Mix(f.SkinShade, 0xC0606AFFu, 0.45f) : f.SkinShade;
            f.HairLit = Tone(f.Hair, 0.1f, -0.1f, 4f, -0.004f);
            f.HairShade = Tone(f.Hair, -0.07f, 0f, 0f, -0.006f);
            f.HairDeep = Tone(f.Hair, -0.14f, 0f, 0f, -0.008f);
            f.Brow = Lab(f.Hair, out _, out _) > 0.55f ? Tone(f.Hair, -0.25f, 0.1f, 0f, 0f) : Tone(f.Hair, -0.03f, 0f, 0f, 0f);
            f.Stubble = Mix(f.Skin, f.HairDeep, 0.3f);
            f.Buzz = Mix(f.Skin, f.HairShade, 0.7f);
            f.BuzzShade = Mix(f.SkinShade, f.HairDeep, 0.7f);
            f.ClothLit = Tone(f.Cloth, 0.06f, 0f, 0f, 0f);
            f.ClothShade = Tone(f.Cloth, -0.08f, 0.05f, 0f, -0.012f);
            f.ClothDeep = Tone(f.Cloth, -0.15f, 0.05f, 0f, -0.015f);
            f.InnerShade = Tone(f.Inner, -0.08f, 0.05f, 0f, -0.012f);
            f.Cord = Lab(f.Cloth, out _, out _) > 0.62f ? f.ClothDeep : Tone(f.Cloth, 0.2f, -0.4f, 0f, 0f);
            f.White = Mix(0xF3EFEAFFu, f.Skin, 0.1f);
            f.Lash = 0x261B1BFFu;

            f.NeckW = f.Woman ? 8.2f : 9.6f;
            f.Top = f.Woman ? 94.5f : 93.5f;
            f.Slope = f.Woman ? 0.25f : 0.2f;
            f.SlopeNorm = 1f / (float)Math.Sqrt(1f + f.Slope * f.Slope);
            f.Wide = f.Woman ? 47f : 52f;
            f.EyeH = (f.Woman ? 2.8f : 2.5f) + Roll(ref s, 3) * 0.12f;
            f.Arch = Roll(ref s, 3) * 0.5f;
        }

        private static void Texel(ref Face f, float x, float y, float k, ref float r, ref float g, ref float b)
        {
            // The head and all on it are laid out at their own scale round a point under the mouth, so it can grow without
            // the shoulders moving. Distances there are worth HeadScale texels of the layout.
            float hx = 64f + (x - 64f) / HeadScale, hy = Pivot + (y - Pivot) / HeadScale, hk = k * HeadScale;
            float ax = Math.Abs(x - 64f), hax = Math.Abs(hx - 64f);
            if (f.Back && hax < 40f && hy > 8f && hy < 122f)
            {
                BackHair(ref f, hx, hy, hax, hk, ref r, ref g, ref b);
            }
            if (y > 60f)
            {
                Body(ref f, x, y, ax, k, ref r, ref g, ref b);
            }
            if (hax > 15f && hax < 27f && hy > 50f && hy < 72f)
            {
                Ears(ref f, hx, hy, hax, hk, ref r, ref g, ref b);
            }
            float hair = f.Style != Bald && hax < 36f && hy > 6f && hy < 122f ? FrontHairSdf(ref f, hx, hy, hax) : 99f;
            if (hax < 24f && hy > 24f && hy < 92f)
            {
                Head(ref f, hx, hy, hax, hk, hair, ref r, ref g, ref b);
            }
            if (f.Glasses > 0 && hax < 21f && hy > 51f && hy < 64f)
            {
                Glasses(ref f, hx, hy, hax, hk, ref r, ref g, ref b);
            }
            if (hair * hk < 0.5f)
            {
                FrontHair(ref f, hx, hy, hax, hk, hair, ref r, ref g, ref b);
            }
        }

        private static float HeadSdf(ref Face f, float x, float y) =>
            SMin(Ellipse(x, y, 64f, 50.5f, f.Crown, 23.5f), Ellipse(x, y, 64f, 61.5f, f.Jaw, 19.5f), 7f);

        // The head's distance at a point of the layout, for shapes off the head that it shades.
        private static float HeadAt(ref Face f, float x, float y) =>
            HeadSdf(ref f, 64f + (x - 64f) / HeadScale, Pivot + (y - Pivot) / HeadScale) * HeadScale;

        // The neck, then the clothes over it, so a neckline shows the neck's own skin.
        private static void Body(ref Face f, float x, float y, float ax, float k, ref float r, ref float g, ref float b)
        {
            float neck = ax < 14f && y < 110f ? Cover(RoundBox(x, y, 64f - f.NeckW, 62f, 64f + f.NeckW, 112f, 3f) * k) : 0f;
            if (neck > 0f)
            {
                Load(f.SkinShade, out float nr, out float ng, out float nb);
                // The chin casts down and a little right, the way the light falls.
                Over(ref nr, ref ng, ref nb, f.SkinDeep, Cover(HeadAt(ref f, x - 1f, y - 4f) * k));
                Over(ref r, ref g, ref b, nr, ng, nb, neck);
            }
            float line = (f.Top + f.Slope * ax - y) * f.SlopeNorm;
            float c = y > 82f ? Cover(SMax(line, RoundBox(x, y, 64f - f.Wide, 75f, 64f + f.Wide, 200f, 22f), 10f) * k) : 0f;
            if (c <= 0f)
            {
                return;
            }
            float flank = (24f - ((x - 64f) * 0.95f + (y - 100f) * 0.3f)) * k;
            Load(f.Cloth, out float mr, out float mg, out float mb);
            Over(ref mr, ref mg, ref mb, f.ClothLit, Cover(Math.Max(Math.Abs(line + 1.7f) - 1.3f, x - 50f) * k));
            Over(ref mr, ref mg, ref mb, f.ClothShade, Cover(flank));
            if (ax < 21f)
            {
                Neckline(ref f, x, y, ax, k, flank, ref mr, ref mg, ref mb);
            }
            Over(ref r, ref g, ref b, mr, mg, mb, c);
        }

        private static void Neckline(ref Face f, float x, float y, float ax, float k, float flank, ref float r, ref float g, ref float b)
        {
            switch (f.Neck)
            {
                case Crew:
                {
                    float open = f.Woman ? Ellipse(x, y, 64f, 95f, 12.5f, 9f) : Ellipse(x, y, 64f, 94f, 12f, 7f);
                    Over(ref r, ref g, ref b, f.SkinShade, Cover(open * k));
                    Over(ref r, ref g, ref b, f.ClothDeep, Cover((Math.Abs(open) - 1.2f) * k));
                    break;
                }
                case VNeck:
                {
                    float v = (ax * 17f + (y - 109f) * 10.5f) / 19.98f;
                    Over(ref r, ref g, ref b, f.SkinShade, Cover(v * k));
                    Over(ref r, ref g, ref b, f.ClothDeep, Cover((Math.Abs(v) - 1.1f) * k));
                    break;
                }
                case Collar:
                {
                    Over(ref r, ref g, ref b, f.SkinShade, Cover((ax * 13f + (y - 104f) * 7.5f) / 15.01f * k));
                    Over(ref r, ref g, ref b, f.ClothShade, Cover(Capsule(x, y, 64f, 104f, 64f, 130f, 0.55f) * k));
                    Over(ref r, ref g, ref b, f.ClothDeep, Cover(Circle(x, y, 64f, 111.5f, 1.05f) * k));
                    float mx = 64f + ax;
                    // The flap lifts off the shirt, so it casts a thin line under its lower edges.
                    Over(ref r, ref g, ref b, f.ClothDeep, Cover(Quad(mx, y + 1.4f, 71f, 89f, 77.5f, 92.5f, 76.5f, 101.5f, 66f, 103.5f) * k));
                    Over(ref r, ref g, ref b, f.ClothLit, Cover(Quad(mx, y, 71f, 89f, 77.5f, 92.5f, 76.5f, 101.5f, 66f, 103.5f) * k));
                    break;
                }
                case Blazer:
                {
                    float v = (ax * 36f + (y - 127f) * 12.5f) / 38.11f;
                    Load(f.Inner, out float ir, out float ig, out float ib);
                    Over(ref ir, ref ig, ref ib, f.InnerShade, Cover(flank));
                    Over(ref ir, ref ig, ref ib, f.SkinShade, Cover(Ellipse(x, y, 64f, 93.6f, 9.6f, 5.8f) * k));
                    Over(ref r, ref g, ref b, ir, ig, ib, Cover(v * k));
                    Over(ref r, ref g, ref b, f.ClothLit, Cover(Math.Max(Math.Abs(v - 2.4f) - 2.4f, y - 118f) * k));
                    Over(ref r, ref g, ref b, f.ClothDeep, Cover(Math.Max(Math.Abs(v - 4.8f) - 0.5f, y - 118f) * k));
                    break;
                }
                case Hoodie:
                {
                    float open = Ellipse(x, y, 64f, 93f, 10.4f, 6.2f);
                    Over(ref r, ref g, ref b, f.ClothLit, Cover(SMax(Ellipse(x, y, 64f, 94f, 19f, 9.4f), -open, 1f) * k));
                    Over(ref r, ref g, ref b, f.ClothDeep, Cover((Math.Abs(open) - 0.8f) * k));
                    Over(ref r, ref g, ref b, f.SkinShade, Cover((open + 0.8f) * k));
                    Over(ref r, ref g, ref b, f.Cord, Cover(Capsule(64f + ax, y, 67.8f, 100f, 68.4f, 112f, 0.75f) * k));
                    break;
                }
                default:
                {
                    Load(f.Cloth, out float tr, out float tg, out float tb);
                    Over(ref tr, ref tg, ref tb, f.ClothShade, Cover(HeadAt(ref f, x - 1f, y - 4f) * k));
                    float folds = Math.Min(Capsule(x, y, 64f - f.NeckW, 88.5f, 64f + f.NeckW, 88.5f, 0.55f), Capsule(x, y, 64f - f.NeckW, 93.5f, 64f + f.NeckW, 93.5f, 0.55f));
                    Over(ref tr, ref tg, ref tb, f.ClothShade, Cover(folds * k));
                    Over(ref r, ref g, ref b, tr, tg, tb, Cover(RoundBox(x, y, 64f - f.NeckW - 1.6f, 83f, 64f + f.NeckW + 1.6f, 100f, 2.5f) * k));
                    break;
                }
            }
        }

        private static void Ears(ref Face f, float x, float y, float ax, float k, ref float r, ref float g, ref float b)
        {
            float mx = 64f + ax;
            float c = Cover(Ellipse(mx, y, 64f + f.Crown - 0.6f, 58.8f, 3.7f, 5.8f) * k);
            if (c > 0f)
            {
                Load(f.Skin, out float mr, out float mg, out float mb);
                Over(ref mr, ref mg, ref mb, f.SkinShade, Cover(Ellipse(mx, y, 64f + f.Crown - 0.2f, 59.3f, 1.7f, 3.3f) * k));
                Over(ref r, ref g, ref b, mr, mg, mb, c);
            }
            float ring = f.Rings == 1 ? Circle(mx, y, 64f + f.Crown - 0.4f, 64.8f, 1.35f) : f.Rings == 2 ? Math.Abs(Circle(mx, y, 64f + f.Crown - 0.2f, 66.8f, 2.3f)) - 0.55f : 99f;
            Over(ref r, ref g, ref b, f.Metal, Cover(ring * k));
        }

        private static void Head(ref Face f, float x, float y, float ax, float k, float hair, ref float r, ref float g, ref float b)
        {
            float head = HeadSdf(ref f, x, y);
            float c = Cover(head * k);
            if (c > 0f)
            {
                Load(f.Skin, out float mr, out float mg, out float mb);
                // A crescent down the right cheek: the light's own ellipse sits left of the head's.
                Over(ref mr, ref mg, ref mb, f.SkinShade, Cover(-Ellipse(x, y, 59.5f, 52f, f.Crown + 1.3f, 30f) * k));
                if (hair * k > -0.5f && f.Style != Bald && ax < 23f && y > 30f && y < 62f)
                {
                    // The fringe's shade on the forehead, looked for only where the hair itself does not cover the skin.
                    Over(ref mr, ref mg, ref mb, f.SkinShade, Cover(FrontHairSdf(ref f, x - 0.6f, y - 2.4f, Math.Abs(x - 64.6f)) * k));
                }
                bool bare = f.Style == Bald || f.Style == Receding, cheek = f.Woman && ax > 9f && ax < 18f && y > 62f && y < 69f;
                Over(ref mr, ref mg, ref mb, f.SkinLit, bare ? Cover(Tilted(x, y, 56.5f, 34f, 5.6f, 2.8f, 0.906f, -0.423f) * k) : 0f);
                Over(ref mr, ref mg, ref mb, f.Cheek, cheek ? Cover(Ellipse(64f + ax, y, 77.2f, 65.6f, 3.7f, 2.2f) * k) : 0f);
                Over(ref mr, ref mg, ref mb, f.Stubble, f.Beard == 1 ? Cover(SMax(SMax(head, -Ellipse(x, y, 64f, 58.5f, 16.2f, 12.6f), 1f), 57f - y, 1f) * k) : 0f);
                if (ax < 6f && y > 57f && y < 69f)
                {
                    Nose(ref f, x, y, k, ref mr, ref mg, ref mb);
                }
                Over(ref r, ref g, ref b, mr, mg, mb, c);
            }
            if (f.Beard == 2 || f.Beard == 4)
            {
                float beard = f.Beard == 2
                    ? SMax(SMax(SMin(head, Ellipse(x, y, 64f, 79.5f, 12.5f, 7.5f), 3f), -Ellipse(x, y, 64f, 58.5f, 15.6f, 12.2f), 1f), 55.5f - y, 1f)
                    : Ellipse(x, y, 64f, 77.8f, 5.4f, 4.4f);
                float bc = Cover(SMax(beard, -Ellipse(x, y, 64f, 72.6f, 4.4f, 2.1f), 0.8f) * k);
                if (bc > 0f)
                {
                    Load(f.Hair, out float br, out float bg, out float bb);
                    Over(ref br, ref bg, ref bb, f.HairShade, Cover((16f - ((x - 64f) * 0.9f + (y - 50f) * 0.43f)) * k));
                    Over(ref r, ref g, ref b, br, bg, bb, bc);
                }
            }
            if (ax < 8f && y > 67f && y < 77f)
            {
                Over(ref r, ref g, ref b, f.Lip, Cover(Ellipse(x, y, 64f, 67.3f + f.Smile, f.Smile * 0.5f, 1.3f) * k));
                Over(ref r, ref g, ref b, f.Mouth, Cover(Smile(x - 64f, y - 66f, f.Smile, 0.75f, 0.643f, 0.766f) * k));
            }
            if (f.Beard >= 3 && ax < 9f && y > 65f && y < 72f)
            {
                Over(ref r, ref g, ref b, f.Hair, Cover(Tilted(64f + ax, y, 67.2f, 69f, 4.3f, 1.8f, 0.978f, 0.208f) * k));
            }
            if (ax > 3.5f && ax < 16f && y > 52f && y < 62f)
            {
                Eye(ref f, x, y, ax, k, ref r, ref g, ref b);
            }
            if (ax > 2.5f && ax < 16.5f && y > 46f && y < 54f)
            {
                float mx = 64f + ax;
                float peak = f.Woman ? 49.3f - f.Arch : 50f - f.Arch * 0.6f;
                float brow = f.Woman
                    ? Math.Min(Capsule(mx, y, 68.8f, 51.2f, 73.6f, peak, 1.25f), Capsule(mx, y, 73.6f, peak, 77.8f, 50.6f, 0.95f))
                    : Math.Min(Capsule(mx, y, 68.4f, 51f, 73.4f, peak, 1.55f), Capsule(mx, y, 73.4f, peak, 77.9f, 50.9f, 1.25f));
                Over(ref r, ref g, ref b, f.Brow, Cover(brow * k));
            }
        }

        // The nose by its shade alone, a flat avatar's way: a line down its far side and under its tip, or a button's curve.
        private static void Nose(ref Face f, float x, float y, float k, ref float r, ref float g, ref float b)
        {
            float side = f.Nose == 0
                ? Math.Min(Capsule(x, y, 66.1f, 59.8f, 66.9f, 65f, 0.75f), Capsule(x, y, 66.9f, 65.3f, 62.8f, 66.9f, 0.95f))
                : Math.Min(Smile(x - 64.2f, y - 63.4f, 3.2f, 0.8f, 0.766f, 0.643f), Capsule(x, y, 66.2f, 60.6f, 66.8f, 63.8f, 0.6f));
            Over(ref r, ref g, ref b, f.SkinShade, Cover(side * k));
            Over(ref r, ref g, ref b, f.SkinLit, Cover(Circle(x, y, 63.9f, 64.2f, 1.2f) * k));
        }

        private static void Eye(ref Face f, float x, float y, float ax, float k, ref float r, ref float g, ref float b)
        {
            float mx = 64f + ax;
            const float ex = 73.2f, ey = 57.6f;
            float white = Ellipse(mx, y, ex, ey, 4.2f, f.EyeH);
            float c = Cover(white * k);
            if (c > 0f)
            {
                Load(f.White, out float er, out float eg, out float eb);
                Over(ref er, ref eg, ref eb, f.Iris, Cover(Circle(mx, y, ex, ey - 0.15f, 2.7f) * k));
                Over(ref er, ref eg, ref eb, 0x120D0EFFu, Cover(Circle(mx, y, ex, ey - 0.15f, 1.3f) * k));
                // The glint sits up and left in both eyes, so it is not mirrored.
                float gx = (x < 64f ? 128f - ex : ex) - 0.9f;
                Over(ref er, ref eg, ref eb, 0xFBF8F2FFu, Cover(Circle(x, y, gx, ey - 1f, 0.85f) * k));
                Over(ref r, ref g, ref b, er, eg, eb, c);
            }
            float lid = Math.Max(Math.Abs(white) - 0.65f, y - (ey - 0.2f));
            lid = f.Lashes ? Math.Min(lid, Capsule(mx, y, ex + 3.9f, ey - 0.8f, ex + 5.3f, ey - 2.1f, 0.5f)) : lid;
            Over(ref r, ref g, ref b, f.Lash, Cover(lid * k));
        }

        private static void Glasses(ref Face f, float x, float y, float ax, float k, ref float r, ref float g, ref float b)
        {
            float mx = 64f + ax;
            float lens = f.Glasses == 1 ? RoundBox(mx, y, 67.3f, 53f, 79.1f, 61.8f, 2.4f) : Circle(mx, y, 73.2f, 57.4f, 5.1f);
            float inside = Cover(lens * k);
            if (inside > 0f)
            {
                Over(ref r, ref g, ref b, 0xFFFFFFFFu, inside * 0.1f);
                float lx = x < 64f ? 54.8f : 73.2f;
                Over(ref r, ref g, ref b, 0xFFFFFFFFu, Cover(Capsule(x, y, lx - 2.6f, 59.6f, lx - 0.6f, 55f, 0.7f) * k) * inside * 0.3f);
            }
            float temple = Capsule(mx, y, 79.1f, 56.5f, 64f + f.Crown, 56.2f, 0.6f);
            float frame = Math.Min(Math.Abs(lens) - 0.62f, Math.Min(Capsule(mx, y, 64f, 56.3f, 67.6f, 56.5f, 0.6f), temple));
            Over(ref r, ref g, ref b, f.Frame, Cover(frame * k));
        }

        private static void BackHair(ref Face f, float x, float y, float ax, float k, ref float r, ref float g, ref float b)
        {
            float d;
            switch (f.Style)
            {
                case Long:
                    d = RoundBox(x, y, 37.5f, 34f, 90.5f, 116f, 14f);
                    break;
                case Bob:
                    d = RoundBox(x, y, 39.5f, 38f, 88.5f, 82f, 11f);
                    break;
                case Ponytail:
                    d = x > 74f && y > 36f && y < 100f ? Strand(Tail, x, y) : 99f;
                    break;
                case Curls:
                    d = RoundBox(x, y, 37f, 32f, 91f, 102f, 18f);
                    break;
                case Afro:
                    d = AfroSdf(x, y);
                    break;
                default:
                    d = RoundBox(x, y, 38f, 34f, 90f, 118f, 12f);
                    break;
            }
            float c = Cover(d * k);
            if (c <= 0f)
            {
                return;
            }
            if (f.Style == Afro || f.Style == Ponytail)
            {
                HairTone(ref f, x, y, k, f.Style == Afro ? 15f : 26f, out float ar, out float ag, out float ab);
                // A tie where the tail leaves the head, in the clothes' colour like a scrunchie.
                Over(ref ar, ref ag, ref ab, f.ClothShade, f.Style == Ponytail ? Cover(Tilted(x, y, 85.6f, 50.5f, 4.2f, 1.6f, 0.423f, 0.906f) * k) : 0f);
                Over(ref r, ref g, ref b, ar, ag, ab, c);
                return;
            }
            Over(ref r, ref g, ref b, f.HairShade, c);
        }

        // A cloud of hair: a circle with puffs round its rim, looked up only near the rim, where they change the edge.
        private static float AfroSdf(float x, float y)
        {
            float c = Len(x - 64f, y - 45f);
            if (c > 35.5f)
            {
                return c - 34.3f;
            }
            float d = c > 28f ? Math.Min(c - 30.5f, Bumps(AfroPuffs, 2, 4.8f, Solid, x, y)) : c - 30.5f;
            return SMax(d, y - 74f, 3f);
        }

        // The hair over the head and face, as a signed distance (negative inside). ax is x's distance from the middle.
        private static float FrontHairSdf(ref Face f, float x, float y, float ax)
        {
            switch (f.Style)
            {
                case Crop:
                {
                    float d = SMax(Ellipse(x, y, 64f, 47.5f, 23.2f, 22.5f), -Ellipse(x, y, 64f, 62.5f, 19.2f, 24.2f), 1.5f);
                    float tufts = Math.Min(Circle(x, y, 57.6f, 38.9f, 2.1f), Math.Min(Circle(x, y, 63.6f, 39.7f, 2.4f), Circle(x, y, 69.9f, 39f, 2f)));
                    return EarCut(SMin(d, tufts, 1.2f), ax, y);
                }
                case SidePart:
                {
                    float crown = SMin(Ellipse(x, y, 64f, 47.2f, 23.5f, 22.8f), Ellipse(x, y, 61f, 31.5f, 18.5f, 9f), 4f);
                    return EarCut(SMax(crown, -Ellipse(x, y, 65.5f, 62.5f, 19f, 24.3f), 1.5f), ax, y);
                }
                case Quiff:
                {
                    // Close to the skull at the sides, all the volume on top and pushed up and back: a man's cut, not an updo.
                    float cap = Ellipse(x, y, 64f, 50.3f, f.Crown + 0.5f, 24f);
                    float top = TiltedBox(x, y, 65f, 31f, 16.5f, 7.5f, 6f, 0.985f, -0.174f);
                    return EarCut(SMax(SMin(cap, top, 2.5f), -Ellipse(x, y, 64f, 62.5f, 19.2f, 24.6f), 1.2f), ax, y);
                }
                case Buzz:
                    return EarCut(SMax(Ellipse(x, y, 64f, 50.3f, f.Crown + 0.7f, 24.1f), -Ellipse(x, y, 64f, 62.5f, 19.4f, 24.8f), 1f), ax, y - 1f);
                case Receding:
                {
                    float bare = SMin(Ellipse(x, y, 64f, 61f, 18.4f, 25.5f), Ellipse(x, y, 64f, 30.5f, 15.5f, 12.5f), 3f);
                    return EarCut(SMax(Ellipse(x, y, 64f, 48f, 22.8f, 22.3f), -bare, 1.5f), ax, y);
                }
                case CurlyTop:
                {
                    float d = SMax(Ellipse(x, y, 64f, 47.5f, 22.6f, 22f), -Ellipse(x, y, 64f, 62.5f, 19f, 24.2f), 1.5f);
                    d = y < 47.5f ? Math.Min(d, Bumps(TopCurls, 2, 5.4f, Solid, x, y)) : d;
                    return EarCut(Math.Min(d, Math.Min(Circle(x, y, 58f, 37.5f, 3.4f), Circle(x, y, 69.5f, 37.2f, 3.2f))), ax, y);
                }
                case ShortAfro:
                {
                    float d = Ellipse(x, y, 64f, 43.5f, 26f, 23f);
                    d = y < 45.5f ? Math.Min(d, Bumps(ShortPuffs, 2, 4.6f, Solid, x, y)) : d;
                    return EarCut(SMax(d, -Ellipse(x, y, 64f, 62.5f, 19f, 24.2f), 1.5f), ax, y);
                }
                case Long:
                {
                    float crown = SMax(Ellipse(x, y, 64f, 46.5f, 24f, 23.2f), -Arch(x, y), 1.2f);
                    float locks = SMax(RoundBox(64f + ax, y, 79.5f, 44f, 91f, 112f, 6f), -Ellipse(x, y, 64f, 63f, 17.2f, 27f), 1.5f);
                    return SMin(crown, locks, 3f);
                }
                case Bob:
                {
                    float helmet = SMin(Ellipse(x, y, 64f, 47f, 24.6f, 23.6f), RoundBox(x, y, 39f, 44f, 89f, 81f, 9.5f), 4f);
                    float face = SMin(RoundBox(x, y, 47.2f, 44.8f, 80.8f, 74f, 5f), Ellipse(x, y, 64f, 62.5f, 15.8f, 20.5f), 3f);
                    return SMax(helmet, -face, 1.2f);
                }
                case Ponytail:
                case Bun:
                {
                    float d = EarCut(SMax(Ellipse(x, y, 64f, 47.8f, 22.9f, 22.6f), -Ellipse(x, y, 64f, 62.3f, 18.8f, 24f), 1.5f), ax, y);
                    return f.Style == Bun ? SMin(d, Circle(x, y, 64f, 20.5f, 9.2f), 2.5f) : d;
                }
                case Curls:
                {
                    float d = Ellipse(x, y, 64f, 46f, 24.6f, 23.6f);
                    d = y < 45.5f ? Math.Min(d, Bumps(CrownCurls, 2, 6.2f, Solid, x, y)) : d;
                    d = SMax(d, -Ellipse(x, y, 64f, 63f, 18.5f, 25f), 1.2f);
                    d = Math.Min(d, Math.Min(Circle(x, y, 57.5f, 38.5f, 3.6f), Circle(x, y, 70.5f, 38f, 3.4f)));
                    if (ax > 14f && y > 46f)
                    {
                        // A mass down each side with a scalloped edge, the way flat art draws curls, kept off the face.
                        float edge = Bumps(x > 64f ? RightScallops : LeftScallops, 3, 0f, Solid, x, y);
                        d = Math.Min(d, SMax(Math.Min(RoundBox(64f + ax, y, 78f, 46f, 92f, 104f, 7f), edge), -Ellipse(x, y, 64f, 63f, 17.2f, 27f), 1f));
                    }
                    return d;
                }
                case Afro:
                    return SMax(SMax(AfroSdf(x, y), -Ellipse(x, y, 64f, 62.5f, 18.9f, 24.4f), 1.5f), HeadSdf(ref f, x, y) - 0.5f, 0.5f);
                case Braids:
                {
                    // Two plaits a side, each a chain of links leaning left and right in turn.
                    float mx = 64f + ax, braid = 99f;
                    int link = (int)Math.Round((y - 52f) / 3.6f);
                    for (int i = Math.Max(0, link - 1); i <= Math.Min(19, link + 1); i++)
                    {
                        float ly = 52f + i * 3.6f, lean = (i & 1) == 0 ? 0.423f : -0.423f;
                        braid = Math.Min(braid, Math.Min(Tilted(mx, y, 85.6f, ly, 2.1f, 2.9f, 0.906f, lean), Tilted(mx, y, 90.6f, ly + 1.8f, 2.1f, 2.9f, 0.906f, -lean)));
                    }
                    return Math.Min(SMax(Ellipse(x, y, 64f, 46.5f, 23.6f, 23.2f), -Arch(x, y), 1.2f), braid);
                }
                default:
                {
                    // A pixie: short, but over the ears to a point at the cheek, with a long fringe swept down to the left temple.
                    float d = SMin(Ellipse(x, y, 64f, 47.5f, 23.6f, 23.2f), Taper(64f + ax, y, 85f, 50f, 3.6f, 82.8f, 66f, 1.4f), 3f);
                    d = SMax(d, -Ellipse(x, y, 64f, 63f, 18.6f, 24.5f), 1.5f);
                    return SMin(d, Tilted(x, y, 60f, 40.5f, 15f, 5.6f, 0.961f, -0.276f), 2f);
                }
            }
        }

        private static void FrontHair(ref Face f, float x, float y, float ax, float k, float d, ref float r, ref float g, ref float b)
        {
            float c = Cover(d * k);
            float lit = (16f - ((x - 64f) * 0.9f + (y - 50f) * 0.43f)) * k;
            if (f.Style == Buzz)
            {
                Load(f.Buzz, out float zr, out float zg, out float zb);
                Over(ref zr, ref zg, ref zb, f.BuzzShade, Cover(lit));
                Over(ref r, ref g, ref b, zr, zg, zb, c);
                return;
            }
            HairTone(ref f, x, y, k, f.Style == Long || f.Style == Braids || f.Style == Curls ? 13f : 15f, out float mr, out float mg, out float mb);
            if (f.Style == Quiff || f.Style == CurlyTop)
            {
                // Faded short sides under the volume on top, a barber's cut.
                Load(f.Buzz, out float zr, out float zg, out float zb);
                Over(ref zr, ref zg, ref zb, f.BuzzShade, Cover(lit));
                Over(ref mr, ref mg, ref mb, zr, zg, zb, Cover(Math.Max(43f - y, 15.8f - ax) * k));
            }
            float line = 99f;
            switch (f.Style)
            {
                case SidePart:
                    line = Capsule(x, y, 54.5f, 27.5f, 56f, 35.5f, 0.55f);
                    break;
                case Long:
                case Braids:
                    line = Capsule(x, y, 64f, 26.5f, 64f, 33f, 0.5f);
                    break;
                case Bob:
                    // Three strands on the fringe, so it reads as hair cut straight and not a cap.
                    line = Math.Min(Capsule(x, y, 57f, 39.5f, 56.7f, 44.5f, 0.45f), Math.Min(Capsule(x, y, 64f, 39.5f, 64f, 44.5f, 0.45f), Capsule(x, y, 71f, 39.5f, 71.3f, 44.5f, 0.45f)));
                    break;
                case Bun:
                    line = Math.Min(Math.Max(Math.Abs(Circle(x, y, 64f, 22.5f, 5.5f)) - 0.5f, 22.5f - y), Ellipse(x, y, 64f, 28.6f, 5.8f, 1.5f));
                    break;
                case Pixie:
                    line = Capsule(x, y, 71.5f, 27f, 70f, 34.5f, 0.5f);
                    break;
            }
            Over(ref mr, ref mg, ref mb, f.HairDeep, Cover(line * k));
            bool odd = f.Style == Braids && y > 50f && ax > 18f && ((int)Math.Floor((y - 50.2f) / 3.6f) & 1) == 1;
            Over(ref mr, ref mg, ref mb, f.HairDeep, odd ? 0.5f : 0f);   // every other link a tone down, so the plait reads
            Over(ref r, ref g, ref b, mr, mg, mb, c);
        }

        // Hair's base, its shade below and right of a line, and a shine on the upper left: an arc on smooth hair, glints on curls.
        private static void HairTone(ref Face f, float x, float y, float k, float edge, out float r, out float g, out float b)
        {
            Load(f.Hair, out r, out g, out b);
            Over(ref r, ref g, ref b, f.HairShade, Cover((edge - ((x - 64f) * 0.9f + (y - 50f) * 0.43f)) * k));
            switch (f.Style)
            {
                case CurlyTop:
                case ShortAfro:
                {
                    float[] curls = f.Style == CurlyTop ? TopCurls : ShortPuffs;
                    float size = f.Style == CurlyTop ? 5.4f : 4.6f;
                    Over(ref r, ref g, ref b, f.HairDeep, y < 47.5f ? Cover(Bumps(curls, 2, size, Rim, x, y) * k) : 0f);
                    Over(ref r, ref g, ref b, f.HairLit, y < 47.5f ? Cover(Bumps(curls, 2, size, Glint, x, y) * k) : 0f);
                    return;
                }
                case Curls:
                {
                    // Long curls by their edges and a few strokes inside, no glint on each, which turns them into beads.
                    float rims = y < 45.5f ? Bumps(CrownCurls, 2, 6.2f, Rim, x, y) : 99f;
                    if (y > 46f)
                    {
                        bool right = x > 64f;
                        rims = Math.Min(rims, Math.Min(Bumps(right ? RightScallops : LeftScallops, 3, 0f, Rim, x, y), Bumps(right ? RightMarks : LeftMarks, 2, 3.6f, Rim, x, y)));
                    }
                    Over(ref r, ref g, ref b, f.HairShade, Cover(rims * k));
                    break;
                }
                case Afro:
                {
                    bool rim = Math.Abs(Len(x - 64f, y - 45f) - 29.5f) < 5.2f;
                    Over(ref r, ref g, ref b, f.HairShade, rim ? Cover(Bumps(AfroPuffs, 2, 4.8f, Rim, x, y) * k) : 0f);
                    Over(ref r, ref g, ref b, f.HairLit, Cover(Bumps(AfroLights, 3, 0f, Glint, x, y) * k));
                    return;
                }
            }
            float shine = Arc(x - 64f, y - 47.5f, -0.62f, -0.78f, 17.5f, 1.6f, 0.375f, 0.927f);
            shine = f.Style == Ponytail ? Math.Min(shine, Capsule(x, y, 85.5f, 55f, 86.5f, 72f, 0.8f)) : shine;
            Over(ref r, ref g, ref b, f.HairLit, Cover(shine * k));
        }

        // Curls and puffs, one circle each (x, y pairs of one radius, or x, y, radius triples): the circles themselves, a thin
        // dark arc on each one's lower right so a cluster reads as curls and not one blob, or a glint in each on the lit side.
        private static float Bumps(float[] at, int stride, float radius, int kind, float x, float y)
        {
            float d = 99f;
            for (int i = 0; i < at.Length; i += stride)
            {
                float cx = at[i], cy = at[i + 1], r = stride == 3 ? at[i + 2] : radius;
                d = Math.Min(d, kind == Solid ? Circle(x, y, cx, cy, r) : kind == Rim ? Arc(x - cx, y - cy, 0.6f, 0.8f, r - 0.6f, 0.45f, 0.94f, 0.342f)
                    : cx < 76f ? Circle(x, y, cx - r * 0.32f, cy - r * 0.36f, r * 0.32f) : 99f);
            }
            return d;
        }

        // A strand through x, y, radius triples, tapering from one to the next.
        private static float Strand(float[] xyr, float x, float y)
        {
            float d = 99f;
            for (int i = 0; i + 5 < xyr.Length; i += 3)
            {
                d = Math.Min(d, Taper(x, y, xyr[i], xyr[i + 1], xyr[i + 2], xyr[i + 3], xyr[i + 4], xyr[i + 5]));
            }
            return d;
        }

        // The face's opening under a centre part: two ellipses crossed, so the hairline peaks in the middle.
        private static float Arch(float x, float y) => SMax(Ellipse(x, y, 66.5f, 63f, 19.5f, 25.5f), Ellipse(x, y, 61.5f, 63f, 19.5f, 25.5f), 1f);

        // Short hair stops above the ears, so they show.
        private static float EarCut(float d, float ax, float y) => Math.Max(d, Math.Min(y - 56.5f, ax - 16.8f));

        // 1 a woman, 2 a man, 0 when the first name does not say: a title first, then the built in names, then the ending.
        private static int Presented(string name)
        {
            int at = 0;
            for (int word = 0; name != null && word < 3; word++)
            {
                while (at < name.Length && !char.IsLetter(name[at]))
                {
                    at++;
                }
                int start = at;
                uint h = 2166136261;
                while (at < name.Length && char.IsLetter(name[at]))
                {
                    h = unchecked((h ^ char.ToLowerInvariant(name[at])) * 16777619);
                    at++;
                }
                int key = unchecked((int)h);
                if (at == start || Array.BinarySearch(Titles, key) < 0)
                {
                    return at == start ? 0 : Array.BinarySearch(Women, key) >= 0 ? 1 : Array.BinarySearch(Men, key) >= 0 ? 2 : at - start < 3 ? 0 : Ending(name, at);
                }
            }
            return 0;
        }

        // An unknown name's ending: Spanish, Italian and Slavic names in a for women and o for men, Japanese ko for women.
        private static int Ending(string name, int end)
        {
            char last = char.ToLowerInvariant(name[end - 1]), before = char.ToLowerInvariant(name[end - 2]), third = char.ToLowerInvariant(name[end - 3]);
            bool feminine = last == 'e' && ((before == 'n' && (third == 'i' || third == 'n')) || (before == 'l' && third == 'l') || (before == 't' && third == 't'));
            return last == 'a' || (last == 'o' && before == 'k') || feminine ? 1 : last == 'o' || (last == 's' && (before == 'u' || before == 'o')) ? 2 : 0;
        }

        // The names' hashes, sorted for the binary search: worked out once when the type loads, so splitting is fine here.
        private static int[] Keys(string words)
        {
            string[] names = words.Split(' ');
            var keys = new int[names.Length];
            for (int i = 0; i < names.Length; i++)
            {
                uint h = 2166136261;
                for (int j = 0; j < names[i].Length; j++)
                {
                    h = unchecked((h ^ names[i][j]) * 16777619);
                }
                keys[i] = unchecked((int)h);
            }
            Array.Sort(keys);
            return keys;
        }

        private static float[] Ring(float cx, float cy, float rx, float ry, float from, float to, int count)
        {
            var xy = new float[count * 2];
            for (int i = 0; i < count; i++)
            {
                double t = (from + (to - from) * i / (count - 1)) * Math.PI / 180.0;
                xy[i * 2] = cx + rx * (float)Math.Cos(t);
                xy[i * 2 + 1] = cy + ry * (float)Math.Sin(t);
            }
            return xy;
        }

        // A xorshift step: the same faces on every runtime, which System.Random does not promise.
        private static int Roll(ref uint s, int n)
        {
            s ^= s << 13;
            s ^= s >> 17;
            s ^= s << 5;
            return (int)((s >> 8) % (uint)n);
        }

        private static int Odds(ref uint s, byte[] odds)
        {
            int total = 0;
            for (int i = 0; i < odds.Length; i++)
            {
                total += odds[i];
            }
            int roll = Roll(ref s, total);
            for (int i = 0; i < odds.Length - 1; i++)
            {
                roll -= odds[i];
                if (roll < 0)
                {
                    return i;
                }
            }
            return odds.Length - 1;
        }

        // The disc: a slate blue at the skin's lightness, leaning a little to the person's hue.
        private static uint Ground(int skin, float hue)
        {
            double h = hue * Math.PI / 180.0, slate = 245.0 * Math.PI / 180.0;
            double a = 0.04 * Math.Cos(slate) + 0.01 * Math.Cos(h), b = 0.04 * Math.Sin(slate) + 0.01 * Math.Sin(h);
            return Pack(UiColour.FromOklch(DiscL[skin], (float)Math.Sqrt(a * a + b * b), (float)(Math.Atan2(b, a) * 180.0 / Math.PI)));
        }

        // A tone of a colour in OKLab: lighter or darker by dl, chroma scaled by 1 + dc, hue turned by dh degrees, b nudged by db.
        private static uint Tone(uint c, float dl, float dc, float dh, float db)
        {
            float l = Lab(c, out float a, out float b);
            double chroma = Math.Sqrt(a * a + b * b) * (1f + dc), hue = Math.Atan2(b, a) + dh * Math.PI / 180.0;
            double na = chroma * Math.Cos(hue), nb = chroma * Math.Sin(hue) + db;
            return Pack(UiColour.FromOklch(Math.Max(0f, Math.Min(1f, l + dl)), (float)Math.Sqrt(na * na + nb * nb), (float)(Math.Atan2(nb, na) * 180.0 / Math.PI)));
        }

        // A colour's OKLab lightness, with its a and b.
        private static float Lab(uint c, out float a, out float b)
        {
            double r = Linear(((c >> 24) & 0xFF) / 255.0), g = Linear(((c >> 16) & 0xFF) / 255.0), bl = Linear(((c >> 8) & 0xFF) / 255.0);
            double lm = Math.Pow(0.4122214708 * r + 0.5363325363 * g + 0.0514459929 * bl, 1.0 / 3.0);
            double mm = Math.Pow(0.2119034982 * r + 0.6806995451 * g + 0.1073969566 * bl, 1.0 / 3.0);
            double sm = Math.Pow(0.0883024619 * r + 0.2817188376 * g + 0.6299787005 * bl, 1.0 / 3.0);
            a = (float)(1.9779984951 * lm - 2.4285922050 * mm + 0.4505937099 * sm);
            b = (float)(0.0259040371 * lm + 0.7827717662 * mm - 0.8086757660 * sm);
            return (float)(0.2104542553 * lm + 0.7936177850 * mm - 0.0040720468 * sm);
        }

        private static double Linear(double c) => c <= 0.04045 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);

        private static float Contrast(uint a, uint b) => UiColour.Contrast(UiColour.FromRgba(a), UiColour.FromRgba(b));

        private static uint Mix(uint a, uint b, float t) => (Channel(a >> 24, b >> 24, t) << 24) | (Channel(a >> 16, b >> 16, t) << 16) | (Channel(a >> 8, b >> 8, t) << 8) | 0xFF;

        private static uint Channel(uint a, uint b, float t) => (uint)((a & 0xFF) * (1f - t) + (b & 0xFF) * t + 0.5f);

        private static uint Pack(UiColour c) => ((uint)(c.R * 255f + 0.5f) << 24) | ((uint)(c.G * 255f + 0.5f) << 16) | ((uint)(c.B * 255f + 0.5f) << 8) | 0xFF;

        private static void Load(uint c, out float r, out float g, out float b)
        {
            r = ((c >> 24) & 0xFF) / 255f;
            g = ((c >> 16) & 0xFF) / 255f;
            b = ((c >> 8) & 0xFF) / 255f;
        }

        // Paints a colour over what is there by a coverage of 0 to 1.
        private static void Over(ref float r, ref float g, ref float b, uint c, float cover)
        {
            if (cover > 0f)
            {
                r += (((c >> 24) & 0xFF) / 255f - r) * cover;
                g += (((c >> 16) & 0xFF) / 255f - g) * cover;
                b += (((c >> 8) & 0xFF) / 255f - b) * cover;
            }
        }

        private static void Over(ref float r, ref float g, ref float b, float cr, float cg, float cb, float cover)
        {
            if (cover > 0f)
            {
                r += (cr - r) * cover;
                g += (cg - g) * cover;
                b += (cb - b) * cover;
            }
        }

        private static byte Byte(float v) => (byte)(Math.Max(0f, Math.Min(1f, v)) * 255f + 0.5f);

        // A distance's coverage of a texel: soft over one texel, as the kit's other shapes are.
        private static float Cover(float d) => Math.Max(0f, Math.Min(1f, 0.5f - d));

        private static float Len(float x, float y) => (float)Math.Sqrt(x * x + y * y);

        private static float Circle(float x, float y, float cx, float cy, float r) => Len(x - cx, y - cy) - r;

        // An ellipse's distance, close enough at its edge for a soft texel even when long and thin.
        private static float Ellipse(float x, float y, float cx, float cy, float rx, float ry)
        {
            float px = (x - cx) / rx, py = (y - cy) / ry;
            float k0 = Len(px, py), k1 = Len(px / rx, py / ry);
            return k1 > 1e-6f ? k0 * (k0 - 1f) / k1 : -Math.Min(rx, ry);
        }

        // An ellipse turned by an angle given as its cosine and sine, clockwise on screen.
        private static float Tilted(float x, float y, float cx, float cy, float rx, float ry, float cos, float sin)
        {
            float dx = x - cx, dy = y - cy;
            return Ellipse(dx * cos + dy * sin, -dx * sin + dy * cos, 0f, 0f, rx, ry);
        }

        private static float Capsule(float x, float y, float ax, float ay, float bx, float by, float r)
        {
            float px = x - ax, py = y - ay, qx = bx - ax, qy = by - ay;
            float h = Math.Max(0f, Math.Min(1f, (px * qx + py * qy) / (qx * qx + qy * qy)));
            return Len(px - qx * h, py - qy * h) - r;
        }

        // A capsule whose two ends have their own radii, its distance exact, so a strand tapers without a kink.
        private static float Taper(float x, float y, float ax, float ay, float ra, float bx, float by, float rb)
        {
            float px = x - ax, py = y - ay, qx = bx - ax, qy = by - ay;
            float h = qx * qx + qy * qy;
            float u = Math.Abs(px * qy - py * qx) / h, v = (px * qx + py * qy) / h;
            float cx = (float)Math.Sqrt(h - (ra - rb) * (ra - rb)), cy = ra - rb;
            float cross = cx * v - cy * u, n = u * u + v * v;
            return cross < 0f ? (float)Math.Sqrt(h * n) - ra : cross > cx ? (float)Math.Sqrt(h * (n + 1f - 2f * v)) - rb : cx * u + cy * v - ra;
        }

        private static float RoundBox(float x, float y, float x0, float y0, float x1, float y1, float r)
        {
            float qx = Math.Abs(x - (x0 + x1) / 2f) - ((x1 - x0) / 2f - r), qy = Math.Abs(y - (y0 + y1) / 2f) - ((y1 - y0) / 2f - r);
            float ox = Math.Max(qx, 0f), oy = Math.Max(qy, 0f);
            return (float)Math.Sqrt(ox * ox + oy * oy) + Math.Min(Math.Max(qx, qy), 0f) - r;
        }

        // A rounded box round a centre, turned by an angle given as its cosine and sine, clockwise on screen.
        private static float TiltedBox(float x, float y, float cx, float cy, float hw, float hh, float r, float cos, float sin)
        {
            float dx = x - cx, dy = y - cy;
            return RoundBox(dx * cos + dy * sin, -dx * sin + dy * cos, -hw, -hh, hw, hh, r);
        }

        // A convex four sided shape, corners clockwise on screen: the farthest of its edges' lines.
        private static float Quad(float x, float y, float x0, float y0, float x1, float y1, float x2, float y2, float x3, float y3) =>
            Math.Max(Math.Max(Side(x, y, x0, y0, x1, y1), Side(x, y, x1, y1, x2, y2)), Math.Max(Side(x, y, x2, y2, x3, y3), Side(x, y, x3, y3, x0, y0)));

        private static float Side(float x, float y, float x0, float y0, float x1, float y1) => ((x - x0) * (y1 - y0) - (y - y0) * (x1 - x0)) / Len(x1 - x0, y1 - y0);

        // A smile: the lower part of a circle round its centre, out to a half angle given by its sine and cosine.
        private static float Smile(float px, float py, float radius, float half, float sin, float cos)
        {
            px = Math.Abs(px);
            return (cos * px > sin * py ? Len(px - sin * radius, py - cos * radius) : Math.Abs(Len(px, py) - radius)) - half;
        }

        // An arc round a centre facing a direction, out to a half angle given by its sine and cosine.
        private static float Arc(float px, float py, float dirX, float dirY, float radius, float half, float sin, float cos) =>
            Smile(px * dirY - py * dirX, px * dirX + py * dirY, radius, half, sin, cos);

        private static float SMin(float a, float b, float k)
        {
            float h = Math.Max(0f, Math.Min(1f, 0.5f + 0.5f * (b - a) / k));
            return b + (a - b) * h - k * h * (1f - h);
        }

        private static float SMax(float a, float b, float k) => -SMin(-a, -b, k);
    }
}
