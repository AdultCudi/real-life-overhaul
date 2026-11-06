# `Scripts/Staffing`

The Staff and roles reference: every business and workplace the mod adds or changes, the roles that run it (how many, what each does to the result, what happens without them) and how each role is had and trained. Generated from the code's own tables, never written by hand. See [the feature page](../../../../../docs/features/staffing.md).

## Read first

- `StaffPlace.cs`: the shapes (a place, a line of its staffing, a role, a sentence and its figures).
- `StaffTable.cs`, `StaffTable.Tech.cs`, `StaffTable.Work.cs`, `StaffTable.Roles.cs`: the table. Every figure is read from a catalogue or a rule when it is made; no text holds a number.
- `StaffEffects.cs`: what a count of people does, worked out by the game's own rules on small made-up firms.
- `StaffPages.cs`, `StaffIndex.cs`: a page from the table, in the Guide's, the game's help's and the Discord guide's mark-up; the A to Z order, the search and the table's checks.
- The window is `Meta/StaffView.cs` and `Meta/StaffApp.cs`; the game's help is `Meta/StaffHelpGame.cs`; the Guide page is `Meta/StaffGuide.cs`.
