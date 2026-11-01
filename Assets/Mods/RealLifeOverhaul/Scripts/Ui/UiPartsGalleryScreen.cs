using System;
using System.Collections.Generic;
using System.Globalization;

namespace RealLifeOverhaul.Ui
{
    /// <summary>Where the parts' gallery stands: its page and every part's state, so the samples can show each state and the screen can change it.</summary>
    public sealed class UiPartsState
    {
        public int Page;
        public int OpenChoice = -1;          // the controls line (UiPartsGallery.Arg) whose list is open, or -1
        public int Van = 1;
        public float VanOffset;
        public int Ads = 250;
        public int Staff = 4;
        public int Bags = 120, Croissants, Shelves = 8;
        public bool Sundays = true, Music;
        public string Name = "Bloom Café";   // a business's own name, as the player typed it
        public string Search = string.Empty;
        public bool Listening = true;
        public int Focused = -1;             // the controls line with focus, for typing and stepping
        public int Sort = 3;
        public bool SortDown = true;
        public int Business = 1;             // 0 for every business: the gallery opens on one firm's staff, as a filter
        public int Skill;                    // 0 for every skill
        public string Query = string.Empty;
        public bool Picking;
        public readonly bool[] Picked = new bool[Staff40];
        public int Selected = -1;            // the shown row, or -1 for the first
        public float TableOffset;
        public int TableReveal = -1;
        public int LineFocus = -1, BarFocus = -1, CandleFocus = -1;
        public int Chosen = 6;
        public bool ShelvesEmpty;
        public int Held = -1;                // the slider held for Left and Right, or -1
        public bool Typing;                  // the business's name being typed (its caret shows)
        public float CatalogueOffset;
        public int CatalogueReveal = -1;
        public bool NewSave;
        public readonly bool[] Folded = new bool[7];
        public int Period;
        public float StatementOffset;
        public int StatementReveal = -1;

        public const int Staff40 = 40;

        public UiPartsState()
        {
            Folded[3] = true;   // one day folded, as the income statement folds a heading
        }
    }

    /// <summary>
    /// The parts' gallery as a screen: every part of the kit on its page, each answering as it would on a real screen (a
    /// choice opens its list, a slider and an amount step with Left and Right, a switch turns, words are typed, the table
    /// sorts, filters and picks, a chart reads the point under focus or the mouse, a card is chosen, a day of the statement
    /// folds), so the owner tries the parts from Options, Tests. Its words are the locale's (the game's keys where the game
    /// has the word), its sums the game's money; nothing it shows changes the game. Game-free (the words and the money's
    /// form come in from the director). Cost: a layout a press.
    /// </summary>
    public sealed partial class UiPartsGalleryScreen : IUiScreen, IUiStepping, IUiPointing
    {
        private readonly UiWords _words;
        private readonly Func<float, string> _money;
        private readonly UiPartsState _state = new UiPartsState();
        private UiPartsGalleryModel _model;
        private bool _dirty = true;

        /// <summary>Back closes the gallery (it stands over whatever showed when the test opened it).</summary>
        public Action Close;

        public UiPartsGalleryScreen(UiWords words, Func<float, string> money)
        {
            _words = words;
            _money = money;
        }

        public string Id => "kit_parts";

        public bool Dirty => _dirty;

        public void Lay(UiScene s, UiFrame frame, bool dark, IUiKeys keys)
        {
            _model = Model(_words, _state, _money);
            UiPartsGallery.Lay(s, _model, frame, dark, keys);
            _state.TableReveal = _state.CatalogueReveal = _state.StatementReveal = -1;
            _state.TableOffset = _model.Table.Offset;
            _state.CatalogueOffset = _model.Catalogue.Offset;
            _state.StatementOffset = _model.Statement.Offset;
            _dirty = false;
        }

        public bool Press(int command, int arg)
        {
            UiPartsState st = _state;
            bool done = true;
            switch (command)
            {
                case UiAppWindow.NavCommand:
                    st.Page = arg;
                    st.OpenChoice = -1;
                    break;
                case UiPartsGallery.Choice:
                    st.OpenChoice = st.OpenChoice == arg ? -1 : arg;
                    break;
                case UiPartsGallery.Slider:
                    st.Held = st.Held == arg ? -1 : arg;   // a press holds it for Left and Right, a second lets it go
                    break;
                case UiPartsGallery.Field:
                    st.Typing = arg == UiPartsGallery.Arg(true, 2, 0);
                    break;
                case UiDataTable.First + UiDataTable.Clear:
                    st.Query = string.Empty;
                    break;
                case UiPartsGallery.Option:
                    st.Van = arg;
                    st.OpenChoice = -1;
                    break;
                case UiPartsGallery.Less:
                case UiPartsGallery.More:
                    done = StepAmount(arg, command == UiPartsGallery.More ? 1 : -1);
                    break;
                case UiPartsGallery.Switch:
                    if (arg == UiPartsGallery.Arg(true, 0, 0))
                    {
                        st.Sundays = !st.Sundays;
                    }
                    else if (arg == UiPartsGallery.Arg(true, 0, 1))
                    {
                        st.Music = !st.Music;
                    }
                    else
                    {
                        done = false;
                    }
                    break;
                case UiPartsGallery.Key:
                    st.Listening = !st.Listening;
                    break;
                case UiDataTable.First + UiDataTable.Head:
                    st.SortDown = st.Sort == arg ? !st.SortDown : true;
                    st.Sort = arg;
                    break;
                case UiDataTable.First + UiDataTable.Filter:
                    if (arg == 0)
                    {
                        st.Business = (st.Business + 1) % (Businesses.Length + 1);
                    }
                    else
                    {
                        st.Skill = (st.Skill + 1) % (Skills.Length + 1);
                    }
                    break;
                case UiDataTable.First + UiDataTable.Row:
                    if (st.Picking)
                    {
                        st.Picked[RowPerson(arg)] = !st.Picked[RowPerson(arg)];
                    }
                    st.Selected = RowPerson(arg);
                    break;
                case UiDataTable.First + UiDataTable.Pick:
                    st.Picking = true;
                    break;
                case UiDataTable.First + UiDataTable.Done:
                case UiDataTable.First + UiDataTable.Mass:
                    st.Picking = false;
                    Array.Clear(st.Picked, 0, st.Picked.Length);
                    break;
                case UiChart.First + UiChart.Point: st.LineFocus = arg; break;
                case UiPartsGallery.BarsBase + UiChart.Point: st.BarFocus = arg; break;
                case UiPartsGallery.CandlesBase + UiChart.Point: st.CandleFocus = arg; break;
                case UiCatalogue.First + UiCatalogue.Card:
                    st.Chosen = arg;
                    break;
                case UiStatement.First + UiStatement.Period: st.Period = (st.Period + 1) % 2; break;
                case UiStatement.First + UiStatement.Group: st.Folded[arg] = !st.Folded[arg]; break;
                default:
                    done = command == UiPartsGallery.Amount
                           || command == UiCatalogue.First + UiCatalogue.Action || command == UiStatement.First + UiStatement.Line
                           || (command >= UiTrack.First && command <= UiTrack.First + 2);
                    break;
            }
            _dirty |= done;
            return done;
        }

