using RealLifeOverhaul;

// HotSpots once its names are all given: the names past the slots share "others", a slot of its own, and the last real
// name keeps its number and its name. (Before, the last real slot was renamed "others" and its own time was reported
// under that name.) Run last of all: it fills the session's slots, so every name given after it is "others".
internal static partial class Program
{
    private static void HotSpotsFull()
    {
        int last = -1;
        string lastName = null;
        for (int i = 0; i < HotSpots.Most + 10; i++)
        {
            string name = "Check: Full" + i + ".Update";
            int slot = HotSpots.Slot(name);
            if (slot != HotSpots.Others)
            {
                last = slot;
                lastName = name;
            }
        }
        int more = HotSpots.Slot("Check: OneMore.Update");
        Check(more == HotSpots.Others && HotSpots.Name(HotSpots.Others) == "others", "hot spots: names past the slots share \"others\", a slot of its own");
        Check(last == HotSpots.Others - 1 && HotSpots.Name(last) == lastName && HotSpots.Slot(lastName) == last,
            "hot spots: once full, the last real name keeps its number and its name (" + HotSpots.Name(last) + ")");
    }
}
