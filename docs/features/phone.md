# The phone: folders, the dock and the Banks app

## In a sentence

The phone's home screen works like a real phone's: the mod's apps stand in five folders, two favourites in a dock along the foot of the HUD phone, the player moves apps with a right click, and the three bank apps are one Banks app.

## Where the code is

- Folder: [`Scripts/Core/GameUi`](../../Assets/Mods/RealLifeOverhaul/Scripts/Core/GameUi/), beside the pages it extends (`PhoneGridPager`).
- Read first: `PhoneLayoutRules.cs` (game-free: which app stands where, the view of a grid, the tiles of a page), `PhoneLayout.cs` (game-free: the player's moves and their one line), then `PhoneGridPager.cs` (its "FOLDERS AND A DOCK" part).
- The Banks app: [`Scripts/Economy/Banking/BankApps`](../../Assets/Mods/RealLifeOverhaul/Scripts/Economy/Banking/BankApps/); see [the Banks app](economy-banks.md).

## How it talks to other features

- None of the mod's features knows of the folders. An app is put in a folder by its id (its button's name, `PhoneAppDef.Id`) through `PhoneLayoutRules.Defaults`, the same order as `PagerRules.ModOrder`; a feature that adds an app adds its id to both tables (ContractCheck fails an app with no place).
- A badge an app sets on its button (`PhoneApp.SetBadge`) is read back from the button by the pager, whoever set it.

```
Core/GameUi/
  PhoneLayoutRules  game-free: the five folders and the dock, each app's usual place, the view of a grid (the home
                    screen or a folder's page, and the dock row), the tiles a page holds, the empty tiles before the
                    dock, who may move where, a folder's number
  PhoneLayout       game-free: the player's moves over the usual places, written as one line ("1|id=place|...");
                    a part this version cannot read is kept as it was read
  PhoneLayoutPrefs  the layout on this computer (PlayerPrefs "reallife.phone.layout"), and the word that it changed
  PhoneFolders      the tiles of a grid's folders: a copy of the game's app button for each folder with the small
                    pictures of its apps, the Back tile, the empty tiles that keep the dock on the last row
  PhoneFolderKeys   the game's cancel key closes a folder of the HUD phone (on only while one is open);
                    PhoneAppPress: the right click on an app of the mod's that opens the move list
  PhoneMove         the move list (the game's list window, DeskList), the options page's reset and the two tests
  PhoneGridPager    the pages of both phones, now over the view: the home screen's items or a folder's
```

## What the player sees

