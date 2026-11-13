using System;
using System.Collections.Generic;
using Buildings.BuildingTypes.Shared.BusinessRequirement;
using Buildings.Retail.Businesses.CinemaTheater;
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
        private readonly List<string> _startHelp = new List<string>(8);   // the help pages the chosen kind's rows open, each by its place plus one in a row's Arg
        private bool _focusName;                                          // the name box takes the keys as the page opens, as the game's own is focused

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
                _typeCommand = BizManPages.Type;   // the game focuses its name box as the view opens, and so does the page's
                _typeArg = BizManPages.FieldName;
                _focusName = true;
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
            // The game hides its competitors column where no kind of the building is a shop (an office, a warehouse): so does the list.
            bool rivals = false;
            for (int i = 0; i < _kindCards.Count && !rivals; i++)
            {
                rivals = TextOf(Get<TMP_Text>(GameContract.KindRivalCount, _kindCards[i].GetComponent<StartBusinessTypeUI>())) != null;
            }
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
                    Cells = rivals ? new[] { words, TextOf(Get<TMP_Text>(GameContract.KindRivalCount, card)) ?? string.Empty } : new[] { words },
                    Values = rivals ? new[] { (int)BizManRules.KindIcon(_kindCards[i].name, null), 0 } : new[] { (int)BizManRules.KindIcon(_kindCards[i].name, null) },
                    Pictures = rivals ? new[] { icon != null ? Pic(icon.sprite) : null, null } : new[] { icon != null ? Pic(icon.sprite) : null }
                });
            }
            bool searching = !string.IsNullOrEmpty(_kindQuery);
            _m.StartKinds = new UiDataTableModel
            {
                Columns = rivals
                    ? new[]
                    {
                        new UiDataColumn { Title = UiText.T("common_business_type"), Kind = UiCellKind.Tile },
                        new UiDataColumn { Title = UiText.T("reallife_ui_bizman_competitors"), Kind = UiCellKind.Figure, Width = 128f }
                    }
                    : new[] { new UiDataColumn { Title = UiText.T("common_business_type"), Kind = UiCellKind.Tile } },
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
            ReadSide(b, pres);
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
                // The game writes the name into the place as it starts the business and the page stays on its start view until the logo
                // is made: a second press then would only be told the name is taken.
                bool starting = b.buildingRegistration != null && !string.IsNullOrEmpty(b.buildingRegistration.BusinessName);
                _actions.Add(Do(BizManPages.DoStartHere, starting ? UiText.T("reallife_ui_bizman_starting") : UiWidgets.Label(setUp) ?? UiText.T("bizman_start_business_button"),
                    UiButtonKind.Primary, UiIcon.Check, UiAction.Alt, starting || !setUp.interactable));
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
            _startHelp.Clear();
            BusinessType bt = Get<BusinessType>(GameContract.KindBusiness, card);
            About(bt);
            string rivals = TextOf(Get<TMP_Text>(GameContract.KindRivalCount, card));
            if (!string.IsNullOrEmpty(rivals))
            {
                _startRows.Add(new BizManStartRow { Kind = BizManStartKind.Fact, Label = UiText.T("reallife_ui_bizman_competitors_here"), Value = rivals });
            }
            GoodsOrStaff(card, bt);
            Sources(card);
            Needs(b, bt);
            Diploma(bt);
            Costs(bt);
            d.Rows = _startRows.ToArray();
            return d;
        }

        // What the game's own help page says the kind is, as far as its first list: the paragraphs, and a way to the whole page.
        private void About(BusinessType bt)
        {
            string key = bt != null ? BizManStartRules.HelpKey(bt.businessTypeName) : null;
            string text = key != null ? key.GetLocalization() : null;
            string[] paragraphs = text != null && text != key ? BizManStartRules.Describe(text, 3) : new string[0];
            if (paragraphs.Length == 0)
            {
                return;
            }
            _startRows.Add(new BizManStartRow { Kind = BizManStartKind.Heading, Label = UiText.T("reallife_ui_bizman_start_about") });
            for (int i = 0; i < paragraphs.Length; i++)
            {
                _startRows.Add(new BizManStartRow { Kind = BizManStartKind.Body, Label = paragraphs[i] });
            }
            _startHelp.Add(BizManStartRules.HelpSlug(bt.businessTypeName));
            _startRows.Add(new BizManStartRow { Kind = BizManStartKind.Link, Label = UiText.T("reallife_ui_bizman_start_guide"), Arg = _startHelp.Count });
        }

        // What the kind charges on top of the lease, in the game's own figures: a cinema's fee for each screen and a theatre's for its stage
        // on each day they show, and the entrance a kind takes.
        private void Costs(BusinessType bt)
        {
            if (bt == null)
            {
                return;
            }
            int from = _startRows.Count;
            bool cinema = bt.businessTypeName == "ba:businesstype_cinema", theatre = bt.businessTypeName == "ba:businesstype_theater";
            if (cinema || theatre)
            {
                float fee = cinema ? CinemaTheaterHelper.CinemaScreenLicensingFee : CinemaTheaterHelper.TheaterStageLicensingFee;
                _startRows.Add(new BizManStartRow
                {
                    Kind = BizManStartKind.Fact, Label = UiText.T("stage_with_licensing_fee_enabled_sublabel").TrimEnd(':', ' '),
                    Value = UiText.Fill(cinema ? "reallife_ui_bizman_start_fee_screen" : "reallife_ui_bizman_start_fee_stage", "amount", UiText.Money(fee))
                });
            }
            if (bt.hasEntranceFee)
            {
                _startRows.Add(new BizManStartRow { Kind = BizManStartKind.Item, Label = UiText.T("ba:transaction_entrancefee_label") });
            }
            if (_startRows.Count > from)
            {
                _startRows.Insert(from, new BizManStartRow { Kind = BizManStartKind.Heading, Label = UiText.T("reallife_ui_bizman_start_costs") });
            }
        }

        private void GoodsOrStaff(StartBusinessTypeUI card, BusinessType bt)
        {
            if (bt == null)
            {
                return;
            }
            // The game's rule, not its label's words: an office, a warehouse or a headquarters lists who it hires, whatever the label says.
            bool staff = BizManStartRules.ShowsStaff(bt.IsHeadquarters, bt.suitableBuildingType);
            string title = Words(Get<Component>(GameContract.KindGoodsTitle, card)) ?? UiText.T(staff ? "myemployees_title" : "bizman_primary_products");
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
            _startRows.Add(new BizManStartRow { Kind = BizManStartKind.Heading, Label = title });
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
                string help = r.GetHelpLink(b.buildingRegistration);   // the game puts a "?" beside a requirement that has a help page
                int page = 0;
                if (!string.IsNullOrEmpty(help))
                {
                    _startHelp.Add(help.ToLowerInvariant());
                    page = _startHelp.Count;
                }
                string label = count > 1 ? count.ToString(System.Globalization.CultureInfo.CurrentCulture) + " x " + words : words;
                _startRows.Add(new BizManStartRow { Kind = BizManStartKind.Item, Label = label, Arg = page });
            }
            if (_startRows.Count > from)
            {
                _startRows.Insert(from, new BizManStartRow { Kind = BizManStartKind.Heading, Label = UiText.T("reallife_ui_bizman_start_needs") });
                // What it needs to open reads as what it needs to be started, and a player with an empty place then waits to furnish
                // it first: the game starts a business with a name and a kind alone, and the list is for afterwards.
                _startRows.Add(new BizManStartRow { Kind = BizManStartKind.Note, Label = UiText.T("reallife_ui_bizman_start_note") });
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

        // The building the business would stand in, as the game's left side lists it beside its start view: the photo, the facts with the
        // size class, and what the lease costs. The kind chosen does not change any of it, so it is read whether or not a kind is chosen.
        private void ReadSide(BizManBusiness b, BizManPresentation pres)
        {
            Image photo = Get<Image>(GameContract.ShowPhoto, pres);
            _m.StartPhoto = photo != null ? Pic(photo.sprite) : null;
            _m.StartPhotoAspect = photo != null && photo.sprite != null ? Aspect(photo.sprite) : 1.6f;
            _startSide.Clear();
            Transform top = Get<Transform>(GameContract.ShowTop, pres), bottom = Get<Transform>(GameContract.ShowBottom, pres);
            Row("common_building_type", b.building != null ? UiText.T(b.building.BuildingType) : null);
            Row("common_neighborhood", WordsAt(top, "Neighborhood"));
            // The game writes two figures, the whole size and the unit's area with its size class: one row says both while they are the same square metres.
            string size = WordsAt(bottom, "TotalSize") ?? (b.building != null ? b.building.totalSqm.ToFormattedArea() : null), area = WordsAt(bottom, "BuildingArea");
            bool same = b.building != null && b.building.totalSqm == BuildingHelper.GetBuildingSquareMeters(b.building.Address);
            Row("bizman_building_total_size", same ? area ?? size : size);
            Row("reallife_ui_world_area", same ? null : area);
            Row("bizman_traffic_index", WordsAt(top, "TrafficIndex"));
            Row("bizman_customers_capacity", Dash(WordsAt(top, "CustomerCapacity")));
            Row("bizman_market_value", Dash(WordsAt(bottom, "MarketValue")));
            if (_startSide.Count > 0)
            {
                _startSide.Insert(0, new BizManStartRow { Kind = BizManStartKind.Heading, Label = UiText.T("reallife_ui_bizman_the_building") });
            }
            BuildingRegistration reg = b.buildingRegistration;
            int lease = _startSide.Count;
            if (reg != null && reg.RentedByPlayer)
            {
                // What the game took when it was rented: its rent for each day and its deposit, the figures of the rent view it showed before.
                BizManFact rent = Split(UiText.Fill("bizman_presentation_daily_rent", "dailyRent", UiText.Money(reg.RentPerDay)));
                if (reg.RentPerDay > 0f && !string.IsNullOrEmpty(rent.Value))
                {
                    _startSide.Add(new BizManStartRow { Kind = BizManStartKind.Fact, Label = rent.Label ?? UiText.T("reallife_ui_bizman_rent_day"), Value = rent.Value });
                }
                if (reg.lastDeposit > 0f)
                {
                    Row("ba:transaction_deposit_label", UiText.Money(reg.lastDeposit));
                }
            }
            if (_startSide.Count > lease)
            {
                _startSide.Insert(lease, new BizManStartRow { Kind = BizManStartKind.Heading, Label = UiText.T("reallife_ui_bizman_lease") });
            }
            _m.StartSide = _startSide.ToArray();
        }

        private readonly List<BizManStartRow> _startSide = new List<BizManStartRow>(12);

        private void Row(string labelKey, string value)
        {
            if (!string.IsNullOrEmpty(value))
            {
                _startSide.Add(new BizManStartRow { Kind = BizManStartKind.Fact, Label = UiText.T(labelKey), Value = value });
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

        // Start business: the game's own button, which asks its own questions (a name, a kind, the kind's diploma) and answers a
        // refusal with its notice. The game writes the name into the place as it starts the business, before its logo is made, so
        // the place having one right after says the business was started.
        private bool SetUpBusiness(BizManBusiness b)
        {
            StartBusinessUI start = Get<StartBusinessUI>(GameContract.ShowStart, PageOf<BizManPresentation>(b, BizManRules.Presentation));
            int heard = ShellNotices.Heard;
            bool handed = Click(Calling(start, "SetUpBusiness"), "set up the business");
            if (handed)
            {
                BuildingRegistration reg = b.buildingRegistration;
                Outcome("start a business", b, reg != null && !string.IsNullOrEmpty(reg.BusinessName), heard);
            }
            return handed;
        }

        private readonly List<string> _checkHelp = new List<string>(8);   // the head's requirements' help pages, by the line's place; null where a line has none

        // A "?" or a guide link: the game's help opened at the page, as its own help button opens it (HelpIconButton.OnClick).
        private static bool OpenHelp(string slug)
        {
            HelpSystem help = InstanceBehavior<HelpSystem>.Instance;
            if (help == null || string.IsNullOrEmpty(slug))
            {
                _why = BizManRefusal.Gone;
                return false;
            }
            using (HotSpots.Game())
            {
                help.Toggle(show: true, slug.ToLowerInvariant());
            }
            UiDirector.Logger?.Info("BizMan (kit): the game's help opened at " + slug);
            return true;
        }

        private bool StartHelpPage(int arg) => arg >= 1 && arg <= _startHelp.Count && OpenHelp(_startHelp[arg - 1]);

        private bool CheckHelpPage(int arg) => arg >= 0 && arg < _checkHelp.Count && OpenHelp(_checkHelp[arg]);

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
