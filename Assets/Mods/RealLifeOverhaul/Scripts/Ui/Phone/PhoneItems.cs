using System;
using RealLifeOverhaul.GameUi;

namespace RealLifeOverhaul.Ui
{
    /// <summary>Whose an app on the phone is.</summary>
    public enum PhoneItemKind : byte
    {
        Game = 0,    // one of the game's eight
        Mod = 1,     // one of this mod's apps
        Other = 2    // another mod's button
    }

    /// <summary>Where an app's own button stands in the game: the phone's grid, or a place only another mod uses.</summary>
    public enum PhoneItemFrom : byte
    {
        Grid = 0,    // the HUD phone's grid, where the game's, the mod's and most other mods' buttons stand
        Row = 1,     // the full menu's own row of app buttons alone: a mod that put its app beside the game's there
        Frame = 2    // the HUD phone itself, outside its grid: a button a mod hung on the phone, which the kit phone covers
    }

    /// <summary>One app the phone lists: its button's name, its words, its look, its badge and where it stands.</summary>
    public struct PhoneItem
    {
        public string Id;           // the button's object name: an AppName ("Persona"), the mod's app id, another mod's own ("BAMP_Chat")
        public string Title;        // as its button shows it, in the game's language
        public PhoneItemKind Kind;
        public UiIcon Icon;         // the kit glyph (PhoneLooks); None for another mod's app, which shows its own picture
        public UiRole Tint;         // the glyph's colour; None for another mod's
        public int Picture;         // another mod's own picture (UiPictures); 0 or less for none
        public int Badge;           // the number on its badge; 0 for none
        public int Folder;          // the folder it stands in (PhoneLayoutRules.Money .. Setup); -1 on the home screen
        public bool First;          // the player keeps it first on the home screen (the old dock)
        public PhoneItemFrom From;  // where its button stands; appended (13 Nov 2026), the grid for every app read before
    }

    /// <summary>
    /// Every app the phone lists, in the phone's one order (the game's, the mod's in PagerRules.ModOrder, other mods' as
    /// they joined: those of the grid, then those a mod put on the full menu's row or on the phone itself, which have no
    /// button of the grid), read by the kit phone, the classic pager's checks, and the status bar's app row and All apps. The
    /// home screen and each folder are the layout rules' views (PhoneLayoutRules.View), so every phone shows the same
    /// order: the two kept first, the game's eight, the folders, the apps put on the home screen, other mods' buttons.
    /// <see cref="Version"/> goes up on any change, so a reader lays out again only when it moved. Game-free: the game file
    /// (PhoneItemsGame) fills it. Cost: a view is a pass over the apps, nothing allocated.
    /// </summary>
    public sealed class PhoneItems
    {
        /// <summary>The phone's apps while the HUD phone stands; null before and offline.</summary>
        public static PhoneItems Current;

        public PhoneItem[] Items = new PhoneItem[48];
        public int Count;

        /// <summary>Goes up whenever an app comes or goes, or a title, a badge, a picture or a place changes.</summary>
        public int Version;

        private int[] _kind = new int[48];
        private int[] _place = new int[48];
        private readonly int[] _members = new int[PhoneLayoutRules.FolderCount];
        private int[] _scratch = new int[64];

        /// <summary>Empties the list for a new reading, keeping its arrays.</summary>
        public void Begin()
        {
            Count = 0;
        }

        /// <summary>The next item to fill, the arrays grown when full.</summary>
        public ref PhoneItem Add()
        {
            if (Count == Items.Length)
            {
                Array.Resize(ref Items, Items.Length * 2);
            }
            Items[Count] = default(PhoneItem);
            Items[Count].Folder = -1;
            return ref Items[Count++];
        }

        /// <summary>The reading is complete: the rules' arrays follow it, and the version goes up when anything differs.</summary>
        public void End(bool changed)
        {
            if (_kind.Length < Count)
            {
                _kind = new int[Items.Length];
                _place = new int[Items.Length];
            }
            for (int i = 0; i < Count; i++)
            {
                ref PhoneItem it = ref Items[i];
                _kind[i] = it.Kind == PhoneItemKind.Game ? PhoneLayoutRules.GameItem : it.Kind == PhoneItemKind.Mod ? PhoneLayoutRules.ModItem : PhoneLayoutRules.OtherItem;
                _place[i] = it.Kind != PhoneItemKind.Mod ? PhoneLayoutRules.Home : it.First ? PhoneLayoutRules.Dock : it.Folder >= 0 ? it.Folder : PhoneLayoutRules.Home;
            }
            if (changed)
            {
                Version++;
            }
        }

        /// <summary>The home screen in order: an item's index, or -1 - folder for a folder's tile; returns the length.</summary>
        public int Home(int[] view)
        {
            return PhoneLayoutRules.View(_kind, _place, Count, -1, true, view, _members);
        }

        /// <summary>A folder's apps in order; returns the length.</summary>
        public int InFolder(int folder, int[] view)
        {
            return folder >= 0 && folder < PhoneLayoutRules.FolderCount ? PhoneLayoutRules.View(_kind, _place, Count, folder, true, view, _members) : 0;
        }

        /// <summary>The apps in a folder now.</summary>
        public int Members(int folder)
        {
            PhoneLayoutRules.View(_kind, _place, Count, folder, true, Scratch(), _members);
            return folder >= 0 && folder < _members.Length ? _members[folder] : 0;
        }

        /// <summary>A folder's number: its apps' numbers added up, as a real phone adds them.</summary>
        public int FolderBadge(int folder)
        {
            int sum = 0;
            for (int i = 0; i < Count; i++)
            {
                if (PhoneLayoutRules.FolderOf(_kind[i], _place[i]) == folder && Items[i].Badge > 0)
                {
                    sum = sum > int.MaxValue - Items[i].Badge ? int.MaxValue : sum + Items[i].Badge;
                }
            }
            return sum;
        }

        /// <summary>The home screen's first <paramref name="most"/> entries, the status bar's app row before its All apps tile.</summary>
        public int FirstPage(int most, int[] view)
        {
            int[] all = Scratch();
            int n = Home(all);
            n = Math.Min(n, Math.Min(most, view != null ? view.Length : 0));
            Array.Copy(all, view, n);
            return n;
        }

        /// <summary>An item's place by its button's name; -1 when the phone has no such app.</summary>
        public int IndexOf(string id)
        {
            for (int i = 0; i < Count; i++)
            {
                if (string.Equals(Items[i].Id, id, StringComparison.Ordinal))
                {
                    return i;
                }
            }
            return -1;
        }

        /// <summary>The first apps of a folder, for its tile's two by two of glyphs; None where an app shows its own picture.</summary>
        public void Minis(int folder, UiIcon[] into)
        {
            int[] view = Scratch();
            int n = InFolder(folder, view);
            for (int k = 0; k < into.Length; k++)
            {
                into[k] = k < n ? Items[view[k]].Icon : UiIcon.None;
            }
        }

        // A view's room: as long as the apps and the folders' tiles together.
        private int[] Scratch()
        {
            if (_scratch.Length < Count + PhoneLayoutRules.FolderCount)
            {
                _scratch = new int[(Count + PhoneLayoutRules.FolderCount) * 2];
            }
            return _scratch;
        }
    }
}
