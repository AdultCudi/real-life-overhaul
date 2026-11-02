# The HUD host: how a piece goes over the running game

The HUD is on screen for most of the game. It must read at a glance over a bright street and a dark night, never glare,
never take a key the player walks with, and cost nothing while it stands still. Everything that stands over the
running game goes through one host, `HudHost`, on a canvas nested in the game's HUD canvas (`Host/UiOverCanvas`): the
game's scaler and the player's UI zoom scale it as they scale the game's own HUD, and its order sits just above the HUD,
below the game's question boxes, its notices and other mods' canvases.

## Three kinds of piece

1. **The HUD itself** (`HudScreen`, model `UiHudModel`): the status bar and the pieces docked at the screen's edges
   (the left column with an info panel and the objectives, the side panels at the top right, an activity's strip, a
   tool row). Set `HudHost.Instance.Screen.Model`'s pieces, then `Dirty`. It is laid out only when its model or the
   pills' signature changes (`HudStatusGame.Signature`). It takes the mouse and no key. A piece's own commands go to
   `HudScreen.PiecePressed`, which whoever put the piece up answers through the game's own buttons and methods.
2. **World prompts** (`HudHost.Follow(model, point, pressed)`): a box beside a point of the world, moved every frame by
   one projection and never laid out again until its model changes (`HudFollower.Dirty`). The game reads its keys (its
   OverlayUI's PlayerAction buttons); the box shows them (`UiGameKey`) and the mouse presses its rows.
3. **One modal piece** (`HudHost.Modal(screen, title, icon)`, `EndModal()`): a picker, an activity's card, the time skip
   veil, All apps while walking. It runs on a `UiHost`, which takes the keys while it shows and gives them back.

## The status bar

`HudStatusGame.Bar` stands behind `UiChrome.Bar`: the game's time, day, money and the frame's app row (`UiGame.Bar`),
then the pills (the game's speed and pause, its energy, hunger and happiness, the mod's hygiene, stress and home, the
season and its weather, the place as the game's HUD names it). While the HUD shows, every bar carries the app row (the
phone's first home page, then All apps), and only the HUD's own bar makes the pills targets (the pause button,
Topbar.ClickMoney, Topbar.ClickPersona, the game's notice list through NotificationsListUI.Toggle). The money's change rises from its
pill (`UiMoneyFlow`, `UiHudRules.ChangeLift`).

## For the HUD wave

Hide the game's Topbar and speed buttons by leaf through the host library (`Host/README.md`), give each converted piece
its line in `UiScreens.HudScreens.cs` with a switch in `UiScreenSwitches.Hud`, and fill `UiHudModel` from the game's
widgets (mirror, never compute). The pieces' models are game-free; their samples are in `UiSamples.Hud.cs`.
