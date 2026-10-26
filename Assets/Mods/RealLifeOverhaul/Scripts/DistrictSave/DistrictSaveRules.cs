using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace RealLifeOverhaul.DistrictSave
{
    /// <summary>What happens to a kind of record in the save that can name one of the mod's places.</summary>
    public enum SaveWay
    {
        Leaves,          // the whole record leaves the game's list for the mod's section, and comes back
        LeavesFromGame,  // the element leaves a record of the game's own, which stays, and comes back into it
        StandsIn         // the field is written as a stand-in that names the game's own places, and the real one comes back
    }

    /// <summary>Every kind of record the district save handles; each has a code that never changes (<see cref="DistrictSaveRules.Code"/>).</summary>
    public enum SaveKind
    {
        Registration, Job, DistrictStats, Employee, Candidate, Loan, Transaction, Recruitment, Delivery, FurnitureDelivery,
        FoodDelivery, Task, MarketEvent, ForSale, Deed, Negotiation, Installation, Moving, Import, ImporterOrder,
        LogisticsPlan, HeadhunterPlan, HrPlan, PricingPlan, FoodOffer, DeliveryDone, VehicleDelivery, FeeOff, FeePaid,
        InsuranceOffer,
        Campaign, AiEmployee, Poached, BusinessStatement, HomeStatement, EstateStatement, Demand, StorePrice, Destination,
        ImportProduct, Replacement, HrMember, Defended, Message, Shift,
        Player, Guider, Mission, Vehicle, DriverVehicle, ContactAddress, EmployeeSource, EmployeeHrPlan, DriverSlot,
        HeadhunterHrPlan,
        MapFilter   // a ticked row of the city map's filters that is a district of the mod's: leaves the save's list (added after the rest, so no code moves)
    }

    /// <summary>One record taken out of a list, with the place it stood at in that list.</summary>
    public struct Taken<T>
    {
        public int Index;
        public T Item;
    }

    /// <summary>
    /// The district save's rules, game-free (checked in EconomyCheck): whose a place is, what each kind of record does,
    /// and the moves that take records out of the game's lists and put them back where they stood, in their order.
    /// COST: nothing per frame; at a save one pass over each list that can name a place, at a load one per kept record.
    /// </summary>
    public static class DistrictSaveRules
    {
        /// <summary>Every id of the mod's starts with this, then '_', '-' or ':' (as the save tools tell the mod's things).</summary>
        public const string Prefix = "reallife";

        /// <summary>
        /// True for a street's or a district's id that is the mod's own. No game id has this prefix, and none of a future
        /// update's or DLC's can, so a place of the game's own is never taken for the mod's.
        /// </summary>
        public static bool IsModId(string id)
        {
            if (id == null || id.Length <= Prefix.Length || !id.StartsWith(Prefix, StringComparison.Ordinal))
            {
                return false;
            }
            char next = id[Prefix.Length];
            return next == '_' || next == '-' || next == ':';
        }

        private static readonly string[] Codes =
        {
            "reg", "job", "stat", "emp", "cand", "loan", "tx", "recr", "dlv", "furn",
            "food", "todo", "mkt", "sale", "deed", "nego", "inst", "move", "imp", "ord",
            "logi", "head", "hr", "price", "offer", "done", "vdc", "feeoff", "feepaid",
            "ins",
            "camp", "ai", "poach", "stmtb", "stmth", "stmte", "dem", "store", "dest",
            "prod", "repl", "hrid", "def", "msg", "shift",
            "player", "guide", "mission", "veh", "pdv", "contact", "esrc", "ehr", "slot",
            "headhr",
            "filter"
        };

        /// <summary>True when a save has nothing of the mod's to keep out: no feature names a place and the load kept nothing. The save is then left as the game has it.</summary>
        public static bool Idle(PlaceSet places, int kept)
        {
            return (places == null || places.Empty) && kept == 0;
        }

        /// <summary>The game's own line that a save's file is written: "Game " + its temporary file + " saved in " (then the time).</summary>
        public static string WrittenPrefix(string tempPath)
        {
            return string.IsNullOrEmpty(tempPath) ? null : "Game " + tempPath + " saved in ";
        }

        /// <summary>
        /// True for the line the game logs right after it has written the save to its file (SaveGameManager.SerializeSaveGame),
        /// or the line that says it could not: the moment the records go back, while the save call is still running.
        /// Without the prefix (the temporary file unknown), any "Game ... saved in ... ms" line.
        /// </summary>
        public static bool IsWrittenLine(string line, string prefix)
        {
            if (string.IsNullOrEmpty(line))
            {
                return false;
            }
            if (line == "SaveGame serialization failed:" || line == "Save game serialization failed:")
            {
                return true;
            }
            if (!line.StartsWith("Game ", StringComparison.Ordinal))
            {
                return false;
            }
            if (prefix != null)
            {
                return line.StartsWith(prefix, StringComparison.Ordinal);
            }
            return line.IndexOf(" saved in ", StringComparison.Ordinal) > 0 && line.EndsWith(" ms", StringComparison.Ordinal);
        }

        /// <summary>The kind's code in the store; it never changes, so a save written by any version reads in any other.</summary>
        public static string Code(SaveKind kind)
        {
            return Codes[(int)kind];
        }

        /// <summary>The kind a code in the store stands for; false for a code this version does not know (a later one's).</summary>
        public static bool KindOf(string code, out SaveKind kind)
        {
            for (int i = 0; i < Codes.Length; i++)
            {
                if (string.Equals(Codes[i], code, StringComparison.Ordinal))
                {
                    kind = (SaveKind)i;
                    return true;
                }
            }
            kind = default;
            return false;
        }

        /// <summary>How many kinds there are (the codes' table and the enum are the same length).</summary>
        public static int KindCount => Codes.Length;

        /// <summary>What a kind does: a business's records leave with it; the player and the player's cars are never lost.</summary>
        public static SaveWay Way(SaveKind kind)
        {
            if (kind == SaveKind.MapFilter)
            {
                return SaveWay.Leaves;   // an item of the save's own top-level list, put back at its place (it came after the stand-ins)
            }
            if (kind >= SaveKind.Player)
            {
                return SaveWay.StandsIn;
            }
            return kind >= SaveKind.Campaign ? SaveWay.LeavesFromGame : SaveWay.Leaves;
        }

        /// <summary>
        /// True when a record of this kind, taken out for the write, is kept in the mod's section to come back: everything
        /// of the player's own. Another player's records in a Going Public session (Going Public mirrors them into every
        /// world, and mirrors them again) are taken out for the write and put back after it, never kept.
        /// </summary>
        public static bool Kept(bool anotherPlayers)
        {
            return !anotherPlayers;
        }

        /// <summary>
        /// Takes every item <paramref name="mine"/> picks out of a list, keeping each one's index; the rest close up in
        /// their order. Returns how many were taken. The list stays the same object.
        /// </summary>
        public static int TakeOut<T>(List<T> list, Predicate<T> mine, List<Taken<T>> into)
        {
            if (list == null || list.Count == 0)
            {
                return 0;
            }
            int write = 0;
            int count = list.Count;
            for (int read = 0; read < count; read++)
            {
                T item = list[read];
                if (mine(item))
                {
                    into.Add(new Taken<T> { Index = read, Item = item });
                    continue;
                }
                if (write != read)
                {
                    list[write] = item;
                }
                write++;
            }
            int taken = count - write;
            if (taken > 0)
            {
                list.RemoveRange(write, taken);
            }
            return taken;
        }

        /// <summary>
        /// Puts taken items back where they stood: in the order of their indices, each at its index or the end if the list
        /// is shorter now. Into a list left as it was taken from, the list is exactly as it was.
        /// </summary>
        public static void PutBack<T>(List<T> list, List<Taken<T>> taken)
        {
            if (list == null || taken == null || taken.Count == 0)
            {
                return;
            }
            Sort(taken);
            for (int i = 0; i < taken.Count; i++)
            {
                list.Insert(Math.Min(Math.Max(0, taken[i].Index), list.Count), taken[i].Item);
            }
        }

        /// <summary>As the list's <see cref="TakeOut{T}(List{T},Predicate{T},List{Taken{T}})"/>, for a queue; <paramref name="scratch"/> is emptied and used.</summary>
        public static int TakeOut<T>(Queue<T> queue, Predicate<T> mine, List<Taken<T>> into, List<T> scratch)
        {
            if (queue == null || queue.Count == 0)
            {
                return 0;
            }
            scratch.Clear();
            int index = 0;
            int taken = 0;
            while (queue.Count > 0)
            {
                T item = queue.Dequeue();
                if (mine(item))
                {
                    into.Add(new Taken<T> { Index = index, Item = item });
                    taken++;
                }
                else
                {
                    scratch.Add(item);
                }
                index++;
            }
            for (int i = 0; i < scratch.Count; i++)
            {
                queue.Enqueue(scratch[i]);
            }
            scratch.Clear();
            return taken;
        }

        /// <summary>As the list's <see cref="PutBack{T}(List{T},List{Taken{T}})"/>, for a queue: its order is the order it is read in.</summary>
        public static void PutBack<T>(Queue<T> queue, List<Taken<T>> taken, List<T> scratch)
        {
            if (queue == null || taken == null || taken.Count == 0)
            {
                return;
            }
            scratch.Clear();
            while (queue.Count > 0)
            {
                scratch.Add(queue.Dequeue());
            }
            Sort(taken);
            for (int i = 0; i < taken.Count; i++)
            {
                scratch.Insert(Math.Min(Math.Max(0, taken[i].Index), scratch.Count), taken[i].Item);
            }
            for (int i = 0; i < scratch.Count; i++)
            {
                queue.Enqueue(scratch[i]);
            }
            scratch.Clear();
        }

        // Insertion sort by index: the lists are short and already nearly in order.
        private static void Sort<T>(List<Taken<T>> taken)
        {
            for (int i = 1; i < taken.Count; i++)
            {
                Taken<T> t = taken[i];
                int j = i - 1;
                while (j >= 0 && taken[j].Index > t.Index)
                {
                    taken[j + 1] = taken[j];
                    j--;
                }
                taken[j + 1] = t;
            }
        }

        /// <summary>
        /// Text made safe for the store's separators ('^', ';', '|', ',', '#', newlines) and read back exactly by
        /// <see cref="Unescape"/>. Null is written as "~", so that it reads back as null and not as empty.
        /// </summary>
        public static string Escape(string text)
        {
            if (text == null)
            {
                return "~";
            }
            StringBuilder b = null;
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                bool special = c == '%' || c == '^' || c == ';' || c == '|' || c == ',' || c == '#' || c == '~' || c == '\n' || c == '\r';
                if (special && b == null)
                {
                    b = new StringBuilder(text.Length + 8);
                    b.Append(text, 0, i);
                }
                if (b == null)
                {
                    continue;
                }
                if (special)
                {
                    b.Append('%').Append(((int)c).ToString("X2", CultureInfo.InvariantCulture));
                }
                else
                {
                    b.Append(c);
                }
            }
            return b != null ? b.ToString() : text;
        }

        /// <summary>Reads back what <see cref="Escape"/> wrote; a broken escape is kept as it stands.</summary>
        public static string Unescape(string text)
        {
            if (text == "~")
            {
                return null;
            }
            if (string.IsNullOrEmpty(text) || text.IndexOf('%') < 0)
            {
                return text;
            }
            var b = new StringBuilder(text.Length);
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (c == '%' && i + 2 < text.Length
                    && int.TryParse(text.Substring(i + 1, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int code))
                {
                    b.Append((char)code);
                    i += 2;
                    continue;
                }
                b.Append(c);
            }
            return b.ToString();
        }

        /// <summary>An address in the store: its street escaped, '#', its number.</summary>
        public static string Place(string street, int number)
        {
            return Escape(street) + "#" + number.ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>Reads an address the store wrote; false for one it cannot read.</summary>
        public static bool ReadPlace(string text, out string street, out int number)
        {
            street = null;
            number = 0;
            int at = text != null ? text.LastIndexOf('#') : -1;
            if (at < 0 || !int.TryParse(text.Substring(at + 1), NumberStyles.Integer, CultureInfo.InvariantCulture, out number))
            {
                return false;
            }
            street = Unescape(text.Substring(0, at));
            return !string.IsNullOrEmpty(street);
        }

        /// <summary>A turn of <paramref name="degrees"/> about the up axis as a quaternion's y and w (x and z are 0), as Unity's Euler(0, degrees, 0).</summary>
        public static void Yaw(float degrees, out float y, out float w)
        {
            double half = degrees * Math.PI / 360.0;
            y = (float)Math.Sin(half);
            w = (float)Math.Cos(half);
        }

        /// <summary>A float as the store writes it: round-trip, invariant.</summary>
        public static string Number(float value)
        {
            return value.ToString("R", CultureInfo.InvariantCulture);
        }

        /// <summary>Reads a float the store wrote; 0 when it cannot.</summary>
        public static float ReadNumber(string text)
        {
            return float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out float v) ? v : 0f;
        }

        /// <summary>Reads a whole number the store wrote; <paramref name="otherwise"/> when it cannot.</summary>
        public static int ReadWhole(string text, int otherwise)
        {
            return int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int v) ? v : otherwise;
        }
    }
}