        // The person a row of the table shows now, as the table is sorted and filtered.
        private int RowPerson(int row) => _model != null && row >= 0 && row < _model.Table.RowCount ? People(_model.Table.Rows[row]) : 0;

        private static int People(UiDataRow row) => row.Values != null && row.Values.Length > 7 ? row.Values[7] : 0;

        public void Focused(int command, int arg)
        {
            UiPartsState st = _state;
            int before = st.Selected;
            int was = st.Focused;
            st.Focused = command >= UiPartsGallery.Choice && command <= UiPartsGallery.Key ? arg : -1;
            if (st.Focused != was && (st.Held >= 0 || st.Typing))
            {
                st.Held = -1;   // leaving a held slider or a typed field lets it go
                st.Typing = false;
                _dirty = true;
            }
            if (command == UiDataTable.First + UiDataTable.Row)
            {
                st.Selected = RowPerson(arg);
            }
            else if (command == UiChart.First + UiChart.Point || command == UiPartsGallery.BarsBase + UiChart.Point || command == UiPartsGallery.CandlesBase + UiChart.Point)
            {
                Pointed(command, arg);
                return;
            }
            _dirty |= st.Selected != before;
        }

        public void Pointed(int command, int arg)
        {
            UiPartsState st = _state;
            int line = st.LineFocus, bar = st.BarFocus, candle = st.CandleFocus;
            if (command == UiChart.First + UiChart.Point)
            {
                st.LineFocus = arg;
            }
            else if (command == UiPartsGallery.BarsBase + UiChart.Point)
            {
                st.BarFocus = arg;
            }
            else if (command == UiPartsGallery.CandlesBase + UiChart.Point)
            {
                st.CandleFocus = arg;
            }
            _dirty |= line != st.LineFocus || bar != st.BarFocus || candle != st.CandleFocus;
        }

        public bool Back()
        {
            if (_state.Held >= 0)
            {
                _state.Held = -1;   // Back lets a held slider go before it closes anything
                _dirty = true;
                return true;
            }
            if (_state.OpenChoice >= 0)
            {
                _state.OpenChoice = -1;   // an open list closes first, as the game's Dropdown does
                _dirty = true;
                return true;
            }
            Close?.Invoke();
            return Close != null;
        }

        public void Tab(int step)
        {
            _state.Page = (_state.Page + step + UiPartsGallery.PageCount) % UiPartsGallery.PageCount;
            _state.OpenChoice = -1;
            _dirty = true;
        }

        public bool Act(UiAction action) => false;

        public void Reveal(int list, int index)
        {
            if (_model == null)
            {
                return;
            }
            if (list == _model.TableList)
            {
                _state.TableReveal = index;
            }
            else if (list == _model.StatementList)
            {
                _state.StatementReveal = index;
            }
            else if (_model.CatalogueList >= 0 && list >= _model.CatalogueList)
            {
                _state.CatalogueReveal = UiCatalogue.CardOf(_model.CatalogueColumns, _model.CatalogueList, list, index);
            }
            else if (_state.OpenChoice >= 0)
            {
                _state.VanOffset = Math.Max(0f, (index - 7) * UiTokens.ControlSmall);
            }
            _dirty = true;
        }

        public void Wheel(int clip, float notches)
        {
            float px = -notches * UiTokens.Row;
            switch (_state.Page)
            {
                case UiPartsGallery.PageControls: _state.VanOffset = Math.Max(0f, _state.VanOffset + px); break;
                case UiPartsGallery.PageTable: _state.TableOffset = Math.Max(0f, _state.TableOffset + px); break;
                case UiPartsGallery.PageCatalogue: _state.CatalogueOffset = Math.Max(0f, _state.CatalogueOffset + px); break;
                case UiPartsGallery.PageStatement: _state.StatementOffset = Math.Max(0f, _state.StatementOffset + px); break;
            }
            _dirty = true;
        }

        // Only a held slider takes Left and Right: a choice opens its list and an amount has its own minus and plus, so the d-pad
        // still crosses the page from any of them.
        public bool Steps(int command) => command == UiPartsGallery.Slider && _state.Held == _state.Focused && _state.Held >= 0;

        public void Step(int command, int arg, int step)
        {
            UiPartsState st = _state;
            if (command == UiPartsGallery.Slider)
            {
                if (arg == UiPartsGallery.Arg(false, 1, 0))
                {
                    st.Ads = Math.Max(0, Math.Min(AdsMost, st.Ads + step * 50));
                }
                else if (arg == UiPartsGallery.Arg(false, 1, 1))
                {
                    st.Staff = Math.Max(1, Math.Min(8, st.Staff + step));
                }
            }
            else if (command == UiPartsGallery.Amount)
            {
                StepAmount(arg, step);
            }
            else if (arg == UiPartsGallery.Arg(false, 0, 0))
            {
                st.Van = (st.Van + step + Vans.Length) % Vans.Length;
            }
            _dirty = true;
        }

        // An amount up or down within its ends, as the game's AmountSelector keeps it.
        private bool StepAmount(int arg, int step)
        {
            UiPartsState st = _state;
            if (arg == UiPartsGallery.Arg(false, 2, 0))
            {
                st.Bags = Math.Max(0, Math.Min(BagsMost, st.Bags + step * 10));
            }
            else if (arg == UiPartsGallery.Arg(false, 2, 1))
            {
                st.Croissants = Math.Max(0, Math.Min(BagsMost, st.Croissants + step * 10));
            }
            else if (arg == UiPartsGallery.Arg(false, 2, 2))
            {
                st.Shelves = Math.Max(0, Math.Min(ShelvesMost, st.Shelves + step));
            }
            else
            {
                return false;
            }
            return true;
        }

