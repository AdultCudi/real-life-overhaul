namespace RealLifeOverhaul.Ui
{
    /// <summary>The picture each kind of firm, product, work and genre has in the Tech app: one glyph a kind, in its own colour on a slate tile (RLO-UI-017). Game-free.</summary>
    public static class TechGlyphs
    {
        // Indexed by FirmKind, ProductKind, WorkKind and Genre as the catalogues number them; EconomyCheck holds the counts to the catalogues'.
        public static readonly UiIcon[] Firm = { UiIcon.Code, UiIcon.GameController, UiIcon.Handshake, UiIcon.ShieldCheck, UiIcon.Brain, UiIcon.Globe, UiIcon.PaintBrush };
        public static readonly UiIcon[] Product = { UiIcon.Cloud, UiIcon.Phone, UiIcon.Key, UiIcon.GameController, UiIcon.Brain };
        public static readonly UiIcon[] Work = { UiIcon.Globe, UiIcon.Arrows, UiIcon.Gear, UiIcon.ClipboardText, UiIcon.ShieldCheck, UiIcon.Fire, UiIcon.PaintBrush };
        public static readonly UiIcon[] Genre =
        {
            UiIcon.GenreAction, UiIcon.GenreAdventure, UiIcon.GenreRpg, UiIcon.GenreStrategy, UiIcon.GenreSimulation, UiIcon.GenreSports, UiIcon.GenreRacing, UiIcon.GenrePuzzle,
            UiIcon.GenreHorror, UiIcon.GenreShooter, UiIcon.GenreCasual, UiIcon.GenreFamily
        };

        public static UiIcon Of(UiIcon[] set, int index) => set[index < 0 ? 0 : index >= set.Length ? set.Length - 1 : index];

        public const UiIcon Centre = UiIcon.HardDrives, Startup = UiIcon.Rocket, Cloud = UiIcon.Cloud, Warehouse = UiIcon.Package;
    }
}
