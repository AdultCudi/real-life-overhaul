namespace RealLifeOverhaul.Ui
{
    /// <summary>A skill's glyph by its key (the part of reallife_study_skill_{key} after the prefix): each skill its own, so a page of skills is not one repeated icon. Game-free.</summary>
    public static class WorkSkills
    {
        /// <summary>The diary's entries as an array, a heart (the red of a warning on the page's dark) drawn in the warm tone so it stands out from what is under it.</summary>
        public static UiDiaryEntry[] Tinted(System.Collections.Generic.List<UiDiaryEntry> entries)
        {
            UiDiaryEntry[] all = entries.ToArray();
            for (int i = 0; i < all.Length; i++)
            {
                all[i].Tint = all[i].Icon == UiIcon.Heart ? UiRole.Warn : all[i].Tint;
            }
            return all;
        }

        public static UiIcon Glyph(string key)
        {
            switch (key)
            {
                case "electrical": return UiIcon.Lightning;
                case "plumbing": return UiIcon.Drop;
                case "joinery": return UiIcon.Hammer;
                case "masonry": return UiIcon.Building;
                case "cooking": return UiIcon.ForkKnife;
                case "hair": return UiIcon.PaintBrush;
                case "beauty": return UiIcon.Star;
                case "care": return UiIcon.Heart;
                case "clinical": return UiIcon.ClipboardText;
                case "first_aid": return UiIcon.Heartbeat;
                case "teaching": return UiIcon.Presentation;
                case "hospitality": return UiIcon.Coffee;
                case "guarding": return UiIcon.ShieldCheck;
                case "handling": return UiIcon.Package;
                case "driving": return UiIcon.Car;
                case "coding": return UiIcon.Code;
                case "system_design": return UiIcon.Cpu;
                case "front_end": return UiIcon.Monitor;
                case "back_end": return UiIcon.Database;
                case "mobile": return UiIcon.Phone;
                case "automation": return UiIcon.Gear;
                case "reliability": return UiIcon.ShieldCheck;
                case "pipelines": return UiIcon.HardDrives;
                case "statistics": return UiIcon.ChartPie;
                case "machine_learning": return UiIcon.Brain;
                case "security_ops": return UiIcon.Lock;
                case "testing": return UiIcon.Bug;
                case "troubleshooting": return UiIcon.Wrench;
                case "networking": return UiIcon.Globe;
                case "product_sense": return UiIcon.Lightbulb;
                case "user_research": return UiIcon.Search;
                case "visual_design": return UiIcon.Image;
                case "writing": return UiIcon.Pencil;
                case "selling": return UiIcon.ShoppingBag;
                case "relationships": return UiIcon.Handshake;
                case "leadership": return UiIcon.People;
                case "delivery": return UiIcon.Truck;
                case "accounting": return UiIcon.ChartLineUp;
                case "law": return UiIcon.Scales;
                case "recruiting": return UiIcon.IdCard;
                case "marketing": return UiIcon.Megaphone;
                case "analysis": return UiIcon.Chart;
                case "structures": return UiIcon.Building;
                case "site_planning": return UiIcon.MapTrifold;
                default: return UiIcon.Cap;
            }
        }
    }
}
