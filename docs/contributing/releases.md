# Versions and releases

The maintainer sets the version and publishes every release; do not change the version number in a pull request. This page is for the maintainer, and for anyone who wants to know how a release is made.

- Each finished fix or feature ships as its own release, built from `main`: the Steam Workshop, a GitHub release with the installer and the zip, and a note on the Discord.
- The installer (`RealLifeOverhaul-<version>-setup.exe`) and the zip are made by `RealLifeOverhaul.Build/Installer/build-installer.ps1`, which builds the mod into a clean folder first and needs Inno Setup 6.
- Publishing to the Workshop is done from inside the game (**Mods**, **Mod Creator**). The texts for its form, the thumbnail script, the gallery pictures and the steps are in `RealLifeOverhaul.Build/Workshop` (start with `UPLOAD.md`).
- What's new: every change adds its line to the locale key `reallife_changes_next` in en, pt and zh-cn. A fix starts `Fixed: ` (`Corrigido: `, `已修复：`); a feature's line has no prefix. At release the next row gets the version's number.

## Version

The version is written in three places and the build stops if they disagree (the `CheckVersion` target): `<Version>` in `RealLifeOverhaul.csproj` (the assembly version, which is what the running mod reports), `Changelog.Latest` in `Scripts/Meta/Changelog.cs`, and `Version` in `ModManifest.asset`.
A new version also gets one row at the top of `Changelog.All` and its two texts in `en.json`.

0.1.0 is the first public release; the numbers the development builds carried (0.13 to 0.46) appear in the git log only.
A save or an options store written by one of those builds is treated as written before 0.1.0 (`SaveTools.DevelopmentVersionKey`, `Welcome.DevelopmentSeenId`).

When the version is bumped: `Changelog.Latest`, and the row `Changelog.Unnumbered` takes the new number.
