# Contributing

Thank you for wanting to help. This page says how a change gets in. To build the mod and find your way round the code, start with the [developer guide](RealLifeOverhaul.Build/README.md).

## Propose a change

1. **Talk to us first.** Say what you want to do on the developer channel of [the Real Life Overhaul Discord](https://discord.gg/tzCh2VXs9E), or open an issue. For anything larger than a small fix this saves you work on something that would not be merged.
2. **Fork the repository** and make a branch for your change.
3. **Make the change, build it and try it in the game.** [Getting started](docs/contributing/getting-started.md) has the steps.
4. **Open a pull request against `main`.** The template asks for the checklist below.
5. **The maintainer reviews it.** Nothing is merged into `main` without the maintainer's approval, and nobody can push to `main` directly.

Small pull requests that do one thing are reviewed fastest. To report a bug or ask for a feature, use the [issue templates](https://github.com/AdultCudi/real-life-overhaul/issues/new/choose).

## The pull request checklist

- [ ] The build ends with `0 Warning(s)` and `0 Error(s)`.
- [ ] Every checker ends with `ALL CHECKS PASSED`, and `locales.py` with `LOCALES OK` ([Checks](docs/contributing/checks.md)).
- [ ] It costs nothing when idle: no allocation, search or text built per frame.
- [ ] It fails safe: a fault logs once and switches its feature off; the game's own screen goes on.
- [ ] A save made with it still loads without the mod: state under `reallife:` keys, contacts under `reallife-` ids.
- [ ] It is safe in a Going Public session: deterministic, nothing new on the network, nobody else's things changed.
- [ ] New texts are in `en.json`, `pt.json` and `zh-cn.json`, at the end, in the [house style](RealLifeOverhaul.Build/Workshop/STYLE.md), with a line in What's new.
- [ ] No game files, decompiled code or assets you have no right to share; where anything third-party came from, and its licence, is said.
- [ ] No personal details, and no attribution lines for tools, in the code or the commit messages.
- [ ] You say what you tried in the game and what you saw. "Not run in the game" is an acceptable answer if it is true.

The full rules, each with why, are in [the rules for new code](docs/contributing/rules.md).

## Models, animations and clothes

The bundles in `Assets/Mods/RealLifeOverhaul/Bundles` are built by the scripts in `PropAssets`, `CityAssets` and `CharacterAssets`. If you change an asset, change its script and rebuild the bundle with that pipeline; each folder's README says how. Do not hand-edit a bundle.

## Versions and releases

The maintainer sets the version and publishes to the Steam Workshop. Do not change the version number in a pull request.

## Conduct

Be civil. Criticise the change, not the person. The maintainer may close issues and pull requests that are abusive or off topic.

## Licence of contributions

By opening a pull request you agree that your contribution is published under the repository's MIT licence.
