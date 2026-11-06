using System;
using System.Collections.Generic;
using Buildings.BuildingTypes.Shared.BusinessRequirement;
using Helpers;
using Localizor;
using TMPro;
using UI.Smartphone.Apps.BizMan;
using UI.Smartphone.Apps.BizMan.StartBusiness;
using UnityEngine;
using UnityEngine.UI;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// Starting a business: the kinds this building takes as the game made a card for each (a list with a search here), and the
    /// chosen kind's detail read from the game's own card and kind of business. Choosing, naming, Randomise and Start press the
    /// game's own buttons, so its questions (a missing source of goods, a missing diploma, a name already taken) and other
    /// mods' patches run. Cost: the cards are read on a change; the detail once for the kind chosen.
    /// </summary>
    public sealed partial class BizManScreen
    {
        private readonly List<int> _kindRows = new List<int>(32);   // the card each row of the list stands for
        private readonly List<BizManStartRow> _startRows = new List<BizManStartRow>(32);
        private string _kindQuery;                                  // the words typed in the list's search: a row shows when its name holds them
        private int _startFor;
        private int _startDetailOf;
        private int _startChosen = -1;
        private BizManStart _startRead;
        private Sprite _startSprite;

        private bool KindFound(string name) => string.IsNullOrEmpty(_kindQuery) || (name != null && name.IndexOf(_kindQuery, StringComparison.CurrentCultureIgnoreCase) >= 0);

        private void Start(BizManBusiness b, StartBusinessUI start)
        {
            int place = b.address != null ? b.address.GetHashCode() : 0;
            bool again = _m.Part == BizManPart.Start && _startFor == place;   // a refresh of the page shown keeps its place, another building starts at the top
            _startFor = place;
            _m.Part = BizManPart.Start;
            if (!again)
            {
                _kindQuery = null;
                _m.StartOffset = 0f;
                _m.StartReveal = -1;
                _startChosen = -1;
                _startRead = null;
            }
            TMP_InputField name = Get<TMP_InputField>(GameContract.StartName, start);
            _m.NameLabel = UiText.T("common_business_name");
            _m.NameText = name != null ? name.text : null;
            _m.NamePlaceholder = UiText.T("reallife_ui_bizman_name_hint");
            Transform kinds = Get<Transform>(GameContract.StartKinds, start);
            _kindCards.Clear();
            for (int i = 0; kinds != null && i < kinds.childCount; i++)
            {
                if (kinds.GetChild(i).gameObject.activeSelf && kinds.GetChild(i).GetComponent<StartBusinessTypeUI>() != null)
                {
                    _kindCards.Add(kinds.GetChild(i));
                }
            }
            UiDataTableModel old = again ? _m.StartKinds : null;
            _kindRows.Clear();
            var rows = new List<UiDataRow>(_kindCards.Count);
            int selected = -1, chosen = -1;
            for (int i = 0; i < _kindCards.Count; i++)
            {
                StartBusinessTypeUI card = _kindCards[i].GetComponent<StartBusinessTypeUI>();
                string words = Words(Get<Component>(GameContract.KindName, card)) ?? UiText.T(_kindCards[i].name);
                bool isChosen = On(Get<GameObject>(GameContract.KindChosen, card));
                chosen = isChosen ? i : chosen;
                if (!KindFound(words))
                {
                    continue;
                }
                Image icon = Get<Image>(GameContract.KindIcon, card);
                selected = isChosen ? rows.Count : selected;
                _kindRows.Add(i);
                rows.Add(new UiDataRow
                {
                    Cells = new[] { words, TextOf(Get<TMP_Text>(GameContract.KindRivalCount, card)) ?? string.Empty },
                    Values = new[] { (int)BizManRules.KindIcon(_kindCards[i].name, null), 0 },
                    Pictures = new[] { icon != null ? Pic(icon.sprite) : null, null }
                });
            }
            bool searching = !string.IsNullOrEmpty(_kindQuery);
            _m.StartKinds = new UiDataTableModel
            {
                Columns = new[]
                {
                    new UiDataColumn { Title = UiText.T("common_business_type"), Kind = UiCellKind.Tile },
                    new UiDataColumn { Title = UiText.T("reallife_ui_bizman_competitors"), Kind = UiCellKind.Figure, Width = 128f }
                },
                Rows = rows.ToArray(), RowCount = rows.Count, Selected = selected, SearchPlaceholder = UiText.T("reallife_ui_bizman_find_kind"), Query = _kindQuery,
                Count = searching ? UiText.Whole(rows.Count) : null, EmptyIcon = UiIcon.Store, EmptyTitle = UiText.T("reallife_ui_bizman_kinds_none"),
                EmptyAction = searching ? UiText.T("bizman_schedule_clear_filter") : null,
                Offset = old != null ? old.Offset : 0f, Reveal = old != null ? old.Reveal : -1
            };
            if (chosen != _startChosen)
            {
                _startChosen = chosen;
                _m.StartOffset = 0f;
                _m.StartReveal = -1;
            }
            _m.StartDetail = chosen >= 0 ? KindDetail(b, _kindCards[chosen]) : null;
            _m.StartEmptyTitle = UiText.T("reallife_ui_bizman_start_pick");
            _m.StartEmptyBody = UiText.T("reallife_ui_bizman_start_pick_body");
            _m.Facts = _m.More = _m.BuildingFacts = new BizManFact[0];
            BizManPresentation pres = PageOf<BizManPresentation>(b, BizManRules.Presentation);
            _actions.Clear();
            Button random = Calling(start, "RandomizeName");
            if (random != null)
            {
                _actions.Add(Do(BizManPages.DoRandomName, UiText.T("reallife_ui_bizman_randomise"), UiButtonKind.Secondary, UiIcon.Spark, UiAction.Count, !random.interactable));
            }
            Offer(_actions, Calling(pres, "PreviewBuilding"), BizManPages.DoPreview, UiButtonKind.Secondary, UiIcon.Eye, UiAction.Option, "bizman_preview_button");
            Offer(_actions, Calling(pres, "TerminateContract"), BizManPages.DoTerminate, UiButtonKind.Destructive, UiIcon.Trash, UiAction.Count, "bizman_terminate_contract_button");
            Button setUp = Calling(start, "SetUpBusiness");
            if (setUp != null)
            {
                _actions.Add(Do(BizManPages.DoStartHere, UiWidgets.Label(setUp) ?? UiText.T("bizman_start_business_button"), UiButtonKind.Primary, UiIcon.Check, UiAction.Alt,
                    !setUp.interactable));
            }
            _m.Actions = _actions.ToArray();
        }

        // What the game says of the kind chosen, read once for it and again only for another kind or another building: its goods or
        // its staff, where the goods come from, what it needs to open, the diploma and the building.
        private BizManStart KindDetail(BizManBusiness b, Transform cardAt)
        {
            StartBusinessTypeUI card = cardAt.GetComponent<StartBusinessTypeUI>();
            Image icon = Get<Image>(GameContract.KindIcon, card);
            int id = cardAt.GetInstanceID() * 31 + _startFor;
            if (_startRead == null || _startDetailOf != id || _startSprite != (icon != null ? icon.sprite : null))
            {
                _startDetailOf = id;
                _startSprite = icon != null ? icon.sprite : null;
                _startRead = ReadKind(b, cardAt, card);
            }
            _startRead.Picture = Pic(_startSprite);   // the picture keys are made again with each layout
            return _startRead;
        }

        private BizManStart ReadKind(BizManBusiness b, Transform cardAt, StartBusinessTypeUI card)
        {
            var d = new BizManStart
            {
                Kicker = UiText.T("common_business_type"), Name = Words(Get<Component>(GameContract.KindName, card)) ?? UiText.T(cardAt.name),
                Icon = BizManRules.KindIcon(cardAt.name, null), Aspect = Aspect(_startSprite)
            };
            _startRows.Clear();
            string rivals = TextOf(Get<TMP_Text>(GameContract.KindRivalCount, card));
            if (!string.IsNullOrEmpty(rivals))
            {
                _startRows.Add(new BizManStartRow { Kind = BizManStartKind.Fact, Label = UiText.T("reallife_ui_bizman_competitors_here"), Value = rivals });
            }
            BusinessType bt = Get<BusinessType>(GameContract.KindBusiness, card);
            GoodsOrStaff(card, bt);
            Sources(card);
            Needs(b, bt);
            Diploma(bt);
            ThisBuilding(b);
            d.Rows = _startRows.ToArray();
            return d;
        }

        private void GoodsOrStaff(StartBusinessTypeUI card, BusinessType bt)
        {
            if (bt == null)
            {
                return;
            }
            string title = Words(Get<Component>(GameContract.KindGoodsTitle, card));
            bool staff = title != null && title == UiText.T("myemployees_title");
            string[] keys = staff ? bt.employeePrimarySkills : null;
            _words.Clear();
            if (!staff)
            {
                foreach (string item in bt.GetPrimaryProducts())
                {
                    _words.Add(item);
                }
            }
            else if (keys != null)
            {
                _words.AddRange(keys);
            }
            if (_words.Count == 0)
            {
                return;
            }
            _startRows.Add(new BizManStartRow { Kind = BizManStartKind.Heading, Label = title ?? UiText.T("bizman_primary_products") });
            int from = _startRows.Count;
            for (int i = 0; i < _words.Count; i++)
            {
                bool can = staff || CanSource(card, _words[i]);
                _startRows.Add(new BizManStartRow
                {
                    Kind = BizManStartKind.Item, Label = _words[i].GetLocalization(), Mark = can ? UiIcon.None : UiIcon.Warning, Tone = can ? UiRole.None : UiRole.Warn
                });
            }
            _startRows.Sort(from, _startRows.Count - from, KindOrder);
        }

        private static readonly Comparer<BizManStartRow> KindOrder = Comparer<BizManStartRow>.Create((a, c) => string.Compare(a.Label, c.Label, StringComparison.CurrentCultureIgnoreCase));

        // The game's own question whether this kind's card can have the product, with the player's headquarters, factory and the city's wholesalers.
        private static bool CanSource(StartBusinessTypeUI card, string item)
        {
            if (GameContract.KindSourceable.Method == null)
            {
                return true;
            }
            try
            {
                return (bool)GameContract.KindSourceable.Method.Invoke(card, new object[] { item });
            }
            catch (Exception e) when (Guard.Missing(e))
            {
                return true;
            }
        }

        private void Sources(StartBusinessTypeUI card)
        {
            GameObject box = Get<GameObject>(GameContract.KindSourcesBox, card);
            if (box == null || !box.activeSelf)
            {
                return;
            }
            int from = _startRows.Count;
            bool missing = false;
            for (int i = 0; i < box.transform.childCount; i++)
            {
                Transform entry = box.transform.GetChild(i);
                if (!entry.gameObject.activeSelf)
                {
                    continue;   // the game's template, kept out of sight
                }
                string words = TextOf(Get<TMP_Text>(GameContract.KindSourceText, entry.GetComponent<StartBusinessInventorySourceEntry>()));
                bool ok = On(Get<GameObject>(GameContract.KindSourceOk, entry.GetComponent<StartBusinessInventorySourceEntry>()));
                if (string.IsNullOrEmpty(words))
                {
                    continue;
                }
                missing |= !ok;
                _startRows.Add(new BizManStartRow { Kind = BizManStartKind.Item, Label = words, Mark = ok ? UiIcon.Check : UiIcon.Warning, Tone = ok ? UiRole.Ok : UiRole.Warn });
            }
            if (_startRows.Count == from)
            {
                return;
            }
            _startRows.Insert(from, new BizManStartRow { Kind = BizManStartKind.Heading, Label = UiText.T("reallife_ui_bizman_start_sources") });
            if (missing)
            {
                _startRows.Add(new BizManStartRow { Kind = BizManStartKind.Note, Label = UiText.T("reallife_ui_bizman_start_source_missing") });
            }
        }

        private void Needs(BizManBusiness b, BusinessType bt)
        {
            if (bt == null || bt.businessRequirements == null || bt.businessRequirements.Count == 0)
            {
                return;
            }
            int from = _startRows.Count;
            for (int i = 0; i < bt.businessRequirements.Count; i++)
            {
                BusinessRequirement r = bt.businessRequirements[i];
                if (r == null)
                {
                    continue;
                }
                int count = r is SpecificItemsInBuildingBySqm a ? a.GetRequiredItemCount(b.buildingRegistration)
                    : r is ItemsOfTypeInBuildingBySqm c ? c.GetRequiredItemCount(b.buildingRegistration) : 1;
                string words = r.GetLocalizeKey().GetLocalization();
                _startRows.Add(new BizManStartRow { Kind = BizManStartKind.Item, Label = count > 1 ? count.ToString(System.Globalization.CultureInfo.CurrentCulture) + " x " + words : words });
            }
            if (_startRows.Count > from)
            {
                _startRows.Insert(from, new BizManStartRow { Kind = BizManStartKind.Heading, Label = UiText.T("reallife_ui_bizman_start_needs") });
            }
        }

        private void Diploma(BusinessType bt)
        {
            if (bt == null || bt.courseRequired == DiplomaName.Undefined)
            {
                return;
            }
            bool done = EducationHelper.HasCompletedDiploma(bt.courseRequired);
            _startRows.Add(new BizManStartRow { Kind = BizManStartKind.Heading, Label = UiText.T("reallife_ui_bizman_start_diploma") });
            _startRows.Add(new BizManStartRow
            {
                Kind = BizManStartKind.Item, Label = UiText.T(bt.courseRequired.GetLocalizeKey()), Mark = done ? UiIcon.Check : UiIcon.Warning, Tone = done ? UiRole.Ok : UiRole.Warn
            });
            if (!done)
            {
                _startRows.Add(new BizManStartRow { Kind = BizManStartKind.Note, Label = UiText.T("reallife_ui_bizman_start_diploma_missing") });
            }
        }

        // The building's own facts, as its presentation lists them beside the game's start view.
        private void ThisBuilding(BizManBusiness b)
        {
            BizManPresentation pres = PageOf<BizManPresentation>(b, BizManRules.Presentation);
            Transform top = Get<Transform>(GameContract.ShowTop, pres), bottom = Get<Transform>(GameContract.ShowBottom, pres);
            int from = _startRows.Count;
            Row("common_building_type", b.building != null ? UiText.T(b.building.BuildingType) : null);
            Row("bizman_building_total_size", WordsAt(bottom, "TotalSize") ?? (b.building != null ? b.building.totalSqm.ToFormattedArea() : null));
            Row("bizman_traffic_index", WordsAt(top, "TrafficIndex"));
            Row("bizman_customers_capacity", Dash(WordsAt(top, "CustomerCapacity")));
            Row("bizman_market_value", Dash(WordsAt(bottom, "MarketValue")));
            if (_startRows.Count > from)
            {
                _startRows.Insert(from, new BizManStartRow { Kind = BizManStartKind.Heading, Label = UiText.T("reallife_ui_bizman_the_building") });
            }
        }

        private void Row(string labelKey, string value)
        {
            if (!string.IsNullOrEmpty(value))
            {
                _startRows.Add(new BizManStartRow { Kind = BizManStartKind.Fact, Label = UiText.T(labelKey), Value = value });
            }
        }

        // ------------------------------------------------------------------------------------------------- presses

        private bool ChooseKind(int row)
        {
            if (row < 0 || row >= _kindRows.Count)
            {
                return false;
            }
            Transform at = _kindCards[_kindRows[row]];
            StartBusinessTypeUI card = at.GetComponent<StartBusinessTypeUI>();
            if (On(Get<GameObject>(GameContract.KindChosen, card)))
            {
                return true;   // already the kind chosen: nothing for the game to do
            }
            return Click(Get<Button>(GameContract.KindSelect, card), "choose " + at.name);
        }

        private bool SetUpBusiness(BizManBusiness b)
        {
            StartBusinessUI start = Get<StartBusinessUI>(GameContract.ShowStart, PageOf<BizManPresentation>(b, BizManRules.Presentation));
            return Click(Calling(start, "SetUpBusiness"), "set up the business");
        }

        private bool ClearKindSearch()
        {
            _kindQuery = null;
            if (_m.StartKinds != null)
            {
                _m.StartKinds.Offset = 0f;
            }
            return true;
        }

        private void KindTyped(string text)
        {
            _kindQuery = string.IsNullOrEmpty(text) ? null : text;
            if (_m.StartKinds != null)
            {
                _m.StartKinds.Offset = 0f;
            }
        }

        private bool StartWheel(int clip, float by)
        {
            if (_m.Page != BizManPage.Business || _m.Part != BizManPart.Start)
            {
                return false;
            }
            if (clip == _m.StartClip && clip >= 0)
            {
                _m.StartOffset = Math.Max(0f, _m.StartOffset + by);
            }
            else if (_m.StartKinds != null)
            {
                _m.StartKinds.Offset = Math.Max(0f, _m.StartKinds.Offset + by);
            }
            return true;
        }

        private bool StartReveal(int list, int index)
        {
            if (_m.Page != BizManPage.Business || _m.Part != BizManPart.Start)
            {
                return false;
            }
            if (list == _m.StartList && list >= 0)
            {
                _m.StartReveal = index;
            }
            else if (_m.StartKinds != null)
            {
                _m.StartKinds.Reveal = index;
            }
            return true;
        }
    }
}
