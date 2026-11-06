namespace RealLifeOverhaul
{
    /// <summary>
    /// The game contract's lines for the start page of BizMan on the RLO interface: the kind's own card of the game, read after the
    /// game filled it, and the game's own question whether a product can be had. A line an update takes away leaves its row out.
    /// </summary>
    public static partial class GameContract
    {
        private const string StartKindCard = "UI.Smartphone.Apps.BizMan.StartBusiness.StartBusinessTypeUI";
        private const string StartSourceEntry = "UI.Smartphone.Apps.BizMan.StartBusiness.StartBusinessInventorySourceEntry";

        public static readonly GameMember KindBusiness = Optional(Field(BizManKitPart, Game, StartKindCard, "_businessType", "BusinessType"));
        public static readonly GameMember KindSourceable = Optional(Method(BizManKitPart, Game, StartKindCard, "IsProductSourceable", "Boolean", "String"));
        public static readonly GameMember KindSourcesBox = Optional(Field(BizManKitPart, Game, StartKindCard, "productSourcesContainer", "GameObject"));
        public static readonly GameMember KindSourceText = Optional(Field(BizManKitPart, Game, StartSourceEntry, "productSourcesText", "TMP_Text"));
        public static readonly GameMember KindSourceOk = Optional(Field(BizManKitPart, Game, StartSourceEntry, "fulfilledIcon", "GameObject"));
    }
}
