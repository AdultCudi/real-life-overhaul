using System;
using System.Collections.Generic;
using System.Reflection;
using Entities;
using Items.SpecialItems;
using Localizor.LanguageChangeEvent;
using Player.HUD.ItemInfoOverlays;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// The small card's read: each of the game's eight hover kinds that stands on in the box, by the words it wrote into its own
    /// fields (after the game and other mods wrote them), joined to what the game knows beyond them (a stock against its room, the
    /// price the player set, a person's shift, a recipe's rate). Called when the box shows another thing and four times a second
    /// after; the strings it makes are made only then.
    /// </summary>
    internal sealed partial class OverlayCardsLive
    {
        private const int MachineRecipeFlag = 0x80;   // SimpleOverlayType.MachineRecipe: a flag the game has no class for

        private FieldInfo _headerOfSimple, _headerOfDetailed;
        private StockOverlay _stock;
        private PriceOverlay _price;
        private CtaOverlay _cta;
        private RequirementsOverlay _requirements;
        private SecurityOverlay _security;
        private InfoOverlay _info;
        private CustomerCapacitySimpleOverlay _capacity;
        private EmployeeNameOverlay _name;
        private readonly List<IOverlay> _hoverParts = new List<IOverlay>(12);
        private static HashSet<string> _known;
        private int _skipId;       // the thing whose box is the game's own for now: its read threw, or it holds a piece the card has no reader for
        private Form _skipForm;

        private bool FindHover()
        {
            _hoverParts.Clear();
            _simple.GetComponentsInChildren(true, _hoverParts);
            _stock = _simple.GetComponentInChildren<StockOverlay>(true);
            _price = _simple.GetComponentInChildren<PriceOverlay>(true);
            _cta = _simple.GetComponentInChildren<CtaOverlay>(true);
            _requirements = _simple.GetComponentInChildren<RequirementsOverlay>(true);
            _security = _simple.GetComponentInChildren<SecurityOverlay>(true);
            _info = _simple.GetComponentInChildren<InfoOverlay>(true);
            _capacity = _simple.GetComponentInChildren<CustomerCapacitySimpleOverlay>(true);
            _name = _simple.GetComponentInChildren<EmployeeNameOverlay>(true);
            const BindingFlags all = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            _headerOfSimple = typeof(SimpleOverlay).BaseType?.GetField("headerField", all);
            _headerOfDetailed = typeof(DetailedOverlay).BaseType?.GetField("headerField", all);
            return _headerOfSimple != null && _headerOfDetailed != null && GameContract.OverlayBoxHeader.Field != null && (_stock != null || _price != null || _cta != null);
        }

        // One box read into the card: the small one, or the large one's kinds.
        private bool Read(EntityController entity, bool renaming)
        {
            _model.Clear();
            if (Unknown(_form == Form.Hover ? _hoverParts : _parts))
            {
                Skip(entity);   // another mod's piece: the card cannot say it, so the game's own box shows it
                return false;
            }
            try
            {
                if (_form == Form.Hover)
                {
                    ReadHover(entity);
                }
                else
                {
                    ReadClick(entity, renaming);
                }
                Lay();
                return true;
            }
            catch (Exception e) when (UiForeignGame.Spoilt(e, entity))
            {
                _model.Clear();   // another mod's piece threw as it was read (said once, with its mod): the game's box shows until the pointer leaves the thing
                Skip(entity);
                return false;
            }
        }

        // The box of this thing is left to the game until the pointer has been off every box: a read that throws or a piece
        // that cannot be drawn would otherwise come back, and be read and thrown again, every frame.
        private void Skip(EntityController entity)
        {
            _skipId = entity.GetInstanceID();
            _skipForm = _form;
        }

        // True when a piece of the box is on that is none of the kinds the card reads (another mod's own overlay).
        private static bool Unknown(List<IOverlay> parts)
        {
            if (_known == null)
            {
                _known = new HashSet<string> { "BuildingEntranceOverlay", "ElevatorOverlay", "GasStationOverlay" };   // the game's prompts over the world, never inside these boxes
                for (int i = 0; i < OverlayKinds.All.Length; i++)
                {
                    string name = OverlayKinds.All[i].Name;
                    int space = name.IndexOf(' ');
                    _known.Add(space > 0 ? name.Substring(0, space) : name);
                }
            }
            for (int i = 0; i < parts.Count; i++)
            {
                IOverlay part = parts[i];
                if (part == null || !part.gameObject.activeSelf)
                {
                    continue;
                }
                bool known = false;
                for (Type t = part.GetType(); t != null && t != typeof(IOverlay) && !known; t = t.BaseType)
                {
                    known = _known.Contains(t.Name);
                }
                if (!known)
                {
                    return true;
                }
            }
            return false;
        }

        // ------------------------------------------------------------------------------------------------- the small card

        private void ReadHover(EntityController entity)
        {
            UiOverlayModel m = _model;
            m.Title = Plain(_headerOfSimple.GetValue(_simple) as TMP_Text);
            m.Icon = IconOf(entity);
            bool owned = InstanceBehavior<BuildingManager>.Instance != null && InstanceBehavior<BuildingManager>.Instance.IsPlayerOwnedBusiness;
            if (Up(_name))
            {
                Person(entity, m);
            }
            if (Up(_stock))
            {
                Stock(entity, m);
            }
            if (Up(_price))
            {
                m.Figure = Plain(Shown(Text(GameContract.OverlayPrice, _price)));
                m.FigureLabel = UiText.T("reallife_ui_overlay_price");
                m.Tag = m.Tag ?? m.FigureLabel;
                m.Icon = UiIcon.Tag;
            }
            if (Up(_capacity))
            {
                m.AddRow(UiText.T("reallife_ui_overlay_customers"), Plain(Shown(Text(GameContract.OverlayCapacity, _capacity))));
            }
            if (Up(_requirements))
            {
                Requirements(m);
            }
            if (Up(_security))
            {
                TMP_Text field = Shown(Text(GameContract.OverlaySecurity, _security));
                m.AddRow(UiText.T("reallife_ui_overlay_security"), Plain(field), field != null ? WorldHost.Tone(field.color) : UiRole.None);
            }
            if (Up(_info))
            {
                m.Note = Plain(Words(GameContract.OverlayInfo, _info));
            }
            if (owned && (entity.simpleOverlayType & (SimpleOverlayType)MachineRecipeFlag) != 0)
            {
                Machine(entity, m);
            }
            Table(entity, m);
            if (Up(_cta))
            {
                string click = Plain(Words(GameContract.OverlayCta, _cta));
                if (!string.IsNullOrEmpty(click))
                {
                    m.AddAction(new UiOverlayAction { Label = click, Hint = true });
                }
            }
        }

        // The stock: how full against its room as a figure and a meter in the game's own colours, what it holds, the price the
        // player set with its margin, and when the next delivery of it comes.
        private void Stock(EntityController entity, UiOverlayModel m)
        {
            m.Tag = UiText.T("reallife_ui_overlay_stock");
            if (OverlayFactsLive.Stock(entity, out int amount, out int room, out string item, out bool storage))
            {
                int share = OverlayRules.StockShare(amount, room);
                m.Figure = UiText.Fill("reallife_ui_overlay_of", "amount", amount.ToString(), "room", room.ToString());
                m.Meter = Math.Max(0, share);
                m.MeterTone = OverlayRules.StockTone(share);
                if (item != null)
                {
                    m.AddRow(UiText.T("reallife_ui_overlay_item"), Plain(LocalizationHelperItem(item)));
                    if (OverlayFactsLive.Price(entity, item, out float price, out float cost))
                    {
                        int margin = OverlayRules.Margin(price, cost);
                        m.AddRow(UiText.T("reallife_ui_overlay_price"), UiText.Money(price));
                        if (margin >= 0)
                        {
                            m.AddRow(UiText.T("reallife_ui_overlay_margin"), UiText.Fill("reallife_ui_overlay_margin_of", "margin", margin.ToString(), "cost", UiText.Money(cost)),
                                OverlayRules.MarginTone(margin));
                        }
                    }
                    if (OverlayFactsLive.NextDelivery(item, out int day))
                    {
                        m.AddRow(UiText.T("reallife_ui_overlay_delivery"), OverlayFactsLive.DayWords(day));
                    }
                }
                return;
            }
            // A vehicle's hold, or anything else the game counts in its own words. The game switches a field off where it has nothing to
            // say (a vehicle has no contents, an empty shelf no amount) and leaves the words the last shelf wrote, so only a field that is on is read.
            m.Figure = Plain(Shown(Text(GameContract.OverlayStockAmount, _stock)));
            m.AddRow(UiText.T("reallife_ui_overlay_item"), OverlayRules.After(Shown(Text(GameContract.OverlayStockContent, _stock))?.text, null));
        }

        // A person at a station: their face with their mood's ring, who and until when, and their mood.
        private void Person(EntityController entity, UiOverlayModel m)
        {
            string name = Plain(Text(GameContract.OverlayName, _name));
            if (string.IsNullOrEmpty(name))
            {
                return;
            }
            EmployeeInstance person = OverlayFactsLive.Employee(entity);
            m.Face = name;
            m.Hue = HueOf(name);
            m.Tag = m.Title;
            m.Title = name;
            if (person == null)
            {
                return;
            }
            OverlayMood mood = OverlayRules.Mood(person.satisfaction);
            m.Ring = OverlayRules.MoodTone(mood);
            if (OverlayFactsLive.Shift(person, out int from, out int until, out bool on))
            {
                if (on)
                {
                    m.Figure = UiText.Fill("reallife_ui_overlay_until", "time", until.GetFormattedTime());
                    m.Meter = OverlayRules.ShiftShare(from, until, TimeHelper.CurrentHour, (int)TimeHelper.CurrentMinute);
                    m.MeterTone = UiRole.Accent;
                }
                else
                {
                    m.Figure = UiText.T("reallife_ui_overlay_off_shift");
                }
            }
            else
            {
                m.Figure = UiText.T("reallife_ui_overlay_no_shift");
            }
            m.AddRow(UiText.T("reallife_ui_overlay_mood"), MoodWords(mood, person.satisfaction),
                mood == OverlayMood.Low ? UiRole.DangerText : mood == OverlayMood.Middle ? UiRole.WarnText : UiRole.OkText);
        }

        // The game's own requirement lines: its words, a tick once met; a line that only says what an item needs is a dot.
        private void Requirements(UiOverlayModel m)
        {
            Transform list = _requirements.transform;
            var template = GameContract.OverlayRequirementTemplate.Field?.GetValue(_requirements) as RequirementOverlayEntry;
            Transform parent = template != null ? template.transform.parent : list;
            int total = 0;
            for (int i = 0; parent != null && i < parent.childCount; i++)
            {
                RequirementOverlayEntry entry = parent.GetChild(i).GetComponent<RequirementOverlayEntry>();
                total += entry != null && entry != template && entry.gameObject.activeSelf ? 1 : 0;
            }
            // A list longer than the card holds ends in a line that says how many are left, so none goes unsaid.
            int room = total > UiOverlayModel.MostChecks ? UiOverlayModel.MostChecks - 1 : UiOverlayModel.MostChecks, shown = 0;
            for (int i = 0; parent != null && i < parent.childCount && shown < room; i++)
            {
                var entry = parent.GetChild(i).GetComponent<RequirementOverlayEntry>();
                if (entry == null || entry == template || !entry.gameObject.activeSelf)
                {
                    continue;
                }
                string words = Plain(Words(GameContract.OverlayRequirementText, entry));
                bool owned = entry.Toggle != null && entry.Toggle.gameObject.activeSelf;
                m.AddCheck(words, null, owned && entry.Toggle.isOn, !owned);
                shown++;
            }
            if (total > shown)
            {
                m.AddCheck(UiText.Fill("reallife_ui_overlay_more", "count", (total - shown).ToString()), null, false, true);
            }
            if (m.Tag == null)
            {
                m.Tag = UiText.T("reallife_ui_overlay_needs");
            }
            m.Icon = UiIcon.ListChecks;
        }

        // A recipe machine: what it makes with its rate, what it needs, and how much of it the pallets hold.
        private void Machine(EntityController entity, UiOverlayModel m)
        {
            if (!OverlayFactsLive.Machine(entity, out string makes, out string needs, out int perHour, out int onPallets))
            {
                return;
            }
            m.Tag = UiText.T("reallife_ui_overlay_recipe");
            m.Icon = UiIcon.Factory;
            m.Figure = makes;
            m.FigureLabel = perHour > 0 ? UiText.Fill("reallife_ui_overlay_per_hour", "amount", perHour.ToString()) : UiText.T("reallife_ui_overlay_nobody");
            m.AddRow(UiText.T("reallife_ui_overlay_needs"), needs);
            m.AddRow(UiText.T("reallife_ui_overlay_pallets"), onPallets.ToString());
        }

        // A table: the seats taken over its seats with a meter, and the line at a till.
        private void Table(EntityController entity, UiOverlayModel m)
        {
            if (string.IsNullOrEmpty(m.Figure) && OverlayFactsLive.Seats(entity, out int taken, out int seats))
            {
                m.Tag = UiText.T("reallife_ui_overlay_seats");
                m.Icon = UiIcon.People;
                m.Figure = UiText.Fill("reallife_ui_overlay_of", "amount", taken.ToString(), "room", seats.ToString());
                m.FigureLabel = UiText.T("reallife_ui_overlay_taken");
                m.Meter = Math.Max(0, OverlayRules.SeatShare(taken, seats));
                m.MeterTone = UiRole.Accent;
            }
            int queue = OverlayFactsLive.Queue(entity);
            if (queue > 0)
            {
                m.AddRow(UiText.T("reallife_ui_overlay_queue"), queue.ToString());
            }
        }

        // ------------------------------------------------------------------------------------------------- words and glyphs

        private static bool Up(Component c) => c != null && c.gameObject.activeSelf;

        // A label the game has switched on: one that is off holds the words of the last thing it was written for.
        private static TMP_Text Shown(TMP_Text t) => t != null && t.gameObject.activeInHierarchy ? t : null;

        private static TMP_Text Text(GameMember field, object on) => field.Field != null && on != null ? field.Field.GetValue(on) as TMP_Text : null;

        private static string Words(GameMember field, object on)
        {
            var label = field.Field != null && on != null ? field.Field.GetValue(on) as TextLocalizationComponent : null;
            return label != null && label.TextContainer != null ? label.TextContainer.text : null;
        }

        private static string Plain(TMP_Text t) => t != null ? OverlayRules.Plain(t.text) : null;

        private static string Plain(string words) => OverlayRules.Plain(words);

        private static string LocalizationHelperItem(string item) => Helpers.LocalizationHelper.GetItemLabel(item).ToString();

        private static string MoodWords(OverlayMood mood, float satisfaction) =>
            UiText.T(mood == OverlayMood.Low ? "reallife_ui_overlay_mood_low" : mood == OverlayMood.Middle ? "reallife_ui_overlay_mood_middle" : "reallife_ui_overlay_mood_high")
            + ", " + OverlayRules.Percent(satisfaction) + "%";

        // A person's hue by their name: the same every time, as the kit's own faces are.
        private static float HueOf(string name)
        {
            int h = 17;
            for (int i = 0; i < name.Length; i++)
            {
                h = unchecked(h * 31 + name[i]);
            }
            return (h & 0x7fffffff) % 360 / 360f;
        }

        // The glyph for what a box stands over, by its own kind.
        private UiIcon IconOf(EntityController entity)
        {
            if (entity is VehicleController)
            {
                return UiIcon.Car;
            }
            if (entity is FridgeController)
            {
                return UiIcon.ForkKnife;
            }
            if (entity is FactoryAssemblyMachineController)
            {
                return UiIcon.Factory;
            }
            if (entity is EmployeeStationController)
            {
                return UiIcon.Person;
            }
            if (entity is LoudspeakerController)
            {
                return UiIcon.MusicNotes;
            }
            if (entity is JobBoardController)
            {
                return UiIcon.Briefcase;
            }
            if (entity is SellerStandController)
            {
                return UiIcon.ShoppingCart;
            }
            return UiIcon.Package;
        }
    }
}