        private const int BagsMost = 1000, ShelvesMost = 8, AdsMost = 1000;

        // --------------------------------------------------------------------------------------------- the samples

        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        private static readonly string[] Vans =
        {
            "ba:vehicletype_vordpony", "ba:vehicletype_umcdesertdeliveryjob", "ba:vehicletype_umcnunavut", "ba:vehicletype_freighttruckt1", "ba:vehicletype_honzamimic",
            "ba:vehicletype_bima320", "ba:vehicletype_mersaidis500", "ba:vehicletype_ferdinand112", "ba:vehicletype_anselmoaf90", "ba:vehicletype_petrollsfanton",
            "ba:vehicletype_missamvillian", "ba:vehicletype_umcdesert"
        };

        private static readonly string[] VanWords =
        {
            "Vord Pony", "Delivery Van", "UMC Nunavut", "Freight Truck T1", "Honza Mimic", "BIMA 320", "Mersaidi S500", "Ferdinand 112", "Anselmo AF90", "Petrolls Fanton",
            "Lytte L6", "UMC Desert"
        };

        // People's and firms' own names: names, not words, so they read the same in every language, as the game's own staff do.
        private static readonly string[] Firsts =
        {
            "Maya", "Luis", "Hana", "Ava", "Marcus", "Priya", "Tom", "Elena", "Jonah", "Kenji", "Nadia", "Theo", "Ines", "Sam", "Dana", "Rosa", "Omar", "Lena", "Felix", "Grace"
        };

        private static readonly string[] Lasts =
        {
            "Okafor", "Romero", "Sato", "Lindqvist", "Hale", "Raman", "Becker", "Sokolova", "Reyes", "Mori", "Haddad", "Brandt", "Duarte", "Okoro", "Whitfield", "Stanley",
            "Farouk", "Novak", "Graham", "Achebe"
        };

        private static readonly string[] Businesses = { "Bloom Café", "Lantern Bay Games", "Harbour Freight", "Kestrel Law" };

        private const string Supplier = "AJ Pederson & Son", Landlord = "Midtown Properties";   // a store's and a landlord's own names

        private static readonly string[] Skills = { "ba:skill_customerservice", "ba:skill_cleaning", "ba:skill_programmer", "ba:skill_deliverydriver", "ba:skill_lawyer" };

        private static readonly string[] SkillWords = { "Customer Service", "Cleaning", "Programmer", "Delivery Driver", "Lawyer" };

        private static readonly UiIcon[] SkillIcons = { UiIcon.Headset, UiIcon.Wrench, UiIcon.Code, UiIcon.Truck, UiIcon.Scales };

        /// <summary>The gallery's view model in a state: its samples from the locale, its sums in the game's form of money.</summary>
        public static UiPartsGalleryModel Model(UiWords t, UiPartsState st, Func<float, string> money)
        {
            var m = new UiPartsGalleryModel
            {
                Title = t("reallife_ui_parts_title", "Interface parts"),
                Page = Math.Max(0, Math.Min(UiPartsGallery.PageCount - 1, st.Page)),
                Nav = new[]
                {
                    new UiNavEntry { Icon = UiIcon.Gear, Tint = UiRole.None, Label = t("reallife_ui_parts_nav_controls", "Controls") },
                    new UiNavEntry { Icon = UiIcon.People, Tint = UiRole.None, Label = t("reallife_ui_parts_nav_table", "Table") },
                    new UiNavEntry { Icon = UiIcon.ChartLineUp, Tint = UiRole.None, Label = t("reallife_ui_parts_nav_charts", "Charts") },
                    new UiNavEntry { Icon = UiIcon.ShoppingCart, Tint = UiRole.None, Label = t("reallife_ui_parts_nav_catalogue", "Catalogue") },
                    new UiNavEntry { Icon = UiIcon.Receipt, Tint = UiRole.None, Label = t("reallife_ui_parts_nav_statement", "Statement") },
                    new UiNavEntry { Icon = UiIcon.Flag, Tint = UiRole.None, Label = t("reallife_ui_parts_nav_track", "Track") },
                },
            };
            for (int i = 0; i < m.Nav.Length; i++)
            {
                m.Nav[i].Tint = UiIconTints.Of(m.Nav[i].Icon);
            }
            Controls(m, t, st, money);
            Table(m, t, st, money);
            Charts(m, t, st, money);
            Catalogue(m, t, st, money);
            Statement(m, t, st, money);
            Tracks(m, t);
            bool open = st.OpenChoice >= 0 && m.Page == UiPartsGallery.PageControls;
            bool picking = st.Picking && m.Page == UiPartsGallery.PageTable, held = st.Held >= 0 && m.Page == UiPartsGallery.PageControls;
            var prompts = new List<UiPrompt>
            {
                new UiPrompt { Action = UiAction.Move, Label = t("reallife_ui_prompt_move", "Move") },
                new UiPrompt
                {
                    Action = UiAction.Confirm, Label = open ? t("reallife_ui_prompt_choose", "Choose") : picking ? t("reallife_ui_parts_prompt_pick", "Pick")
                        : held ? t("reallife_ui_prompt_change", "Change") : t("reallife_ui_prompt_select", "Select")
                },
            };
            if (!open)
            {
                prompts.Add(new UiPrompt { Action = UiAction.TabPrev, Label = t("reallife_ui_prompt_page", "Page") });
            }
            prompts.Add(new UiPrompt { Action = UiAction.Back, Label = t("reallife_ui_prompt_back", "Back") });
            m.Prompts = prompts.ToArray();
            m.PromptCount = m.Prompts.Length;
            return m;
        }

        private static string On(UiWords t, bool on) => on ? t("reallife_ui_parts_on", "On") : t("reallife_ui_parts_off", "Off");

