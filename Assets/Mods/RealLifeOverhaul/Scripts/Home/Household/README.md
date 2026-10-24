# `Scripts/Home/Household`

Household staff: a cleaner, a cook, a live-in butler and a mobile mechanic taken on for the player's home through the "Household staff" contact, paid each visit through the game's own money call, seen at work at home, and their work's effect on the home's tidiness, the fridge's portions and the cars at home.

## Read first

- `HouseholdRules.cs`: the four kinds and their hours, wages from the city's going rate, the cook's dishes, the agency's candidates (game-free).
- `HouseholdDirector.cs`: the one component, the hour's round of visits; `HouseholdDirector.Tests.cs` its test tools.
- `HouseholdGame.cs`: the only file that touches the game's cars, the floor and the fridge.
- `HouseholdPhone.cs`: the contact and its call; `HouseholdAtHome.cs`: the character at work at home.

## Documentation

How it works, its rules and its tests: [Household staff](../../../../../../docs/features/household.md).
How the code is laid out, and how features meet: [Architecture](../../../../../../docs/reference/architecture.md).

Maintained by the Real Life Overhaul maintainer.
