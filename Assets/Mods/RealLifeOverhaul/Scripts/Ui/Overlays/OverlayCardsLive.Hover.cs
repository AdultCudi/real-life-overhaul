using System;
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

        private bool FindHover()
        {
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
            try
            {
                _model.Clear();
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
                _model.Clear();   // another mod's piece threw as it was read (said once, with its mod): the card says nothing this time
                return true;
            }
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
                m.Figure = Plain(Text(GameContract.OverlayPrice, _price));
                m.FigureLabel = UiText.T("reallife_ui_overlay_price");
                m.Tag = m.Tag ?? m.FigureLabel;
                m.Icon = UiIcon.Tag;
            }
            if (Up(_capacity))
            {
                m.AddRow(UiText.T("reallife_ui_overlay_customers"), Plain(Text(GameContract.OverlayCapacity, _capacity)));
            }
            if (Up(_requirements))
            {
                Requirements(m);
            }
            if (Up(_security))
            {
                TMP_Text field = Text(GameContract.OverlaySecurity, _security);
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
            // A vehicle's hold, or anything else the game counts in its own words.
            m.Figure = Plain(Text(GameContract.OverlayStockAmount, _stock));
            string content = OverlayRules.After(Text(GameContract.OverlayStockContent, _stock)?.text, null);
            m.AddRow(UiText.T("reallife_ui_overlay_item"), content);
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
            for (int i = 0; parent != null && i < parent.childCount && m.CheckCount < UiOverlayModel.MostChecks; i++)
            {
                var entry = parent.GetChild(i).GetComponent<RequirementOverlayEntry>();
                if (entry == null || entry == template || !entry.gameObject.activeSelf)
                {
                    continue;
                }
                string words = Plain(Words(GameContract.OverlayRequirementText, entry));
                bool owned = entry.Toggle != null && entry.Toggle.gameObject.activeSelf;
                m.AddCheck(words, null, owned && entry.Toggle.isOn, !owned);
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
