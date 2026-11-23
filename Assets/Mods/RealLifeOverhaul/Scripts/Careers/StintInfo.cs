using BigAmbitions.DayNightCycle;

namespace RealLifeOverhaul.Careers
{
    /// <summary>
    /// The line under the hours slider of a contract's stint, on the game's work panel at a desk (ContractStint) and at the computer at home (ComputerSitting): the hours picked, the
    /// time they end at if the stint began now, and what that time yields, the part of the contract it leaves or the fee it pays when it finishes the contract. Cost: two texts made
    /// when the player moves the slider, nothing per frame.
    /// </summary>
    internal static class StintInfo
    {
        public static (string key, object arguments) Of(StintSlider slider)
        {
            string time = TimeHelper.Now().AddMinutes(slider.Minutes).GetFormattedTime();
            if (slider.Finishes)
            {
                return ("reallife_own_slider_done", new { hours = slider.Now, time, fee = CareersText.Money(slider.Pays) });
            }
            return ("reallife_own_slider_part", new { hours = slider.Now, time, left = CareersText.Minutes(slider.LeftAfter), fee = CareersText.Money(slider.Pays) });
        }
    }
}