        private static void Controls(UiPartsGalleryModel m, UiWords t, UiPartsState st, Func<float, string> money)
        {
            int van = Math.Max(0, Math.Min(Vans.Length - 1, st.Van));
            m.Left = new[]
            {
                new UiPartsSection
                {
                    Title = t("reallife_ui_parts_sec_choices", "Choices"),
                    Rows = new[]
                    {
                        new UiPartsRow { Kind = UiPartsField.Choice, Label = t("reallife_ui_parts_van", "Delivery vehicle"), Value = t(Vans[van], VanWords[van]) },
                        new UiPartsRow { Kind = UiPartsField.Choice, Label = t("reallife_ui_parts_hours", "Opening hours"),
                            Value = t("reallife_ui_parts_hours_value", "{from} to {to}").Replace("{from}", "08:00").Replace("{to}", "20:00") },
                        new UiPartsRow { Kind = UiPartsField.Choice, Label = t("common_neighborhood", "Neighborhood"), Value = t("ba:neighborhood_midtown", "Midtown"), Disabled = true },
                    }
                },
                new UiPartsSection
                {
                    Title = t("reallife_ui_parts_sec_sliders", "Sliders"),
                    Rows = new[]
                    {
                        new UiPartsRow { Kind = UiPartsField.Slider, Label = t("reallife_ui_parts_ads", "Ads budget a day"), Value = money(st.Ads), Share = st.Ads / (float)AdsMost,
                            On = st.Held == UiPartsGallery.Arg(false, 1, 0) },
                        new UiPartsRow { Kind = UiPartsField.Slider, Label = t("reallife_ui_parts_staff", "Staff on each shift"), Value = st.Staff.ToString(Inv), Share = (st.Staff - 1) / 7f,
                            On = st.Held == UiPartsGallery.Arg(false, 1, 1) },
                        new UiPartsRow { Kind = UiPartsField.Slider, Label = t("reallife_ui_parts_markup", "Price markup"), Value = "35%", Share = 0.35f, Disabled = true },
                    }
                },
                new UiPartsSection
                {
                    Title = t("reallife_ui_parts_sec_amounts", "Amounts to order"),
                    Rows = new[]
                    {
                        Amount(t("ba:itemname_paperbag", "Paper Bag"), st.Bags, BagsMost, 10),
                        Amount(t("ba:itemname_croissant", "Croissant"), st.Croissants, BagsMost, 10),
                        Amount(t("ba:itemname_storageshelf", "Storage Shelf"), st.Shelves, ShelvesMost, 1),
                        new UiPartsRow { Kind = UiPartsField.Amount, Label = t("ba:itemname_cupcake", "Cupcake"), Value = "0", Disabled = true },
                    }
                },
            };
            m.Right = new[]
            {
                new UiPartsSection
                {
                    Title = t("reallife_ui_parts_sec_switches", "Switches"),
                    Rows = new[]
                    {
                        new UiPartsRow { Kind = UiPartsField.Switch, Label = t("reallife_ui_parts_sundays", "Open on Sundays"), On = st.Sundays, Value = On(t, st.Sundays) },
                        new UiPartsRow { Kind = UiPartsField.Switch, Label = t("reallife_ui_parts_music", "Music in the shop"), On = st.Music, Value = On(t, st.Music) },
                        new UiPartsRow { Kind = UiPartsField.Switch, Label = t("reallife_ui_parts_alcohol", "Sell alcohol"), Value = On(t, false), Disabled = true },
                    }
                },
                new UiPartsSection
                {
                    Title = t("reallife_ui_parts_sec_needs", "What it needs"),
                    Rows = new[]
                    {
                        new UiPartsRow { Kind = UiPartsField.Check, Label = t("ba:itemname_cashregister", "Cash Register"), On = true },
                        new UiPartsRow { Kind = UiPartsField.Check, Label = t("reallife_ui_parts_need_parking", "Parking for a delivery van"), On = true },
                        new UiPartsRow { Kind = UiPartsField.Check, Label = t("reallife_ui_parts_need_licence", "A licence to sell alcohol"), On = false },
                    }
                },
                new UiPartsSection
                {
                    Title = t("reallife_ui_parts_sec_fields", "Words and keys"),
                    Rows = new[]
                    {
                        new UiPartsRow { Kind = UiPartsField.Text, Label = t("reallife_ui_parts_name", "Business name"), Value = st.Name, On = st.Typing },
                        new UiPartsRow { Kind = UiPartsField.Search, Label = t("common_search", "Search"), Value = st.Search, Placeholder = t("reallife_ui_parts_search_goods", "Search goods") },
                        new UiPartsRow { Kind = UiPartsField.Text, Label = t("reallife_ui_parts_licence_no", "Licence number"),
                            Placeholder = t("reallife_ui_parts_licence_needs", "Needs a liquor licence"), Disabled = true },
                        new UiPartsRow { Kind = UiPartsField.Key, Label = t("reallife_ui_parts_key_phone", "Open the phone"), Value = "Tab" },
                        new UiPartsRow { Kind = UiPartsField.Key, Label = t("reallife_ui_parts_key_pause", "Pause the game"), Value = "Esc", Disabled = true },
                        new UiPartsRow { Kind = UiPartsField.Key, Label = t("reallife_ui_parts_key_photo", "Take a photo"), On = st.Listening,
                            Value = st.Listening ? t("reallife_ui_parts_press_key", "Press a key") : "C" },
                    }
                },
            };
            if (st.OpenChoice == UiPartsGallery.Arg(false, 0, 0))
            {
                m.OpenSection = 0;
                m.OpenRow = 0;
                m.Options = new string[Vans.Length];
                for (int i = 0; i < Vans.Length; i++)
                {
                    m.Options[i] = t(Vans[i], VanWords[i]);
                }
                m.OptionChosen = van;
                m.OptionOffset = st.VanOffset;
            }
        }

        private static UiPartsRow Amount(string label, int value, int most, int step) => new UiPartsRow
        {
            Kind = UiPartsField.Amount, Label = label, Value = value.ToString("#,0", Inv), CanLess = value > 0, CanMore = value + step <= most
        };

