using System;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RealLifeOverhaul.Ui
{
    internal sealed partial class OptionsScreen
    {
        // One option of the game's custom difficulty: the game's own component for it, its words (the game's keys), the section it stands in
        // and the figure of the game's variables it stands for (what is applied now, to tell what the panel holds from it).
        private struct DiffDef
        {
            public GameMember Member;
            public string Name, Tip;
            public byte Section;      // 1 General, 2 Character, 3 Economy
            public bool Locked;       // set when the game began: the game applies the rest and keeps these
            public Func<GameVariables, float> Value;
        }

        private static readonly DiffDef[] Defs =
        {
            Def(GameContract.DiffDays, "main_menu_custom_game_days_per_year", null, 1, true, v => v.daysPerYear),
            Def(GameContract.DiffMoney, "main_menu_custom_game_starting_money", null, 1, true, v => v.startingMoney),
            Def(GameContract.DiffRivals, "main_menu_custom_game_rivals_difficulty_multiplier",
                "main_menu_custom_game_rivals_difficulty_multiplier_tooltip", 1, false, v => v.rivalsDifficultyMultiplier),
            Def(GameContract.DiffAge, "main_menu_custom_game_starting_age", null, 2, true, v => v.startingAge),
            Def(GameContract.DiffAging, "main_menu_custom_game_disable_aging", null, 2, false, v => v.disableAging ? 1f : 0f),
            Def(GameContract.DiffHappy, "main_menu_custom_game_disable_happiness", null, 2, false, v => v.disableHappiness ? 1f : 0f),
            Def(GameContract.DiffEnergy, "main_menu_custom_game_disable_energy", null, 2, false, v => v.disableEnergy ? 1f : 0f),
            Def(GameContract.DiffCourses, "main_menu_custom_game_all_courses_unlocked", "main_menu_custom_game_all_courses_unlocked_tooltip", 2, false, v => v.allCoursesUnlocked ? 1f : 0f),
            Def(GameContract.DiffContacts, "main_menu_custom_game_unlock_all_contacts", null, 2, false, v => v.allContactsUnlocked ? 1f : 0f),
            Def(GameContract.DiffDamage, "main_menu_custom_game_disable_vehicle_damage", null, 2, false, v => v.disableVehicleDamage ? 1f : 0f),
            Def(GameContract.DiffFuel, "main_menu_custom_game_disable_vehicle_fuel", null, 2, false, v => v.disableVehicleFuel ? 1f : 0f),
            Def(GameContract.DiffTax, "main_menu_custom_game_tax_percentage", "main_menu_custom_game_tax_percentage_tooltip", 3, false, v => v.taxPercentage),
            Def(GameContract.DiffWages, "main_menu_custom_game_employee_hourly_salary_multiplier", null, 3, false, v => v.employeeHourlySalaryMultiplier),
            Def(GameContract.DiffPrices, "main_menu_custom_game_market_price_multiplier", "main_menu_custom_game_market_price_multiplier_tooltip", 3, false, v => v.marketPriceMultiplier),
            Def(GameContract.DiffInterest, "main_menu_custom_game_bank_interest_multiplier", "main_menu_custom_game_bank_interest_multiplier_tooltip", 3, false, v => v.bankInterestMultiplier),
            Def(GameContract.DiffPromotion, "main_menu_custom_game_base_customer_promotion_multiplier",
                "main_menu_custom_game_base_customer_promotion_multiplier_tooltip", 3, false, v => v.baseCustomerPromotionMultiplier),
            Def(GameContract.DiffWholesale, "main_menu_custom_wholesale_urgent_fee_multiplier",
                "main_menu_custom_wholesale_urgent_fee_multiplier_tooltip", 3, false, v => v.wholesaleUrgentFeeMultiplier),
            Def(GameContract.DiffImporter, "main_menu_custom_importer_urgent_fee_multiplier", "main_menu_custom_importer_urgent_fee_multiplier_tooltip", 3, false, v => v.importerUrgentFeeMultiplier),
            Def(GameContract.DiffLimits, "main_menu_custom_disable_import_limits", "main_menu_custom_disable_import_limits_tooltip", 3, false, v => v.disableWholesaleAndImportLimits ? 1f : 0f),
            Def(GameContract.DiffCatalogue, "main_menu_custom_all_products_available_from_importers",
                "main_menu_custom_all_products_available_from_importers_tooltip", 3, false, v => v.allProductsAvailableFromImporters ? 1f : 0f),
            Def(GameContract.DiffExport, "main_menu_custom_export_price_multiplier", "main_menu_custom_export_price_multiplier_tooltip", 3, false, v => v.exportMultiplier),
            Def(GameContract.DiffSelling, "main_menu_custom_game_selling_multiplier", "main_menu_custom_game_selling_multiplier_tooltip", 3, false, v => v.sellingMultiplier)
        };

        private static DiffDef Def(GameMember member, string name, string tip, byte section, bool locked, Func<GameVariables, float> value)
        {
            return new DiffDef { Member = member, Name = name, Tip = tip, Section = section, Locked = locked, Value = value };
        }

        private string _presetName = string.Empty;

        // What the panel holds (the values its sliders and checkboxes have written into the game's current preset) and what the game applies now.
        private static GameVariables Held()
        {
            return GameContract.DiffPreset.Field != null ? GameContract.DiffPreset.Field.GetValue(null) as GameVariables : null;
        }

        private static GameVariables Applied() => SaveGameManager.Current != null ? SaveGameManager.Current.gameVariables : null;

        private static bool Differs(int def)
        {
            GameVariables held = Held(), applied = Applied();
            return held != null && applied != null && !Defs[def].Locked && Math.Abs(Defs[def].Value(held) - Defs[def].Value(applied)) > 0.0001f;
        }

        private int ChangedCount()
        {
            int n = 0;
            for (int i = 0; i < Defs.Length; i++)
            {
                n += Differs(i) ? 1 : 0;
            }
            return n;
        }

        // A hash of what the panel holds and its list of presets, read with the rest four times a second.
        private long DifficultySign()
        {
            long h = ChangedCount();
            GameVariables held = Held();
            for (int i = 0; held != null && i < Defs.Length; i++)
            {
                h = Mix(h, BitConverter.SingleToInt32Bits(Defs[i].Value(held)));
            }
            UI.Elements.Dropdown presets = Presets();
            h = Mix(h, presets != null ? presets.SelectedOptionIndex : -2);
            return h;
        }

        private UI.Elements.Dropdown Presets()
        {
            Component panel = Field<Component>(GameContract.OptCustomPanel);
            return BuyKits.Get<UI.Elements.Dropdown>(GameContract.DiffPresets, panel);
        }

        // ------------------------------------------------------------------------------------------------- the pages

        private void FillDifficulty(int group)
        {
            Component panel = Field<Component>(GameContract.OptCustomPanel);
            Component handler = BuyKits.Get<Component>(GameContract.DiffHandler, panel);
            if (group == 0)
            {
                Heading("reallife_ui_opt_diff_presets", UiIcon.Bookmark);
                UI.Elements.Dropdown presets = Presets();
                if (presets != null)
                {
                    Slot pick = Take(Kind.Choice);
                    pick.NameKey = "reallife_ui_opt_diff_preset";
                    pick.ValueKey = "reallife_ui_opt_diff_unsaved";
                    pick.D = presets;
                }
                Slot name = Take(Kind.Name);
                name.NameKey = "reallife_ui_opt_diff_name";
                AddAction("custom_game_presets_save_preset", "custom_game_presets_save_preset", Do.SavePreset, null);
                AddAction("custom_game_presets_tooltip_copy", "custom_game_presets_tooltip_copy", Do.CopyPreset, null);
                AddAction("custom_game_presets_tooltip_paste", "custom_game_presets_tooltip_paste", Do.PastePreset, null);
                AddAction("custom_game_presets_tooltip_delete", "custom_game_presets_tooltip_delete", Do.DeletePreset, null);
                return;
            }
            int options = 0;
            for (int i = 0; i < Defs.Length; i++)
            {
                options += Defs[i].Section == group ? 1 : 0;
            }
            Heading(_names[group], _icons[group], true);
            for (int i = 0; i < Defs.Length; i++)
            {
                if (Defs[i].Section == group)
                {
                    DifficultyRow(i, BuyKits.Get<Component>(Defs[i].Member, handler));
                }
            }
        }

        private void DifficultyRow(int def, Component option)
        {
            DiffDef d = Defs[def];
            if (d.Locked)
            {
                Slot locked = Take(Kind.Action);
                locked.NameKey = d.Name;
                locked.Locked = true;
                locked.Index = def;
                return;
            }
            if (option == null)
            {
                return;
            }
            Toggle toggle = BuyKits.Get<Toggle>(GameContract.CheckPiece, option);
            Slider slider = BuyKits.Get<Slider>(GameContract.SliderPiece, option);
            Slot s = Take(toggle != null ? Kind.Toggle : Kind.Slider);
            s.NameKey = d.Name;
            s.TipKey = d.Tip;
            s.Opt = option;
            s.Index = def;
            s.T = toggle;
            s.S = slider;
            s.Fig = BuyKits.Get<TMP_Text>(GameContract.SliderFigure, option);
        }

        // The panel's own band: what it holds against what the game applies now, and the two ways out of it.
        private void DifficultyChrome()
        {
            UiSettingsModel l = _m.List;
            int changed = ChangedCount();
            l.Preset = T("reallife_ui_opt_diff_note");
            l.Differs = changed > 0 ? T("reallife_ui_opt_diff_changed").Replace("{count}", changed.ToString(_inv)) : null;
            l.PresetActions = new[]
            {
                new UiActionModel { Label = T("common_discard_changes"), Kind = UiButtonKind.Secondary, Icon = UiIcon.Reset, Key = UiAction.Option, Disabled = changed == 0 },
                new UiActionModel { Label = T("common_apply_changes"), Kind = UiButtonKind.Primary, Icon = UiIcon.Check, Key = UiAction.Alt, Disabled = changed == 0 }
            };
        }

        private void WriteDifficulty(Slot s, ref UiSettingRow r)
        {
            if (s.Kind == Kind.Action && s.Locked)
            {
                GameVariables applied = Applied();
                r.Control = UiSettingControl.Action;
                float v = applied != null ? Defs[s.Index].Value(applied) : 0f;
                r.Value = s.Index == 1 ? UiText.Money(v) : ((int)v).ToString("N0", CultureInfo.CurrentCulture);
                r.Greyed = true;
                r.Tip = T("reallife_ui_opt_diff_locked");
            }
            else if (s.Opt != null)
            {
                r.Differs = Differs(s.Index);
            }
        }

        // ------------------------------------------------------------------------------------------------- the presses

        private bool RunDifficulty(Do act)
        {
            Component panel = Field<Component>(GameContract.OptCustomPanel);
            if (panel == null)
            {
                return false;
            }
            switch (act)
            {
                case Do.SavePreset:
                    return SavePreset(panel);
                case Do.CopyPreset:
                    return Call(GameContract.DiffCopy, panel);
                case Do.PastePreset:
                    return Call(GameContract.DiffPaste, panel);
                case Do.DeletePreset:
                    return Call(GameContract.DiffDelete, panel);
            }
            return false;
        }

        // Saving under a name: the game's own naming box is asked for, the name typed into its field and its own Confirm pressed, so the
        // game asks before it writes over a preset of that name and refuses an empty one.
        private bool SavePreset(Component panel)
        {
            string name = _presetName.Trim();
            if (name.Length == 0 || name.IndexOfAny(System.IO.Path.GetInvalidFileNameChars()) >= 0)
            {
                UI.Notification.Notifications.ShowError("notification_save_game_invalid_characters", "notification_save_game_invalid_characters", false);
                return false;
            }
            if (!Call(GameContract.DiffSave, panel))
            {
                return false;
            }
            Component naming = BuyKits.Get<Component>(GameContract.DiffNaming, panel);
            TMP_InputField field = BuyKits.Get<TMP_InputField>(GameContract.NameField, naming);
            Button confirm = BuyKits.Get<Button>(GameContract.NameButton, naming);
            UiWidgets.Type(field, name, false);
            bool done = UiWidgets.Press(confirm);
            if (!done && naming != null)
            {
                naming.gameObject.SetActive(false);   // the game would not take that name: its box is put away as its own Cancel does
            }
            _stamp++;
            return done;
        }

        private static bool Call(GameMember method, object on)
        {
            if (on == null || method.Method == null)
            {
                return false;
            }
            using (HotSpots.Game())
            {
                method.Method.Invoke(on, null);
            }
            return true;
        }

        private bool Discard()
        {
            Component panel = Field<Component>(GameContract.OptCustomPanel);
            _stamp++;
            return Call(GameContract.DiffFromCurrent, panel);
        }

        private bool Apply()
        {
            if (ChangedCount() == 0)
            {
                return false;
            }
            using (HotSpots.Game())
            {
                _o.ConfirmDifficultyCustomizeUI();   // the game's own: it asks first, then applies and closes the panel
            }
            return true;
        }
    }
}
