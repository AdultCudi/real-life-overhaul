using System.Collections.Generic;
using RealLifeOverhaul.GameUi;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// The one file that fills PhoneItems from the game: every button of the HUD phone's grid in the classic pager's order
    /// (the game's, the mod's, other mods'), each one's words and badge as its button shows them after the game's and
    /// other mods' code ran, another mod's own picture, and each mod app's folder or place first on the home screen. It
    /// presses a button through its own onClick, so the game's OpenApp, PhoneApp.Open and other mods' handlers run as a
    /// click would. Read when the pager lays the grid out; looked at a few times a second by the kit phone for badges and
    /// words another mod changes without telling (a signature of references, nothing allocated). Cost: a read on a
    /// change, a few field reads a look.
    /// </summary>
    internal static class PhoneItemsGame
    {
        internal static readonly PhoneItems Items = new PhoneItems();

        private static readonly List<Transform> Buttons = new List<Transform>(48);
        private static readonly List<int> Kinds = new List<int>(48);
        private static readonly List<int> Places = new List<int>(48);
        private static readonly List<GameObject> BadgeObjects = new List<GameObject>(48);
        private static readonly List<TMP_Text> BadgeLabels = new List<TMP_Text>(48);
        private static readonly List<TMP_Text> TitleLabels = new List<TMP_Text>(48);
        private static readonly List<Image> PictureImages = new List<Image>(48);
        private static readonly List<string> SeenBadge = new List<string>(48);
        private static readonly List<bool> SeenBadgeOn = new List<bool>(48);
        private static readonly List<string> SeenTitle = new List<string>(48);
        private static readonly List<Sprite> SeenPicture = new List<Sprite>(48);
        private static readonly List<Sprite> Pictures = new List<Sprite>(8);   // other mods' pictures by handle less one
        private static int _passesSeen = -1;
        private static bool _foldersSeen;

        /// <summary>Another mod's picture by its handle, for UiPictures.</summary>
        internal static Sprite Picture(int handle)
        {
            return handle > 0 && handle <= Pictures.Count ? Pictures[handle - 1] : null;
        }

        /// <summary>The phone's apps read afresh from the pager; true when anything differs from the last reading.</summary>
        internal static bool Read(PhoneGridPager pager)
        {
            _passesSeen = PhoneGridPager.Passes;
            _foldersSeen = PhoneLayoutPrefs.On;
            pager.Arranged(Buttons, Kinds, Places);
            bool changed = Items.Count != Buttons.Count;
            PhoneItem[] before = changed ? null : Items.Items;
            int count = Buttons.Count;
            Grow(count);
            var fresh = new PhoneItem[count];
            for (int i = 0; i < count; i++)
            {
                Transform button = Buttons[i];
                BadgeObjects[i] = BadgeObject(button);
                BadgeLabels[i] = BadgeObjects[i] != null ? BadgeObjects[i].GetComponentInChildren<TMP_Text>(true) : null;
                TitleLabels[i] = TitleLabel(button);
                PictureImages[i] = PhoneGridPager.Picture(button);
                ref PhoneItem it = ref fresh[i];
                it.Id = button.name;
                it.Kind = Kinds[i] == PhoneLayoutRules.GameItem ? PhoneItemKind.Game : Kinds[i] == PhoneLayoutRules.ModItem ? PhoneItemKind.Mod : PhoneItemKind.Other;
                it.Folder = -1;
                if (it.Kind == PhoneItemKind.Mod)
                {
                    it.First = Places[i] == PhoneLayoutRules.Dock;
                    it.Folder = PhoneLayoutRules.FolderOf(Kinds[i], Places[i]);
                }
                if (it.Kind == PhoneItemKind.Other || !PhoneLooks.Of(it.Id, out it.Icon, out it.Tint))
                {
                    it.Icon = UiIcon.None;
                    it.Tint = UiRole.None;
                    it.Picture = Handle(PictureImages[i] != null ? PictureImages[i].sprite : null);
                }
                Look(i, ref it);
                changed |= before == null || !Same(ref before[i], ref it);
            }
            Items.Begin();
            for (int i = 0; i < count; i++)
            {
                Items.Add() = fresh[i];
            }
            Items.End(changed);
            return changed;
        }

        /// <summary>
        /// A look at what changes without the pager hearing: a badge another mod shows, a title, a picture; a new layout
        /// of the grid reads all again. True when the phone's apps changed.
        /// </summary>
        internal static bool Poll(PhoneGridPager pager)
        {
            if (pager == null)
            {
                return false;
            }
            if (PhoneGridPager.Passes != _passesSeen || PhoneLayoutPrefs.On != _foldersSeen)
            {
                return Read(pager);
            }
            bool changed = false;
            for (int i = 0; i < Items.Count && i < Buttons.Count; i++)
            {
                if (Buttons[i] == null)
                {
                    return Read(pager);
                }
                bool on = BadgeObjects[i] != null && BadgeObjects[i].activeSelf;
                string badge = BadgeLabels[i] != null ? BadgeLabels[i].text : null;
                string title = TitleLabels[i] != null ? TitleLabels[i].text : null;
                Sprite picture = PictureImages[i] != null ? PictureImages[i].sprite : null;
                if (on == SeenBadgeOn[i] && ReferenceEquals(badge, SeenBadge[i]) && ReferenceEquals(title, SeenTitle[i]) && picture == SeenPicture[i])
                {
                    continue;
                }
                ref PhoneItem it = ref Items.Items[i];
                if (it.Kind == PhoneItemKind.Other)
                {
                    it.Picture = Handle(picture);
                }
                Look(i, ref it);
                changed = true;
            }
            if (changed)
            {
                Items.End(true);
            }
            return changed;
        }

        /// <summary>Presses the app's own button on the HUD phone, as a click would.</summary>
        internal static bool Press(int index)
        {
            if (index < 0 || index >= Buttons.Count || Buttons[index] == null)
            {
                return false;
            }
            Button button = Buttons[index].GetComponent<Button>();
            if (button == null || !button.interactable)
            {
                return false;
            }
            UiWidgets.Hand(button);   // the app's opening is the game's own work, and the kit's screen it shows is counted as its own
            return true;
        }

        /// <summary>The city's interface went: nothing of it is held.</summary>
        internal static void Forget()
        {
            Buttons.Clear();
            Pictures.Clear();
            Items.Begin();
            Items.End(true);
            _passesSeen = -1;
        }

        // The words, badge and picture as the button shows them now, and what was seen, for the next look.
        private static void Look(int i, ref PhoneItem it)
        {
            Transform button = Buttons[i];
            bool on = BadgeObjects[i] != null && BadgeObjects[i].activeSelf;
            string badge = BadgeLabels[i] != null ? BadgeLabels[i].text : null;
            string title = TitleLabels[i] != null ? TitleLabels[i].text : null;
            it.Badge = on ? PhoneGridPager.BadgeCount(button) : 0;
            it.Title = !string.IsNullOrEmpty(title) ? title.Trim() : it.Kind == PhoneItemKind.Other ? UiText.T("reallife_phone_other_app") : button.name;
            SeenBadgeOn[i] = on;
            SeenBadge[i] = badge;
            SeenTitle[i] = title;
            SeenPicture[i] = PictureImages[i] != null ? PictureImages[i].sprite : null;
        }

        private static bool Same(ref PhoneItem a, ref PhoneItem b)
        {
            return a.Id == b.Id && a.Title == b.Title && a.Badge == b.Badge && a.Picture == b.Picture && a.Folder == b.Folder && a.First == b.First && a.Icon == b.Icon && a.Kind == b.Kind;
        }

        // A handle for a picture, the same for the same picture; 0 for none.
        private static int Handle(Sprite sprite)
        {
            if (sprite == null)
            {
                return 0;
            }
            int at = Pictures.IndexOf(sprite);
            if (at < 0)
            {
                Pictures.Add(sprite);
                at = Pictures.Count - 1;
            }
            return at + 1;
        }

        // The button's badge: the game's Badge where it has one (Going Public keeps the copy's), else a child of that name.
        private static GameObject BadgeObject(Transform button)
        {
            var badge = button.GetComponentInChildren<global::UI.Elements.Badge>(true);
            if (badge != null)
            {
                return badge.gameObject;
            }
            Transform named = button.Find("Badge");
            return named != null ? named.gameObject : null;
        }

        // The words under the button: its Title, else its first words that are not the badge's.
        private static TMP_Text TitleLabel(Transform button)
        {
            Transform named = button.Find("Title");
            TMP_Text title = named != null ? named.GetComponent<TMP_Text>() : null;
            if (title != null)
            {
                return title;
            }
            TMP_Text[] all = button.GetComponentsInChildren<TMP_Text>(true);
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i].GetComponentInParent<global::UI.Elements.Badge>(true) == null && all[i].transform.parent != null && all[i].transform.parent.name != "Badge")
                {
                    return all[i];
                }
            }
            return null;
        }

        private static void Grow(int count)
        {
            while (BadgeObjects.Count < count)
            {
                BadgeObjects.Add(null);
                BadgeLabels.Add(null);
                TitleLabels.Add(null);
                PictureImages.Add(null);
                SeenBadge.Add(null);
                SeenBadgeOn.Add(false);
                SeenTitle.Add(null);
                SeenPicture.Add(null);
            }
        }
    }
}