        // Forty staff of four firms, the same every time: each a face, a firm, a wage, the day hired, a skill, how content
        // they are and where they are now; filtered, searched and sorted as the state says.
        private static void Table(UiPartsGalleryModel m, UiWords t, UiPartsState st, Func<float, string> money)
        {
            UiDataTableModel table = m.Table;
            string[] status =
            {
                t("reallife_ui_parts_at_work", "At work"), t("reallife_ui_parts_off_today", "Off today"), t("common_in_training", "In Training"), t("reallife_ui_parts_sick", "Off sick")
            };
            UiRole[] tones = { UiRole.Ok, UiRole.Muted, UiRole.Accent, UiRole.Warn };
            UiRole[] moods = { UiRole.Ok, UiRole.Warn, UiRole.Danger };
            var rows = new List<UiDataRow>(UiPartsState.Staff40);
            string query = st.Query ?? string.Empty;
            for (int i = 0; i < UiPartsState.Staff40; i++)
            {
                int firm = i % Businesses.Length, skill = (i * 3 + i / 4) % Skills.Length;
                string name = Firsts[i % Firsts.Length] + " " + Lasts[(i * 7 + i / Firsts.Length * 3 + 3) % Lasts.Length];   // forty different people
                if ((st.Business > 0 && st.Business - 1 != firm) || (st.Skill > 0 && st.Skill - 1 != skill)
                    || (query.Length > 0 && name.IndexOf(query, StringComparison.OrdinalIgnoreCase) < 0))
                {
                    continue;
                }
                int wage = 16 + (i * 13) % 31, hired = 40 + (i * 37) % 380, level = 38 + (i * 29) % 61, content = 30 + (i * 41) % 69, where = (i * 5 + i / 4) % 4;
                rows.Add(new UiDataRow
                {
                    Cells = new[] { name, money(wage), DayWords(t, hired), level.ToString(Inv), content.ToString(Inv), status[where], Businesses[firm] },
                    Values = new[] { -1, wage, -hired, level, content, -1, -1, i },
                    Tones = new[] { UiRole.None, UiRole.None, UiRole.None, UiRole.None, UiRole.None, tones[where] },
                    Sub = t(Skills[skill], SkillWords[skill]),
                    Hue = UiColour.HueOf(name),
                    Ring = moods[content >= 65 ? 0 : content >= 45 ? 1 : 2],
                    Picked = st.Picked[i],
                });
            }
            int sort = Math.Max(0, Math.Min(5, st.Sort));
            rows.Sort((a, b) =>
            {
                int c = sort == 0 || sort == 5 ? string.CompareOrdinal(a.Cells[sort], b.Cells[sort]) : a.Values[sort].CompareTo(b.Values[sort]);
                c = st.SortDown ? -c : c;
                return c != 0 ? c : a.Values[7].CompareTo(b.Values[7]);   // ties in the order hired, so the list never shuffles
            });
            table.Columns = new[]
            {
                new UiDataColumn { Title = t("myemployees_employee_name", "Employee Name"), Kind = UiCellKind.Person, Sortable = true },
                new UiDataColumn { Title = t("myemployees_hourly_wage", "Hourly Wage"), Kind = UiCellKind.Money, Width = 96f, Sortable = true },
                new UiDataColumn { Title = t("reallife_ui_parts_col_hired", "Hired"), Kind = UiCellKind.Date, Width = 96f, Sortable = true },
                new UiDataColumn { Title = t("myemployees_primary_skill", "Primary Skill"), Kind = UiCellKind.Meter, Width = 144f, Sortable = true },
                new UiDataColumn { Title = t("myemployees_satisfaction", "Satisfaction"), Kind = UiCellKind.Figure, Width = 96f, Sortable = true, Graded = true },
                new UiDataColumn { Title = t("reallife_ui_parts_col_now", "Now"), Kind = UiCellKind.Chip, Width = 112f },
            };
            table.Rows = rows.ToArray();
            table.RowCount = rows.Count;
            table.SortColumn = sort;
            table.SortDown = st.SortDown;
            table.Filters = new[]
            {
                st.Business > 0 ? Businesses[st.Business - 1] : t("reallife_ui_parts_all_firms", "All businesses"),
                st.Skill > 0 ? t(Skills[st.Skill - 1], SkillWords[st.Skill - 1]) : t("reallife_ui_parts_all_skills", "All skills"),
            };
            table.SearchPlaceholder = t("reallife_ui_parts_search_staff", "Search staff");
            table.Query = query;
            table.Count = rows.Count == UiPartsState.Staff40
                ? t("reallife_ui_parts_people", "{count} people").Replace("{count}", rows.Count.ToString(Inv))
                : t("reallife_ui_parts_people_of", "{shown} of {count}").Replace("{shown}", rows.Count.ToString(Inv)).Replace("{count}", UiPartsState.Staff40.ToString(Inv));
            int picked = 0, wages = 0;
            for (int i = 0; i < UiPartsState.Staff40; i++)
            {
                picked += st.Picked[i] ? 1 : 0;
            }
            for (int r = 0; r < rows.Count; r++)
            {
                wages += rows[r].Values[1];
            }
            table.Picking = st.Picking;
            table.PickWords = t("reallife_ui_parts_pick", "Pick several");
            table.PickedWords = t("reallife_ui_parts_picked", "{count} picked").Replace("{count}", picked.ToString(Inv));
            table.DoneWords = t("reallife_ui_parts_done", "Done");
            table.Mass = new[]
            {
                new UiActionModel { Label = t("myemployees_mass_action_fire", "Fire"), Kind = UiButtonKind.Destructive, Key = UiAction.Count, Disabled = picked == 0 },
                new UiActionModel { Label = t("myemployees_mass_action_trainprimaryskill", "Train primary skill"), Kind = UiButtonKind.Secondary, Key = UiAction.Option, Disabled = picked == 0 },
                new UiActionModel { Label = t("myemployees_mass_action_paybonuses", "Pay Bonuses"), Kind = UiButtonKind.Primary, Key = UiAction.Alt, Disabled = picked == 0 },
            };
            table.TotalsLabel = t("common_total", "Total");
            table.Totals = rows.Count > 0 ? new[] { null, money(wages), null, null, null, null } : null;
            table.EmptyTitle = t("reallife_ui_parts_empty_title", "No one matches {query}").Replace("{query}", query);
            table.EmptyBody = t("reallife_ui_parts_empty_body", "Clear the search to see all {count} of your staff here.").Replace("{count}", UiPartsState.Staff40.ToString(Inv));
            table.EmptyIcon = UiIcon.Search;
            table.EmptyAction = t("reallife_ui_parts_clear_search", "Clear search");
            m.DetailEmptyTitle = t("reallife_ui_parts_detail_empty_title", "No one chosen");
            m.DetailEmptyBody = t("reallife_ui_parts_detail_empty_body", "Choose a row to see that person here.");
            table.Offset = st.TableOffset;
            table.Reveal = st.TableReveal;
            table.Selected = -1;
            int shown = st.Selected >= 0 ? st.Selected : rows.Count > 0 ? rows[0].Values[7] : -1;
            for (int r = 0; r < rows.Count; r++)
            {
                if (rows[r].Values[7] == shown)
                {
                    table.Selected = r;
                    UiDataRow row = rows[r];
                    int skill = (shown * 3 + shown / 4) % Skills.Length;
                    m.DetailRole = row.Sub;
                    m.DetailRoleIcon = SkillIcons[skill];
                    m.DetailLabels = new[] { t("myemployees_age", "Age"), t("myemployees_current_business", "Current Business"), t("myemployees_hourly_wage", "Hourly Wage"),
                        t("myemployees_hours_per_week", "Hours Per Week"), t("reallife_ui_parts_col_hired", "Hired") };
                    m.DetailValues = new[] { (21 + shown * 7 % 38).ToString(Inv), row.Cells[6], row.Cells[1], (24 + shown * 4 % 17).ToString(Inv), row.Cells[2] };
                    m.AttributeLabels = new[] { t("myemployees_primary_skill", "Primary Skill"), t("myemployees_satisfaction", "Satisfaction"), t("common_energy", "Energy") };
                    m.AttributeValues = new[] { row.Values[3], row.Values[4], 35 + shown * 23 % 64 };
                }
            }
        }

