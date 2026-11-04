namespace RealLifeOverhaul.Ui
{
    /// <summary>The game's boxes the kit draws over: the full menu's question box, the HUD's, and the acknowledgement.</summary>
    public enum ShellBox
    {
        MenuAsk = 0,
        HudAsk = 1,
        Acknowledge = 2,
    }

    /// <summary>What the look at the game's interface walks (ShellBoxes' scene; a recording one offline).</summary>
    public interface IShellBoxScene
    {
        /// <summary>True once the game's interface is there to be looked at; before that a look is not counted.</summary>
        bool Built { get; }

        /// <summary>One walk of the interface for this box: true when it is there now.</summary>
        bool Holds(ShellBox box);
    }

    /// <summary>
    /// When the kit looks for the game's boxes under its interface, game-free. The first look of a city is made as the
    /// interface is on; a box not there then (the game builds some of its boxes late, or only in another scene) is looked
    /// for again only while a box of its kind stands open, a few times a city at most, so a box built late is bound when
    /// it is first needed and no search runs while none is up. A box found stays found until the next city (Rearm).
    /// Cost: a few comparisons; one walk of the interface per look.
    /// </summary>
    public sealed class ShellBoxLook
    {
        public const int MostLooks = 6;   // walks per box in a city: the first, then five while a box of its kind is up

        private const int Kinds = 3;
        private readonly bool[] _found = new bool[Kinds];
        private readonly int[] _looks = new int[Kinds];
        private readonly bool[] _toldMiss = new bool[Kinds];
        private readonly bool[] _toldLate = new bool[Kinds];

        /// <summary>The walks made for this box in this city.</summary>
        public int Looks(ShellBox box) => _looks[(int)box];

        /// <summary>True when the box has been found in this city.</summary>
        public bool Found(ShellBox box) => _found[(int)box];

        /// <summary>A new city: every box is looked for again.</summary>
        public void Rearm()
        {
            for (int i = 0; i < Kinds; i++)
            {
                _found[i] = false;
                _looks[i] = 0;
                _toldMiss[i] = false;
                _toldLate[i] = false;
            }
        }

        /// <summary>
        /// Whether the box is there. <paramref name="open"/> is true while a box of its kind stands open (the game's own
        /// flag, a static read): a miss at the first look is looked at again only then.
        /// </summary>
        public bool Seek(ShellBox box, bool open, IShellBoxScene scene)
        {
            int i = (int)box;
            if (_found[i])
            {
                return true;
            }
            if (!scene.Built || (_looks[i] > 0 && (!open || _looks[i] >= MostLooks)))
            {
                return false;
            }
            _looks[i]++;
            _found[i] = scene.Holds(box);
            return _found[i];
        }

        /// <summary>True once per city, after the first look found nothing: the line to log.</summary>
        public bool FirstMiss(ShellBox box)
        {
            int i = (int)box;
            if (_found[i] || _looks[i] == 0 || _toldMiss[i])
            {
                return false;
            }
            _toldMiss[i] = true;
            return true;
        }

        /// <summary>True once per city, when the box was found at a later look than the first.</summary>
        public bool FoundLate(ShellBox box)
        {
            int i = (int)box;
            if (!_found[i] || _looks[i] < 2 || _toldLate[i])
            {
                return false;
            }
            _toldLate[i] = true;
            return true;
        }
    }
}
