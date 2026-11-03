using System;
using System.Text;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// The world's pieces on the RLO interface as the game-free kit knows them (Ui/World runs them over the game): their
    /// ids, the picker's place over the running game, and the words a job offer's schedule is told in. Game-free; checked
    /// offline. Cost: a layout a change.
    /// </summary>
    public static class UiWorld
    {
        public const string PromptId = "world_prompt", BuildingId = "world_building", PickerId = "world_picker", OfferId = "world_job_offer";

        // The smaller windows of the game's over the world, each on its own switch too.
        public const string DriverId = "world_private_driver", ItemsId = "world_items_list", SignId = "world_sign", TerminalId = "world_preview_terminal",
            PreviewId = "world_building_preview", ColourId = "world_colour_picker";

        /// <summary>
        /// The picker over the running game with the status bar and its prompts: centred, a third of the way down the room
        /// above the bar, as every list of the world's stands (custody's too).
        /// </summary>
        public static void PickerLay(UiScene s, UiPickerModel m, UiFrame frame, bool dark, IUiKeys keys)
        {
            s.Begin(PickerId, frame.Width, frame.Height, dark);
            s.Pointer = frame.Pointer;
            s.Floats = true;
            float h = UiPicker.Height(m);
            float x = UiDraw.SnapDown((frame.Width - UiPicker.Width) / 2f);
            float y = UiDraw.SnapDown(Math.Max(UiTokens.S8, (frame.BandY - h) / 3f));
            UiPicker.Draw(s, UiAt.Screen, m, x, y, keys);
            for (int i = 0; i < m.PromptCount; i++)
            {
                s.AddPrompt(m.Prompts[i].Action, m.Prompts[i].Label);
            }
            UiDraw.PromptBar(s, frame, keys);
        }

        /// <summary>
        /// The working days of a week (Monday first) as words: one day by its name, three or more in a row as a run ("Monday
        /// to Friday", <paramref name="run"/> with {from} and {to}), any other set as a list whose last joins with
        /// <paramref name="and"/> ({list} and {last}); <paramref name="none"/> when no day is worked.
        /// </summary>
        public static string Days(bool[] on, string[] names, string run, string and, string none)
        {
            int count = 0, first = -1, last = -1;
            for (int i = 0; i < on.Length && i < names.Length; i++)
            {
                if (!on[i])
                {
                    continue;
                }
                count++;
                first = first < 0 ? i : first;
                last = i;
            }
            if (count == 0)
            {
                return none;
            }
            if (count == 1)
            {
                return names[first];
            }
            if (count > 2 && last - first + 1 == count)
            {
                return run.Replace("{from}", names[first]).Replace("{to}", names[last]);
            }
            var sb = new StringBuilder(64);
            for (int i = first; i < last; i++)
            {
                if (on[i])
                {
                    sb.Append(sb.Length > 0 ? ", " : string.Empty).Append(names[i]);
                }
            }
            return and.Replace("{list}", sb.ToString()).Replace("{last}", names[last]);
        }
    }
}
