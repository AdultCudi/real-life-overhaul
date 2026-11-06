using System;
using RealLifeOverhaul.GameUi;
using UI;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>The pages of <see cref="GarageShop"/>'s list: what each offers, and what a click puts in the cart.</summary>
    public sealed partial class GarageShop
    {
        private void FillMain(TravelList list)
        {
            int bill = Bill();
            Row(list, bill > 0 ? GarageText.T("reallife_garage_pay", "amount", GarageText.Money(bill)) : GarageText.T("reallife_customshop_done"), GarageText.T("reallife_garage_work_note"),
                bill > 0 ? "reallife_garage_list_pay" : "reallife_customshop_done", true, _pay);

            bool paintable = _fitted.Paintable;
            string paint = !paintable ? GarageText.T("reallife_garage_no_paint")
                : (_cart.Paint < 0 ? GarageText.T("reallife_garage_paint_factory") : "#" + _cart.Paint.ToString("X6")) + ". "
                  + GarageText.T("reallife_garage_paint_prices", "palette", GarageText.Money(GarageCatalogue.PaintPrice(_kind, false)),
                      "custom", GarageText.Money(GarageCatalogue.PaintPrice(_kind, true)));
            Row(list, GarageText.T("reallife_garage_tab_paint"), paint, "reallife_garage_list_choose", paintable, _toPaint);
            Row(list, GarageText.T("reallife_garage_tab_finish"), paintable ? GarageText.T(GarageCatalogue.FinishName(_cart.Finish)) : GarageText.T("reallife_garage_no_paint"),
                "reallife_garage_list_choose", paintable, _toFinish);

            string lights = GarageText.T(GarageCatalogue.LampName(_cart.Lamps));
            if (_cart.Glow >= 0)
            {
                lights += ", " + GarageText.T("reallife_garage_summary_glow");
            }
            Row(list, GarageText.T("reallife_garage_tab_lights"), lights, "reallife_garage_list_choose", true, _toLights);

            _text.Length = 0;
            Fitted(_cart.Spoiler, "reallife_garage_body_spoiler");
            Fitted(_cart.Rack, "reallife_garage_body_rack");
            Fitted(_cart.Bar, "reallife_garage_body_bar");
            for (int i = 0; i < WheelIds.Length; i++)
            {
                Fitted(_cart.Wheels == WheelIds[i], WheelNames[i]);
            }
            Row(list, GarageText.T("reallife_garage_tab_body"), _text.Length > 0 ? _text.ToString() : GarageText.T("reallife_garage_part_stock"), "reallife_garage_list_choose", true, _toBody);

            _text.Length = 0;
            for (int s = 0; s < GarageCatalogue.Slots; s++)
            {
                PartDef part = GarageCatalogue.Part(_cart.Parts[s]);
                if (part != null)
                {
                    Fitted(true, part.Name);
                }
            }
            bool tuning = Settings.GarageTuning;
            Row(list, GarageText.T("reallife_garage_tab_parts"), !tuning ? GarageText.T("reallife_garage_parts_off") : _text.Length > 0 ? _text.ToString() : GarageText.T("reallife_garage_part_stock"),
                "reallife_garage_list_choose", tuning, _toParts);
            Row(list, GarageText.T("reallife_tuning_tab_figures"), GarageText.T("reallife_tuning_tab_figures_detail"), "reallife_tuning_list_read", true, _toFigures);

            Row(list, GarageText.T("reallife_garage_stock"), GarageText.T("reallife_garage_list_stock_detail"), "reallife_garage_list_reset", !_cart.IsStock, _stock);
        }

        private void Fitted(bool fitted, string nameKey)
        {
            if (fitted)
            {
                if (_text.Length > 0)
                {
                    _text.Append(", ");
                }
                _text.Append(GarageText.T(nameKey));
            }
        }

        // The button's word for an option: the one in the cart cannot be chosen again.
        private static string Choose(bool chosen)
        {
            return chosen ? "reallife_garage_list_chosen" : "reallife_garage_list_choose";
        }

        private const int Shades = 7;   // the chart: six families of seven shades, light to dark (GarageCatalogue.Palette)

        // A family to an entry: its button steps through the family's shades.
        private void FillPaint(TravelList list)
        {
            Row(list, GarageText.T("reallife_garage_paint_factory"), _saved.Paint < 0 ? GarageText.T("reallife_garage_fitted") : GarageText.Money(GarageCatalogue.PaintPrice(_kind, false)),
                Choose(_cart.Paint < 0), _cart.Paint >= 0, () => PickPaint(-1));
            int[] palette = GarageCatalogue.Palette;
            string price = GarageText.Money(GarageCatalogue.PaintPrice(_kind, false));
            for (int f = 0; f * Shades < palette.Length && f < Families.Length; f++)
            {
                int family = f;
                int first = f * Shades;
                int count = Mathf.Min(Shades, palette.Length - first);
                int at = -1;
                for (int i = 0; i < count; i++)
                {
                    if (palette[first + i] == _cart.Paint)
                    {
                        at = i;
                    }
                }
                // The family's name in the shade chosen (the middle one while none is), lightened where it would not show.
                int sample = palette[first + (at >= 0 ? at : count / 2)];
                Color shown = GarageCar.FromRgb(sample);
                if (shown.grayscale < 0.22f)
                {
                    shown = Color.Lerp(shown, Color.white, 0.4f);
                }
                string detail = at >= 0
                    ? GarageText.T("reallife_garage_list_shade", "n", (at + 1).ToString(), "all", count.ToString()) + " #" + sample.ToString("X6") + ", "
                      + (sample == _saved.Paint ? GarageText.T("reallife_garage_fitted") : price) + "."
                    : GarageText.T("reallife_garage_list_shades", "all", count.ToString(), "price", price);
                int next = palette[first + (at + 1) % count];
                Row(list, "<color=#" + ColorUtility.ToHtmlStringRGB(shown) + ">" + GarageText.T(Families[family]) + "</color>", detail, "reallife_garage_list_next", true, () => PickPaint(next));
            }
            bool mixed = _cart.Paint >= 0 && !GarageCatalogue.InPalette(_cart.Paint);
            Row(list, GarageText.T("reallife_garage_list_mix"), mixed
                    ? "#" + _cart.Paint.ToString("X6") + ". " + GarageText.T("reallife_garage_paint_mixed", "amount", GarageText.Money(GarageCatalogue.PaintPrice(_kind, true)))
                    : GarageText.T("reallife_garage_list_mix_detail", "amount", GarageText.Money(GarageCatalogue.PaintPrice(_kind, true))),
                "reallife_garage_list_open", true, _mix);
        }

        private void PickPaint(int colour)
        {
            _cart.Paint = colour;
            Changed("paint " + (colour < 0 ? "factory" : "#" + colour.ToString("X6")));
        }

        // The game's own colour picker: its button keeps the colour, closing it any other way puts the paint back.
        private void MixOpen()
        {
            UIs uis = InstanceBehavior<UIs>.Instance;
            CustomColorPicker picker = uis != null ? uis.customColorPicker : null;
            if (picker == null)
            {
                GameUi.UiClone.Fallback("the game's colour picker is not there, so only the chart's colours can be chosen");
                return;
            }
            _mixFrom = _cart.Paint;
            _mixKept = false;
            picker.onColorPick = _onMixed;
            picker.Open(_onMixing, _cart.Paint >= 0 ? GarageCar.FromRgb(_cart.Paint) : _fitted.StockColour());
            _mixing = true;
            if (Meta.Dev.Verbose)
            {
                Logger?.Info("Garage check (custom shop list): the game's colour picker is open for a mixed colour.");
            }
        }

        private void Mixing(Color colour)
        {
            if (!_open || _cart == null)
            {
                return;
            }
            _cart.Paint = GarageCar.ToRgb(colour);
            Preview();
        }

        private void Mixed(Color colour)
        {
            Mixing(colour);
            _mixKept = _open;
        }

        private void FillFinish(TravelList list)
        {
            for (int i = 0; i <= (int)PaintFinish.Pearl; i++)
            {
                var finish = (PaintFinish)i;
                string price = finish == _saved.Finish ? GarageText.T("reallife_garage_fitted") : GarageText.Money(GarageCatalogue.FinishPrice(finish, _kind));
                Row(list, GarageText.T(GarageCatalogue.FinishName(finish)), i == 0 ? price + ". " + GarageText.T("reallife_garage_finish_note") : price,
                    Choose(finish == _cart.Finish), finish != _cart.Finish, () =>
                {
                    _cart.Finish = finish;
                    Changed("finish " + finish);
                });
            }
        }

        private void FillLights(TravelList list)
        {
            for (int i = 0; i <= (int)LampTint.Yellow; i++)
            {
                var tint = (LampTint)i;
                string price = tint == _saved.Lamps ? GarageText.T("reallife_garage_fitted")
                    : GarageText.Money(tint == LampTint.Stock ? GarageCatalogue.RemovalPrice : GarageCatalogue.LampPrice);
                Row(list, GarageText.T("reallife_garage_lamps_header") + ": " + GarageText.T(GarageCatalogue.LampName(tint)), price, Choose(tint == _cart.Lamps), tint != _cart.Lamps, () =>
                {
                    _cart.Lamps = tint;
                    Changed("lamps " + tint);
                });
            }
            string glow = GarageText.T("reallife_garage_glow_header", "price", GarageText.Money(GarageCatalogue.GlowPrice));
            Row(list, GarageText.T("reallife_garage_summary_glow") + ": " + GarageText.T("reallife_customshop_none"), glow, Choose(_cart.Glow < 0), _cart.Glow >= 0, () =>
            {
                _cart.Glow = -1;
                Changed("underglow off");
            });
            int[] colours = GarageCatalogue.GlowColours;
            for (int i = 0; i < colours.Length; i++)
            {
                int colour = colours[i];
                Row(list, "<color=#" + colour.ToString("X6") + ">" + GarageText.T("reallife_garage_summary_glow") + " " + (i + 1) + "</color>",
                    "#" + colour.ToString("X6") + ". " + GarageText.T("reallife_garage_glow_note"),
                    Choose(colour == _cart.Glow), colour != _cart.Glow, () =>
                    {
                        _cart.Glow = colour;
                        Changed("underglow #" + colour.ToString("X6"));
                    });
            }
        }

        private void FillBody(TravelList list)
        {
            string type = _car.vehicleType != null ? _car.vehicleType.vehicleTypeName : null;
            BodyRow(list, "reallife_garage_body_spoiler", GarageCatalogue.SpoilerPrice, GarageFit.Spoiler(type, _kind), _cart.Spoiler, _saved.Spoiler, () => _cart.Spoiler = !_cart.Spoiler);
            BodyRow(list, "reallife_garage_body_rack", GarageCatalogue.RackPrice, GarageFit.Rack(type, _kind), _cart.Rack, _saved.Rack, () => _cart.Rack = !_cart.Rack);
            BodyRow(list, "reallife_garage_body_bar", GarageCatalogue.BarPrice, GarageFit.Bar(type, _kind), _cart.Bar, _saved.Bar, () => _cart.Bar = !_cart.Bar);

            bool any = false;
            string header = GarageText.T("reallife_garage_wheels_header") + ": ";
            for (int i = 0; i < WheelIds.Length; i++)
            {
                string id = WheelIds[i];
                if (!PropLibrary.TryGet(id, out Mesh mesh, out Material material) || mesh == null || material == null)
                {
                    continue;   // not in the bundle: nothing stands in for a wheel
                }
                if (!any)
                {
                    any = true;
                    Row(list, header + GarageText.T("reallife_garage_wheels_own"), string.Empty, Choose(_cart.Wheels.Length == 0), _cart.Wheels.Length != 0, () =>
                    {
                        _cart.Wheels = string.Empty;
                        Changed("the car's own wheels");
                    });
                }
                string price = id == _saved.Wheels ? GarageText.T("reallife_garage_fitted") : GarageText.Money(GarageCatalogue.WheelsPrice);
                Row(list, header + GarageText.T(WheelNames[i]), price, Choose(_cart.Wheels == id), _cart.Wheels != id, () =>
                {
                    _cart.Wheels = id;
                    Changed("wheels " + id);
                });
            }
            if (!any)
            {
                Row(list, GarageText.T("reallife_garage_wheels_header"), GarageText.T("reallife_garage_wheels_none"), "reallife_garage_list_choose", false, null);
            }
        }

        private void BodyRow(TravelList list, string nameKey, int price, bool fits, bool inCart, bool fitted, Action toggle)
        {
            if (!fits)
            {
                Row(list, GarageText.T(nameKey), GarageText.T("reallife_garage_body_no_fit", "part", GarageText.T(nameKey)), "reallife_garage_list_fit", false, null);
                return;
            }
            string cost = fitted == inCart ? (fitted ? GarageText.T("reallife_garage_fitted") : GarageText.Money(price))
                : GarageText.Money(inCart ? price : GarageCatalogue.RemovalPrice);
            Row(list, GarageText.T(nameKey), cost, inCart ? "reallife_garage_list_remove" : "reallife_garage_list_fit", true, () =>
            {
                toggle();
                Changed(nameKey + (inCart ? " off" : " on"));
            });
        }

        private void FillParts(TravelList list)
        {
            for (int s = 0; s < GarageCatalogue.Slots; s++)
            {
                int slot = s;
                PartDef part = GarageCatalogue.Part(_cart.Parts[slot]);
                string price = _cart.Parts[slot] == _saved.Parts[slot]
                    ? (part != null ? GarageText.T("reallife_garage_fitted") : string.Empty)
                    : GarageText.Money(part != null ? GarageCatalogue.PartPrice(part, _kind) : GarageCatalogue.RemovalPrice);
                string effects = part != null ? Effects(part) : string.Empty;
                string detail = price.Length > 0 && effects.Length > 0 ? price + ". " + effects : price + effects;
                if (part != null && part.Sound != null && Settings.GarageSounds)
                {
                    detail += (detail.Length > 0 ? ". " : string.Empty) + GarageText.T(part.Sound);   // heard on the throttle key here
                }
                Row(list, GarageText.T(GarageCatalogue.SlotName((PartSlot)slot)) + ": " + (part != null ? GarageText.T(part.Name) : GarageText.T("reallife_garage_part_stock")), detail,
                    "reallife_garage_list_next", true, () => Cycle(slot, 1));
            }
        }

        // The next part for a slot: stock, then every part of that slot this kind of car can take, and round again.
        private void Cycle(int slot, int step)
        {
            PartDef[] all = GarageCatalogue.Parts;
            int at = -1;
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i].Id == _cart.Parts[slot])
                {
                    at = i;
                    break;
                }
            }
            for (int tries = 0; tries <= all.Length; tries++)
            {
                at += step;
                if (at < -1)
                {
                    at = all.Length - 1;
                }
                else if (at >= all.Length)
                {
                    at = -1;
                }
                if (at == -1)
                {
                    _cart.Parts[slot] = null;
                    break;
                }
                PartDef part = all[at];
                if ((int)part.Slot == slot && (!part.UtilityOnly || _kind == CarKind.Utility || _kind == CarKind.Van))
                {
                    _cart.Parts[slot] = part.Id;
                    break;
                }
            }
            Changed((PartSlot)slot + " " + (_cart.Parts[slot] ?? "stock"));
        }

        // The figures page: each figure as standard, as the car came in, and with this visit's work, by the same model
        // that drives it. Three runs of the sums, on opening the page and on each click there.
        private void FillFigures(TravelList list)
        {
            var car = _car as CarController;
            GarageFigures standard = GarageTuning.Figures(car, Standard);
            GarageFigures now = GarageTuning.Figures(car, _saved);
            GarageFigures after = GarageTuning.Figures(car, _cart);
            string note = GarageText.T("reallife_tuning_fig_note");
            list.AddNote(GarageText.T("reallife_tuning_tab_figures"), note, list.HeightFor(note.Length, false));

            Figure(list, "reallife_tuning_fig_power", TuningRules.Compare(standard.PowerKw, now.PowerKw, after.PowerKw, 0.5f),
                Power(standard.PowerKw), Power(now.PowerKw), Power(after.PowerKw));
            Figure(list, "reallife_tuning_fig_torque", TuningRules.Compare(standard.TorqueNm, now.TorqueNm, after.TorqueNm, 0.5f),
                Unit("reallife_tuning_unit_nm", standard.TorqueNm, "F0"), Unit("reallife_tuning_unit_nm", now.TorqueNm, "F0"), Unit("reallife_tuning_unit_nm", after.TorqueNm, "F0"));
            Figure(list, "reallife_tuning_fig_to100", TuningRules.Compare(standard.To100, now.To100, after.To100, 0.05f),
                Unit("reallife_tuning_unit_s", standard.To100, "F1"), Unit("reallife_tuning_unit_s", now.To100, "F1"), Unit("reallife_tuning_unit_s", after.To100, "F1"));
            Figure(list, "reallife_tuning_fig_top", TuningRules.Compare(standard.TopKmh, now.TopKmh, after.TopKmh, 0.5f),
                Unit("reallife_tuning_unit_kmh", standard.TopKmh, "F0"), Unit("reallife_tuning_unit_kmh", now.TopKmh, "F0"), Unit("reallife_tuning_unit_kmh", after.TopKmh, "F0"));
            Figure(list, "reallife_tuning_fig_shift", TuningRules.Compare(standard.ShiftSeconds, now.ShiftSeconds, after.ShiftSeconds, 0.005f),
                Unit("reallife_tuning_unit_s", standard.ShiftSeconds, "F2"), Unit("reallife_tuning_unit_s", now.ShiftSeconds, "F2"), Unit("reallife_tuning_unit_s", after.ShiftSeconds, "F2"));
            Figure(list, "reallife_tuning_fig_stop", TuningRules.Compare(standard.StopMetres, now.StopMetres, after.StopMetres, 0.5f),
                Unit("reallife_tuning_unit_m", standard.StopMetres, "F0"), Unit("reallife_tuning_unit_m", now.StopMetres, "F0"), Unit("reallife_tuning_unit_m", after.StopMetres, "F0"));
            Figure(list, "reallife_tuning_fig_weight", TuningRules.Compare(standard.MassKg, now.MassKg, after.MassKg, 0.5f),
                Unit("reallife_tuning_unit_kg", standard.MassKg, "F0"), Unit("reallife_tuning_unit_kg", now.MassKg, "F0"), Unit("reallife_tuning_unit_kg", after.MassKg, "F0"));
            Figure(list, "reallife_tuning_fig_ride", TuningRules.CompareAny(0f, now.RideMetres, after.RideMetres, 0.005f),
                Ride(0f), Ride(now.RideMetres), Ride(after.RideMetres));
            Figure(list, "reallife_tuning_fig_grip", TuningRules.Compare(Grip(standard), Grip(now), Grip(after), 0.005f),
                GripText(standard), GripText(now), GripText(after));

            GarageModifiers was = _saved.Modifiers();
            GarageModifiers will = _cart.Modifiers();
            Figure(list, "reallife_tuning_fig_costs", TuningRules.CompareAny(0f, was.Wear + was.Insurance, will.Wear + will.Insurance, 0.001f),
                Costs(0f, 0f), Costs(was.Wear, was.Insurance), Costs(will.Wear, will.Insurance));
            if (Settings.GarageResale)
            {
                int worthNow = GarageBridges.ValueAdded(_car.vehicleInstance.id);
                int worthAfter = _cart.IsStock ? 0 : Mathf.RoundToInt((_saved.Spent + Bill()) * GarageCatalogue.Resale);   // as Pay keeps it
                Figure(list, "reallife_tuning_fig_worth", TuningRules.Compare(0f, worthNow, worthAfter, 0.5f),
                    GarageText.Money(0), GarageText.Money(worthNow), GarageText.Money(worthAfter));
            }
            else
            {
                string off = GarageText.T("reallife_tuning_fig_worth_off");
                list.AddNote(GarageText.T("reallife_tuning_fig_worth"), off, list.HeightFor(off.Length, false));
            }
        }

        private static void Figure(TravelList list, string nameKey, FigureChange change, string standard, string now, string after)
        {
            string detail;
            switch (change)
            {
                case FigureChange.Standard: detail = GarageText.T("reallife_tuning_fig_standard", "now", now); break;
                case FigureChange.Kept: detail = GarageText.T("reallife_tuning_fig_kept", "now", now, "standard", standard); break;
                case FigureChange.Changed: detail = GarageText.T("reallife_tuning_fig_changed", "now", now, "after", after, "standard", standard); break;
                default: detail = GarageText.T("reallife_tuning_fig_unknown"); break;
            }
            list.AddNote(GarageText.T(nameKey), detail, list.HeightFor(detail.Length, false));
        }

        private static string Power(float kilowatts)
        {
            return kilowatts < 0f ? string.Empty
                : GarageText.T("reallife_tuning_unit_power", "kw", Mathf.RoundToInt(kilowatts).ToString(), "hp", TuningRules.Horsepower(kilowatts).ToString());
        }

        private static string Unit(string key, float value, string format)
        {
            return value < 0f ? string.Empty : GarageText.T(key, "n", value.ToString(format));
        }

        private static string Ride(float metres)
        {
            int cm = Mathf.RoundToInt(metres * 100f);
            return cm == 0 ? GarageText.T("reallife_tuning_ride_standard")
                : GarageText.T(cm < 0 ? "reallife_tuning_ride_lower" : "reallife_tuning_ride_higher", "n", Mathf.Abs(cm).ToString());
        }

        // Dry and wet grip as one reading, for whether either changed: a part moves either by 0.1 at least, the sum by under 10.
        private static float Grip(GarageFigures f)
        {
            return f.Grip * 10f + f.WetGrip;
        }

        private static string GripText(GarageFigures f)
        {
            return GarageText.T("reallife_tuning_unit_grip", "dry", Mathf.RoundToInt(f.Grip * 100f).ToString(), "wet", Mathf.RoundToInt(f.WetGrip * 100f).ToString());
        }

        private static string Costs(float wear, float insurance)
        {
            return wear <= 0f && insurance <= 0f ? GarageText.T("reallife_tuning_costs_standard")
                : GarageText.T("reallife_tuning_unit_costs", "wear", Mathf.RoundToInt(wear * 100f).ToString(), "insurance", Mathf.RoundToInt(insurance * 100f).ToString());
        }

        /// <summary>What a part does and costs to run, in a line: "acceleration +12 %, top speed +4 %, wears 12 % faster".</summary>

        public static string Effects(PartDef part)
        {
            var text = new System.Text.StringBuilder(96);
            Effect(text, part.Accel, "reallife_garage_fx_accel");
            Effect(text, part.Top, "reallife_garage_fx_top");
            Effect(text, part.Brake, "reallife_garage_fx_brake");
            Effect(text, part.Grip, "reallife_garage_fx_grip");
            Effect(text, part.WetGrip, "reallife_garage_fx_wet");
            Effect(text, part.Stiff, "reallife_garage_fx_stiff");
            Effect(text, part.Mass, "reallife_garage_fx_mass");
            Effect(text, part.Shift, "reallife_tuning_fx_shift");

            if (part.Ride != 0f)
            {
                int cm = Mathf.RoundToInt(part.Ride * 100f);
                Add(text, GarageText.T("reallife_garage_fx_ride", "n", (cm > 0 ? "+" : string.Empty) + cm));
            }
            if (part.Wear > 0f)
            {
                Add(text, GarageText.T("reallife_garage_fx_wear", "n", Mathf.RoundToInt(part.Wear * 100f).ToString()));
            }
            if (part.Insurance > 0f)
            {
                Add(text, GarageText.T("reallife_garage_fx_insurance", "n", Mathf.RoundToInt(part.Insurance * 100f).ToString()));
            }
            if (part.Loud == 1)
            {
                Add(text, GarageText.T("reallife_garage_fx_loud"));
            }
            else if (part.Loud >= 2)
            {
                Add(text, GarageText.T("reallife_garage_fx_illegal"));
            }
            return text.ToString();
        }

        private static void Effect(System.Text.StringBuilder text, float factor, string key)
        {
            if (factor != 1f)
            {
                Add(text, GarageText.T(key, "n", GarageText.Signed(factor)));
            }
        }

        private static void Add(System.Text.StringBuilder text, string piece)
        {
            if (text.Length > 0)
            {
                text.Append(", ");
            }
            text.Append(piece);
        }
    }
}
