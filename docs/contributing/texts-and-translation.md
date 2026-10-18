# Texts and translation

Every word the player reads is a locale key. This page says where the texts live, how to add one, how they are written, and how the nine languages fit together.

## Where texts live

`Assets/Mods/RealLifeOverhaul/Locales` has one file per language, named by the code the game gives the language:

| File | Language | Every key? |
| --- | --- | --- |
| `en.json` | English, the source | yes |
| `pt.json` | Brazilian Portuguese | yes |
| `zh-cn.json` | Simplified Chinese | yes |
| `de.json`, `fr.json`, `es.json`, `ru.json`, `nl.json`, `tr.json` | German, French, Spanish, Russian, Dutch, Turkish | may lack keys |

- Each file is flat JSON, one `"reallife_...": "text"` pair a line. The files are UTF-8 with a BOM and CRLF line endings: keep both.
- The game reads a key a language lacks in English, its fallback language. So a new key in en, pt and zh-cn is enough; German, French, Spanish, Russian, Dutch and Turkish are translated in batches.
- Code reads a text by its key, through the feature's `*Text.cs` (for example `JobText.T`), which fills its `{placeholders}`.

## Add a text

1. Add the key at the end of `en.json`, `pt.json` and `zh-cn.json`, in the same order, in the same commit. Edit the files with a script or an editor that keeps the BOM and the line endings.
2. Name it after its feature: `reallife_<feature>_<what>`. Never rename or reorder existing keys. Remove a key only when nothing reads it any more.
3. Write the Portuguese and Chinese yourself if you can. If you cannot, say so in the pull request and the maintainer has them translated.
4. If you change an English text's `{placeholders}`, tags or line breaks, mend the key in `de.json`, `fr.json`, `es.json`, `ru.json`, `nl.json` and `tr.json` too, or take it out of them (it then reads in English until it is translated again).
5. Run the locale check: `python RealLifeOverhaul.Build/Workshop/locales.py` must end with `LOCALES OK`. CatalogueCheck checks that every key the code names exists.
6. Add a line to What's new: the end of `reallife_changes_next` in all three files. A fix starts `Fixed: ` in English, `Corrigido: ` in Portuguese and `已修复：` in Chinese; a feature's line has no prefix. If the row says `Nothing yet.` (`Nada ainda.`, `还没有。`), replace it.

## How texts are written

[The style sheet](../../RealLifeOverhaul.Build/Workshop/STYLE.md) is the rule for every text a player can read, and [the texts standard](../standards/texts.md) holds the rules a change is reviewed against. In short:

- Plain British English, short sentences, no jargon, nothing that describes how the code works.
- No em or en dashes and no spaced hyphen as punctuation. One exclamation mark at most, and only in speech.
- Numbers, money, dates and times through the game's own formatting, never written into a text.
- A text never names a key: it carries `{key}`, which the code fills with the player's own binding.
- A notification is at most 240 characters in English (NoticeCheck and `locales.py` fail it otherwise).
- Every statement must be true of the code. If you change a price, a fine or a key, change the text that names it. The Guide's figures are `{facts}` filled from the code, so they cannot go out of date.

`perl RealLifeOverhaul.Build/Workshop/check.pl -v Assets/Mods/RealLifeOverhaul/Locales/en.json` lists the mechanical faults the style sheet names. The gate fails on any hit that is not in `check-allow.txt` beside it: fix the text, or, where the text is right as it is (a film shoot, a parking meter), add a line there under the reason that fits.

## The Guide's pages

The in-game **Guide** app's pages are each feature's `*Guide.cs` and its locale keys. They are the one source: the Discord server's guide is built from them at each release. A page's text uses `# ` for a heading, `- ` for a bullet and `{fact}` for a figure.

## Words to translate the same way

| English | Chinese |
| --- | --- |
| Standing | 声望 |
| City Ledger | 城市纪事报 |
| Permadeath | 永久死亡 |
| the Guide | 指南 |