        private static string DayWords(UiWords t, int daysAgo) => t("common_day_number", "Day {number}").Replace("{number}", (412 - daysAgo).ToString(Inv));

        private static void Charts(UiPartsGalleryModel m, UiWords t, UiPartsState st, Func<float, string> money)
        {
            var months = new string[12];
            for (int i = 0; i < 12; i++)
            {
                months[i] = t(MonthKeys[i], MonthWords[i]);
            }
            float[] takings = { 142000f, 151000f, 149500f, 163000f, 171200f, 168000f, 184200f, 190500f, 186300f, 201000f, 208400f, 219600f };
            float[] costs = { 118000f, 121500f, 124000f, 126500f, 129000f, 131000f, 133200f, 134800f, 138000f, 139500f, 141200f, 144000f };
            var profit = new float[12];
            for (int i = 0; i < 12; i++)
            {
                profit[i] = takings[i] - costs[i];
            }
            m.Line = new UiChartModel
            {
                Kind = UiChartKind.Line, Title = t("reallife_ui_parts_chart_year", "Takings and costs this year"), Points = months, Format = money,
                Series = new[]
                {
                    new UiSeries { Label = t("reallife_ui_parts_takings", "Takings"), Values = takings, Tint = UiRole.Chart1 },
                    new UiSeries { Label = t("reallife_ui_parts_costs", "Costs"), Values = costs, Tint = UiRole.Chart2 },
                    new UiSeries { Label = t("reallife_ui_parts_profit", "Profit"), Values = profit, Tint = UiRole.Chart3 },
                },
                Focus = st.LineFocus,
            };
            Func<float, string> count = v => v.ToString("#,0", Inv);
            m.Bars = new UiChartModel
            {
                Kind = UiChartKind.Bars, Title = t("reallife_ui_parts_chart_districts", "Customers a day by district"), Format = count,
                Points = new[]
                {
                    t("ba:neighborhood_garmentdistrict", "Garment District"), t("ba:neighborhood_hellskitchen", "Hell's Kitchen"), t("ba:neighborhood_industrycity", "Industry City"),
                    t("ba:neighborhood_lowermanhattan", "Lower Manhattan"), t("ba:neighborhood_midtown", "Midtown"), t("ba:neighborhood_murrayhill", "Murray Hill"),
                },
                Series = new[] { new UiSeries { Label = t("reallife_ui_parts_customers", "Customers"), Values = new[] { 420f, 655f, 210f, 540f, 880f, 365f } } },
                BarTints = new[] { UiRole.Chart3, UiRole.Chart5, UiRole.Chart2, UiRole.Chart1, UiRole.Chart7, UiRole.Chart6 },
                Focus = st.BarFocus,
            };
            float[] close = new float[20], open = new float[20], high = new float[20], low = new float[20], volume = new float[20];
            float price = 42.6f;
            var days = new string[20];
            var reads = new string[20];
            for (int i = 0; i < 20; i++)
            {
                float drift = (float)Math.Sin(i * 0.9) * 1.4f + (i % 3 == 0 ? -0.9f : 0.6f);
                open[i] = price;
                close[i] = (float)Math.Round(price + drift, 2);
                high[i] = (float)Math.Round(Math.Max(open[i], close[i]) + 0.4f + (i % 4) * 0.3f, 2);
                low[i] = (float)Math.Round(Math.Min(open[i], close[i]) - 0.3f - (i % 5) * 0.25f, 2);
                volume[i] = 12000f + (i * 3700) % 21000;
                price = close[i];
                days[i] = (393 + i).ToString(Inv);
                reads[i] = DayWords(t, 19 - i);
            }
            m.Candles = new UiChartModel
            {
                Kind = UiChartKind.Candles, Title = t("reallife_ui_parts_chart_shares", "Lantern Bay Games shares, 20 days"), Points = days, Reads = reads,
                Open = open, High = high, Low = low, Close = close, Volume = volume, Format = v => "$" + v.ToString("0.00", Inv), CountFormat = count,
                VolumeLabel = t("reallife_ui_parts_volume", "Volume"), OpenLabel = t("reallife_ui_parts_open", "Open"), HighLabel = t("reallife_ui_parts_high", "High"),
                LowLabel = t("reallife_ui_parts_low", "Low"), CloseLabel = t("reallife_ui_parts_close", "Close"), Focus = st.CandleFocus,
            };
        }

        private static readonly string[] MonthKeys =
        {
            "reallife_calendar_month_1", "reallife_calendar_month_2", "reallife_calendar_month_3", "reallife_calendar_month_4", "reallife_calendar_month_5",
            "reallife_calendar_month_6", "reallife_calendar_month_7", "reallife_calendar_month_8", "reallife_calendar_month_9", "reallife_calendar_month_10",
            "reallife_calendar_month_11", "reallife_calendar_month_12"
        };

        private static readonly string[] MonthWords = { "January", "February", "March", "April", "May", "June", "July", "August", "September", "October", "November", "December" };

