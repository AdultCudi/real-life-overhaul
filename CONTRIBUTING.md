# Contributing

Thank you for wanting to help. This page says how changes get in.

## How it works

1. Fork the repository and make a branch for your change.
2. Make the change, build it and try it in the game.
3. Open a pull request against `main`.
4. The maintainer reviews it. Nothing is merged into `main` without the maintainer's approval, and nobody can push to `main` directly.

Small pull requests that do one thing are reviewed fastest. For anything large, open an issue first and describe what you plan, so you do not spend time on something that will not be merged.

## What a pull request must do

- **Build clean.** `dotnet build RealLifeOverhaul.Build/RealLifeOverhaul.csproj -c Release` ends with `0 Warning(s)` and `0 Error(s)`.
- **Pass the text check.** `CatalogueCheck` ends with `ALL CHECKS PASSED`. See the README for the two commands.
- **Say what you tested.** Tell us what you did in the game and what you saw. "Not run in the game" is an acceptable answer if it is true; say so plainly.
- **Cost nothing when idle.** The mod's first rule is no performance hit. A feature that is not in use must do no more than a timer comparison per frame. No searches, no allocations and no text rebuilt every frame.
- **Fail safe.** Any `Update` that can throw must catch, log once and switch its feature off for the session.
- **Leave the save loadable without the mod.** Keep per-save state under `reallife:` keys and phone contacts under `reallife-` ids, so the removal tool finds them.

The full rules are at the top of [RealLifeOverhaul.Build/README.md](RealLifeOverhaul.Build/README.md).

## Code

- Follow [the engineering standard](docs/standards/README.md): the numbered rules for code, architecture, performance, multiplayer, texts, comments, checks and review.
- Match the code around you: its naming, its comment density and its structure. One folder per feature.
- Features talk to each other through the small optional interfaces in each feature's bridge file, never by reaching into another feature's internals.
- Each feature registers itself in `Scripts/Meta/FeatureList.cs` so it gets a guide page, presets and save clean-up.

## Texts

Every text the player reads is in `Assets/Mods/RealLifeOverhaul/Locales/en.json`.

- Follow [the style sheet](RealLifeOverhaul.Build/Workshop/STYLE.md): plain English, British spelling, no em dashes, no hype, nothing that describes how the code works.
- Every statement must be true of the code. If you change a price, a fine or a key, change the text that mentions it.
- Add new keys at the end of the file. Do not rename existing keys.

## Models, animations and clothes

The bundles in `Assets/Mods/RealLifeOverhaul/Bundles` are built by the scripts in `PropAssets`, `CityAssets` and `CharacterAssets`. If you change an asset, change its script and rebuild the bundle with that pipeline; each folder's README says how. Do not hand-edit a bundle.

## What must not be in a pull request

- Files from the game: its DLLs, its models, its textures, its sounds, or decompiled code.
- Assets you did not make or do not have the right to share. Say where anything third-party came from and under what licence.
- Personal details, or attribution lines for tools in commit messages.

## Versions and releases

The maintainer sets the version and publishes to the Steam Workshop. Do not change the version number in a pull request.

## Conduct

Be civil. Criticise the change, not the person. The maintainer may close issues and pull requests that are abusive or off topic.

## Licence of contributions

By opening a pull request you agree that your contribution is published under the repository's MIT licence.