- **Folders.** The home screen holds the game's own apps first, in the game's order, then a tile for each folder that has an app in it, in this order: Money (Finance, Banks, Your bank, Stocks, the Newsstand, Utilities), Work and study (Career, Campus, Open Roles), Social and life (the Camera and the Gallery, and the Planner and Flare if moved there), Business (Company, Brands, Services, Ventures, the web shop, Property, the Car Showroom, Tech) and Settings (the Guide and Mod settings). Then the mod's apps the player put on the home screen by themselves, then other mods' buttons as they stand (Going Public's Business, Chat and Hub). The groups are `PagerRules.ModOrder`'s.
- **A folder's tile** is a copy of the game's app button of that phone with the game's scripts that make it an app taken off (as the More apps button is): the folder's name where an app shows its own, a slate tile painted as the game's icons are (`AppIconPainter`) under the small pictures of its first nine apps, three rows of three, and the game's red badge with its apps' numbers added up.
- **A folder's page**: the Back tile first on every page (a light tile with the game's double chevron turned back, the folder's name and "Back" under it; its badge counts the buttons outside the folder with a number up), then the folder's apps in the mod's order, paged as the home screen is (the More apps button on the last tile). Back, the game's cancel key on the HUD phone, or the full menu turning to an app outside the folder go back to the home screen, on the page it was left on.
- **The full menu's top row** has the same folders on its own pager. When the menu opens on an app in a folder, the row shows that folder's page with the app's dot. A row of two tiles (a 4:3 screen) has no room for a Back tile, an app and the More apps button, and keeps the plain pages.
- **The dock** (HUD phone): the grid's last row, two apps, on every page of the home screen; empty tiles (a bare `RectTransform` each) fill the page before it, so the dock stands on the last row however short the page. Its apps start as the Planner and Flare. On the full menu's row the docked apps stand first after the game's own. An empty dock gives its row back. A third app in the dock (a layout from a time an app was off) stands on the home screen.
- **Moving an app**: a right click on an app of the mod's on the HUD phone opens the game's list window (the subway's, `DeskList`) with an entry for each folder, the home screen and the dock; the place it is in is marked and cannot be picked, nor the dock while it is full. A pick moves it on both phones at once and a notice says where to.
- **The layout** is kept on this computer with the game's own settings (PlayerPrefs), as the options are: the same phone in every save, and in a Going Public session each player's own. Only moves away from an app's usual place are kept; an app added in a later version starts in its folder.
- **The option** "App folders on the phone" (`phone_folders`, Phone section, on): off, every tile of the folders goes and the pages are the plain ones of before. "Put every app back in its folder" empties the layout.

## The grid's order

The game's grid lays out what shows in the order it stands. The pager puts every child it counts in the view's order whatever the page (`PhoneGridPager.Order`): the game's own apps as they stand, the Back tile, the view's other items (folder tiles, loose apps, other mods'), the empty tiles, the dock, then the buttons in closed folders in the mod's order; it moves nothing when that is the order already. A button in a closed folder is switched off with the same `PageMark` as a button on another page, so another mod's pager is still seen at once, and the pager steps aside as before, taking every tile of its own away (`DropFolders`).

## The game's parts

- The game's phone has no folders and no dock; it has the app button (its tile, title, picture and badge, `SmartphoneAppButton`, `FullMenuAppButton`), the grid (`GridLayoutGroup`, two columns, four rows) and the row (`HorizontalLayoutGroup`). The folders are made of those alone.
- The game's badge is `UI.Elements.Badge` (`UpdateBadge`: on above 0, "99+" past 99). `SmartphoneUI.UpdateBadgeCount` raises `OnUpdatedBadgeCount`, which the pager already hears.
- The game's cancel key is `PlayerAction.Cancel` (`CancelButtonHandler.HandleEscapeClick`, from `GameManager`). With only the HUD up the game opens its menu on it; with a folder open the key is taken first (`[DefaultExecutionOrder(-31000)]`, `PlayerAction.Cancel.Reset`, as `TravelListKeys` does), and left to the game while anything it closes first is up (a question, the menu, the full menu, the map, the options, an activity's panel, a dialogue, a building's card, a preview, the interior designer, a list window, a field being typed in).

## Cost

Nothing per frame. The pager works on the events it worked on before, and on a folder's tile, a move and the option. The view is one pass over a few dozen buttons (`PhoneLayoutRules.View`, no allocation), the order one more; a folder's small pictures are written only where one changed. The tiles are made once a grid, the first time folders are shown. The cancel key's component is switched on only while a folder of the HUD phone is open: one look at the key a frame then.

## Checks

ContractCheck (`PhoneLayoutChecks.cs`): every app of the mod's has a usual place, a folder or the dock, and the dock holds at most two; every button is in exactly one place (the home screen, the dock or one folder's page) for thousands of layouts, orders and mixes of the game's, the mod's and other mods' buttons, with a folder's tile on the home screen exactly while it has an app; the game's own apps first and in order, other mods' after; the pages' tiles with and without the dock and on a folder's page, the More apps button never on an item's tile; the empty tiles; the layout's line read and written back (moves, an app this version lacks, a later version's folder, a later version's line, the most moves); the option off is the plain pages; the folders' numbers; the timing and allocation; the pager's and the tiles' sources; the texts in the three languages and What's new.

In the game: Options > Tests > "Check the phone's folders" writes `Phone check (folders)` for both phones (every button in exactly one place, for the home screen and every folder), and "Open a folder on the phone" opens the HUD phone's first folder with an app. With diagnostics on, opening or closing a folder writes the same check, and a move writes `Phone check (move)`.

## The Contacts app's groups

The game's Contacts app already filters its list by a row of category buttons (`ContactCategorySelection`: one button for each category some contact is in, with the unread badge of its contacts, a tooltip, and the list below in the game's order: unread first, then by the last message). Its seven categories are the numbers 0 to 6 of `ContactCategoryName`. The mod adds seven more, numbers 101 to 107, and puts each of its contacts in one, so the buttons, badges, "Mark all as read" and the filter are the game's own.

- **The groups** (`ContactGroup`): Family, Friends and people, Work and careers, Services and trades, Money and banks, City and government, Other. A group shows only when a contact is in it. The game's own contacts (stores, banks, employees, rivals, Uncle Fred and the rest) stay in the game's categories.
- **The one registry** is `ContactCatalogue.All` (Phone/Contacts): each entry is a contact's id, its picture and its `Group`. A contact with no group fails CatalogueCheck, as does a contact made by a call the registry does not know. Two kinds have no fixed id and are found by their description: the boss of a job (`reallife_contact_boss_desc`, Work) and somebody met (`reallife_social_desc_...`, Friends). A "reallife-" id not in the catalogue is put in Other at run time.
- **How a contact gets its group.** `ContactGroupsGame.Install` runs at each load after the mod's contacts are made: it adds the seven `ContactCategory` objects to the row's list (`categories`, a line of the game contract, with the game's own `Icon-` pictures), puts the `ContactGroupsRow` component on the row, listens to `ContactsApp.onContactAdded` and sweeps the phone's contacts. A contact added later is put in its group by the same sweep. The sweep also keeps `PlayerDefaults.contactsLastCategoryName` right, or the app would open on an empty list.
- **The row.** One frame after the game builds its buttons, `ContactGroupsRow` writes the count of contacts into each button's tooltip (a second line, `reallife_contacts_group_count`), gives the mod's buttons the colour of the game's own pictures, and, when more buttons stand in the row than its width holds (the game's seven and the mod's seven are fourteen), makes the whole row smaller by the same factor with its width larger, so the footprint is the game's (`ContactGroups.RowNeeds`, `ScaleFor`). It has no Update.
- **The names** are the game's own keys, `contactcategoryname_101` to `_107` (the game asks for `"contactcategoryname_" + category`).
- **Search** looks through every group: while words are typed the list is the whole list, and the category that was shown comes back when the box is emptied (`ContactSearch`).
- **The option** "Groups in the Contacts app" (`phone_contact_groups`, Phone section, on): off, every contact of the mod has the category it had before groups (General; the BizMan desk Business; a boss Employees) and the buttons go at once.
- **The test**: Options > Tests > "Check the Contacts app's groups" opens the Contacts app and writes `Contact groups check (...)` lines: each button lists exactly the contacts of its category, the buttons together list every contact (nothing lost), every contact of the mod is in its registry group, and typing each name in the search box finds that contact.
- **The save.** The mod writes nothing of its own: the category number is the game's field on the contact, and the mod sets it again at every load, so an old save loads and is grouped. Loaded without the mod, a save that kept the numbers lists those contacts under All only (no button holds them); "Prepare this save for removing the mod" removes them as before.