        private static void Catalogue(UiPartsGalleryModel m, UiWords t, UiPartsState st, Func<float, string> money)
        {
            string owned = t("reallife_ui_parts_owned", "Owned"), soldOut = t("reallife_ui_parts_sold_out", "Sold out"), locked = t("reallife_ui_parts_locked", "Locked");
            m.Catalogue = new UiCatalogueModel
            {
                Cards = new[]
                {
                    Good(t("ba:itemname_cashregister", "Cash Register"), UiIcon.Coin, money(450f), owned, UiRole.Ok),
                    Good(t("ba:itemname_computer", "Computer"), UiIcon.Cpu, money(1200f)),
                    Good(t("ba:itemname_securitycamera", "Security Camera"), UiIcon.Camera, money(380f)),
                    Good(t("ba:itemname_officephone", "Office Phone"), UiIcon.Phone, money(160f)),
                    Good(t("ba:itemname_storageshelf", "Storage Shelf"), UiIcon.Package, money(240f)),
                    Good(t("ba:itemname_industrialcoffeemachine", "Industrial Coffee Machine"), UiIcon.Coffee, money(2850f)),
                    Good(t("ba:itemname_painting40x30", "Painting 40x30"), UiIcon.Image, money(640f), null, UiRole.None, false, CityPicture, 4f / 3f),
                    Good(t("ba:itemname_loudspeaker2", "JayBeeel Loudspeaker (Large)"), UiIcon.MusicNotes, money(1450f)),
                    Good(t("ba:itemname_floorlamp", "Floor Lamp"), UiIcon.Lightbulb, money(90f)),
                    Good(t("ba:itemname_stackofshoppingbaskets", "Stack Of Shopping Baskets"), UiIcon.ShoppingCart, money(120f)),
                    Good(t("ba:itemname_saladbar", "Salad Bar"), UiIcon.ForkKnife, money(1900f), soldOut, UiRole.Danger, true),
                    Good(t("ba:itemname_trophygolf", "Golf Trophy"), UiIcon.Trophy, money(4200f), locked, UiRole.Muted, true),
                },
                CardCount = st.ShelvesEmpty ? 0 : 12,
                Chosen = Math.Max(-1, Math.Min(11, st.Chosen)),
                Offset = st.CatalogueOffset,
                Reveal = st.CatalogueReveal,
                DetailKicker = t("reallife_ui_parts_decoration", "Decoration"),
                DetailLabels = new[] { t("reallife_ui_parts_size", "Size"), t("reallife_ui_parts_stocked", "Stocked at") },
                DetailValues = new[] { "40 \u00d7 30", Supplier },
                DetailNote = t("reallife_ui_parts_painting_note", "A view of the city for any wall. Customers stay a little longer where the walls are dressed."),
                Actions = new[]
                {
                    new UiActionModel { Label = t("common_setdestination", "Set destination"), Kind = UiButtonKind.Secondary, Icon = UiIcon.MapPin, Key = UiAction.Option },
                    new UiActionModel { Label = t("common_purchase", "Purchase"), Kind = UiButtonKind.Primary, Key = UiAction.Alt },
                },
                EmptyTitle = t("reallife_ui_parts_shelves_empty", "The shelves are empty"),
                EmptyBody = t("reallife_ui_parts_shelves_empty_body", "New stock comes in each morning. Come back tomorrow."),
            };
        }

        /// <summary>The key of the painting's picture: a view of the city, which the game's own picture of the painting stands for.</summary>
        public const string CityPicture = "parts_painting_city";

        private static UiCatalogueCard Good(string name, UiIcon icon, string price, string state = null, UiRole tone = UiRole.None, bool off = false, string picture = null,
            float aspect = 0f) =>
            new UiCatalogueCard { Name = name, Icon = icon, Figure = price, State = state, StateTone = tone, Disabled = off, Picture = picture, Aspect = aspect };

        // A busy week of a coffee shop's account, a day a band, or a new save's account with nothing in it yet.
        private static void Statement(UiPartsGalleryModel m, UiWords t, UiPartsState st, Func<float, string> money)
        {
            // The week up to now: Friday to today, Tuesday Day 412 as the status bar says, today's lines before its 14:32.
            string[] dayKeys = { "common_friday", "common_saturday", "common_sunday", "common_monday", "common_tuesday" };
            string[] dayWords = { "Friday", "Saturday", "Sunday", "Monday", "Tuesday" };
            UiStatementModel s = m.Statement;
            s.Account = t("reallife_ui_parts_account", "Everyday account");
            s.AccountIcon = UiIcon.Bank;
            s.Number = t("reallife_ui_parts_account_line", "Midtown Savings, ending 4417");
            s.BalanceLabel = t("common_balance", "Balance");
            s.PeriodLabel = st.Period == 0 ? t("reallife_ui_parts_this_week", "This week") : t("reallife_ui_parts_last_week", "Last week");
            s.Columns = new[]
            {
                t("reallife_ui_parts_col_when", "When"), t("reallife_ui_parts_col_what", "What"), t("reallife_ui_parts_col_in", "In"), t("reallife_ui_parts_col_out", "Out"),
                t("common_balance", "Balance")
            };
            s.TotalLabels = new[] { t("reallife_ui_parts_money_in", "Money in"), t("reallife_ui_parts_money_out", "Money out"), t("reallife_ui_parts_change", "Change") };
            s.EmptyTitle = t("reallife_ui_parts_statement_empty", "No money has moved yet");
            s.EmptyBody = t("reallife_ui_parts_statement_empty_body", "Your first wages and takings show here, a line each, under the day they came in.");
            s.Offset = st.StatementOffset;
            s.Reveal = st.StatementReveal;
            if (st.NewSave)
            {
                // An account opened today: the same statement with nothing in it yet, beside the player's own money.
                s.Account = t("reallife_ui_parts_savings", "Savings account");
                s.AccountIcon = UiIcon.PiggyBank;
                s.Number = t("reallife_ui_parts_opened_today", "Opened today");
                s.Balance = money(0f);
                s.Groups = new UiStatementGroup[0];
                s.Totals = new[] { money(0f), money(0f), money(0f) };
                s.EmptyBody = t("reallife_ui_parts_statement_new_body", "Money you pay in shows here, a line each, under the day it came in.");
                return;
            }
            string sales = t("reallife_ui_parts_takings", "Takings"), wages = t("reallife_ui_parts_line_wages", "Wages"), rent = t("reallife_ui_parts_line_rent", "Rent");
            string power = t("reallife_ui_parts_line_power", "Electricity"), goods = t("reallife_ui_parts_line_goods", "Goods delivered"), tax = t("reallife_ui_parts_line_tax", "Sales tax");
            string shop = Businesses[0], depot = Businesses[2], landlord = Landlord;
            float balance = Opening, inSum = 0f, outSum = 0f;
            var groups = new UiStatementGroup[5];
            for (int d = 0; d < groups.Length; d++)
            {
                var lines = new List<UiStatementLine>(6);
                float total = 0f;
                Action<string, UiIcon, string, string, float> add = (when, icon, what, where, amount) =>
                {
                    balance += amount;
                    total += amount;
                    if (amount >= 0f)
                    {
                        inSum += amount;
                    }
                    else
                    {
                        outSum -= amount;
                    }
                    lines.Add(new UiStatementLine { When = when, Icon = icon, What = what, Where = where, In = amount >= 0f ? money(amount) : null,
                        Out = amount < 0f ? money(-amount) : null, Balance = money(balance) });
                };
                // The day's lines in the order they came, each with the balance after it.
                add("08:00", UiIcon.Truck, goods, depot, -(380f + d * 45f));
                if (d == 0)
                {
                    add("09:00", UiIcon.Home, rent, landlord, -2400f);
                }
                if (d == 2)
                {
                    add("10:15", UiIcon.Lightning, power, shop, -186f);
                }
                add("12:30", UiIcon.Coin, sales, shop, 1240f + d * 85f);
                if (d < 4)
                {
                    add("18:00", UiIcon.Coin, sales, shop, 2210f + d * 130f);
                    add("18:30", UiIcon.People, wages, shop, -(640f + d * 20f));
                }
                if (d == 3)
                {
                    add("23:59", UiIcon.Percent, tax, shop, -412f);
                }
                groups[d] = new UiStatementGroup
                {
                    Label = t(dayKeys[d], dayWords[d]) + ", " + DayWords(t, 4 - d), Total = (total >= 0f ? "+" : "−") + money(Math.Abs(total)), TotalGood = total >= 0f,
                    Folded = st.Folded[d], Lines = lines.ToArray()
                };
            }
            Array.Reverse(groups);   // the latest day first, as a bank's statement reads
            s.Groups = groups;
            s.Balance = money(balance);
            float change = inSum - outSum;
            s.Totals = new[] { money(inSum), money(outSum), (change >= 0f ? "+" : "−") + money(Math.Abs(change)) };
            s.ChangeGood = change >= 0f;
        }

