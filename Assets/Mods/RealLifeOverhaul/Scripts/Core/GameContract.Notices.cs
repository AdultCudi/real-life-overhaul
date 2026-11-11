namespace RealLifeOverhaul
{
    /// <summary>
    /// The game contract's lines for the kinds of the mod's notifications kept from popping up (Core/GameUi/NoticeMute): its
    /// part and members in this file alone, so another job adds its own file and never edits one line of another's. A part
    /// file's fields are made in their own order and lean on nothing in another part file.
    /// </summary>
    public static partial class GameContract
    {
        public static readonly GamePart NoticeMutePart = ContractBook.Part("notice_mute", "phone", "the switches that keep kinds of the mod's notifications from popping up");

        // The game's toast maker on Notifications.onShow (private; its Start puts it on the event, its OnDestroy takes it off):
        // made again by reflection to be found in the event's list by equality, where the gate stands in its place.
        public static readonly GameMember NoticeMaker = Method(NoticeMutePart, Game, "UI.Notification.NotificationsUI", "ShowNotification", "Void",
            "NotificationType", "String", "Dictionary<String,String>", "Single", "String", "Action", "Boolean", "Boolean");
    }
}