        private const float Opening = 39568f;   // the week's opening balance: it closes at the status bar's $48,210

        private static void Tracks(UiPartsGalleryModel m, UiWords t)
        {
            string[] states =
            {
                t("reallife_ui_parts_step_done", "Done"), t("reallife_ui_parts_step_now", "Now"), t("reallife_ui_parts_step_next", "Next"), t("reallife_ui_parts_step_locked", "Locked"),
                t("reallife_ui_parts_step_later", "Later")
            };
            m.Tracks = new[]
            {
                new UiTrackModel
                {
                    Title = t("reallife_ui_parts_track_career", "Your career at the coffee shop"), Line = t("reallife_ui_parts_track_career_line", "Shift lead for 64 days"), StateWords = states,
                    Steps = new[]
                    {
                        Step(UiIcon.Coffee, t("reallife_ui_parts_rung_1", "Barista"), DayWords(t, 210), UiStepState.Done),
                        Step(UiIcon.Star, t("reallife_ui_parts_rung_2", "Shift lead"), DayWords(t, 64), UiStepState.Now),
                        Step(UiIcon.IdBadge, t("reallife_ui_parts_rung_3", "Store manager"), t("reallife_ui_parts_rung_3_needs", "Needs 80 skill"), UiStepState.Next),
                        Step(UiIcon.MapTrifold, t("reallife_ui_parts_rung_4", "Area manager"), t("reallife_ui_parts_rung_4_needs", "Needs a degree"), UiStepState.Locked),
                        Step(UiIcon.Key, t("reallife_ui_parts_rung_5", "Partner"), t("reallife_ui_parts_rung_5_needs", "Needs a stake"), UiStepState.Locked),
                    }
                },
                new UiTrackModel
                {
                    Title = t("reallife_ui_parts_track_game", "Cinder Valley milestones"), Line = t("reallife_ui_parts_track_game_line", "Playtest tomorrow"), StateWords = states,
                    Steps = new[]
                    {
                        Step(UiIcon.Lightbulb, t("reallife_ui_parts_mile_1", "Concept"), DayWords(t, 90), UiStepState.Done),
                        Step(UiIcon.Code, t("reallife_ui_parts_mile_2", "Prototype"), DayWords(t, 41), UiStepState.Done),
                        Step(UiIcon.Bug, t("reallife_ui_parts_mile_3", "Playtest"), t("common_tomorrow", "Tomorrow"), UiStepState.Now),
                        Step(UiIcon.Megaphone, t("reallife_ui_parts_mile_4", "Marketing"), InDays(t, 18), UiStepState.Next),
                        Step(UiIcon.Rocket, t("reallife_ui_parts_mile_5", "Launch"), InDays(t, 40), UiStepState.Later),
                    }
                },
                new UiTrackModel
                {
                    Title = t("reallife_ui_parts_track_build", "Warehouse on 9th Avenue"), Line = t("reallife_ui_parts_track_build_line", "The frame is going up"), StateWords = states,
                    Steps = new[]
                    {
                        Step(UiIcon.Presentation, t("reallife_ui_parts_stage_1", "Plans"), DayWords(t, 30), UiStepState.Done),
                        Step(UiIcon.Hammer, t("reallife_ui_parts_stage_2", "Foundations"), DayWords(t, 12), UiStepState.Done),
                        Step(UiIcon.Building, t("reallife_ui_parts_stage_3", "Frame"), t("common_days_left", "{days} days left").Replace("{days}", "6"), UiStepState.Now),
                        Step(UiIcon.Sofa, t("reallife_ui_parts_stage_4", "Fit out"), InDays(t, 14), UiStepState.Next),
                        Step(UiIcon.Store, t("reallife_ui_parts_stage_5", "Open"), InDays(t, 21), UiStepState.Later),
                    }
                },
            };
        }

        private static string InDays(UiWords t, int days) => t("reallife_ui_parts_in_days", "In {value} days").Replace("{value}", days.ToString(Inv));

        private static UiTrackStep Step(UiIcon icon, string label, string note, UiStepState state) => new UiTrackStep { Icon = icon, Label = label, Note = note, State = state };
    }
}
