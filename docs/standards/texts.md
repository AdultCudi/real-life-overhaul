# Texts

Every word a player reads comes from the locale files in `Assets/Mods/RealLifeOverhaul/Locales`. `RealLifeOverhaul.Build/Workshop/STYLE.md` is the style sheet for how a text reads; these rules cover where texts live and what every change to them must do.

| File | Language | Keys | Rule |
| --- | --- | --- | --- |
| `en.json` | English, the source | 19,748 | Every key |
| `pt.json` | Brazilian Portuguese | 19,748 | Every key of `en.json` |
| `zh-cn.json` | Simplified Chinese | 19,748 | Every key of `en.json` |
| `de.json`, `fr.json`, `es.json`, `ru.json`, `nl.json`, `tr.json`, `pl.json`, `ko.json` | German, French, Spanish, Russian, Dutch, Turkish, Polish, Korean | 19,748 | May lack keys; the game then reads the English |

### RLO-TXT-001: Every player text is a locale key

**MUST.** Put every text a player can read in `en.json` and read it by its key. **MUST NOT** write a player text as a string in C#.

Why: a text in code cannot be translated, and the checkers cannot see it.

Checked by: CatalogueCheck (the keys the catalogues name), review.

### RLO-TXT-002: New keys go at the end

**MUST.** Name a new key `reallife_<feature>_<what>` and add it at the end of `en.json`, `pt.json` and `zh-cn.json`, in the same order, in the same commit, with a script rather than by hand. **MUST NOT** rename, move or reorder an existing key. Remove a key only when its option or text is gone and nothing reads it.

Why: translations and merges are done by key order; a key moved or renamed loses its translations. 19,708 of the 19,748 keys begin `reallife_`; the others are the phone contacts' names (`reallife-<name>`) and business type names the game reads.

Checked by: StandardCheck (against the `en.json` your branch started from), `locales.py` (the same order in every file).

### RLO-TXT-003: English, Portuguese and Chinese in the same change

**MUST.** Write the `pt` and `zh-cn` text of every new or changed English text in the same change. **MUST NOT** add keys to `de.json`, `fr.json`, `es.json`, `ru.json`, `nl.json`, `tr.json`, `pl.json` or `ko.json` by hand; they are translated in batches. If you change an English text's `{placeholders}`, tags or lines, mend the key in those eight files too, or take it out of them.

Why: the two are required languages, and a key missing from either fails `locales.py`.

Checked by: `locales.py`.

### RLO-TXT-004: The files' format

**MUST.** Keep the locale files UTF-8 with a byte order mark, indented by two spaces, one key a line, with each file's line endings as they are.

Why: the game and the translation tools read them so; `.editorconfig` sets it.

Checked by: `locales.py`, `.editorconfig`.

### RLO-TXT-005: British English

**MUST** use British spelling and words: colour, licence (noun), kerb, tyre, metre, pavement, petrol. Names keep the game's spelling: Mom, the subway, "Clothing Store".

Why: the style sheet sets British English, and one spelling throughout reads as one game.

Checked by: `check.pl`, review.

### RLO-TXT-006: No dashes

**MUST NOT** use an em dash, an en dash, `--` or a hyphen with a space either side as punctuation. Rewrite the sentence, or use a colon or a comma.

Why: the style sheet bans them; no English text has one today.

Checked by: StandardCheck (English), `locales.py` (translations).

```
"Fine paid: the car is yours again."     good
"Fine paid - the car is yours again."    bad
```

### RLO-TXT-007: One exclamation mark at most, and only in speech

**MUST NOT** use more than one exclamation mark in a text, or any outside speech: a line a character says or writes, such as a text message or a pitch. Status lines, buttons, prompts, the mod's own notices and the Guide have none.

Why: the mod's voice is plain, and a person may raise their voice where a screen does not. A translation may not add one (`locales.py`).

Checked by: StandardCheck (more than one), `locales.py`, review (speech only).

### RLO-TXT-008: A notification fits in 240 characters

**MUST** keep a notification's English text at or under 240 characters: one or two sentences.

Why: the game's notice shows about that much.

Checked by: CatalogueCheck (NoticeCheck), `locales.py`.

### RLO-TXT-009: The game formats numbers

**MUST** show money, dates, times and distances through the game's own formatting and a `{placeholder}`, never as a number written into the text.

Why: the game shows `$1,250` and `17:00` its own way in every language, and a number in a text goes stale when the code changes.

Checked by: review.

### RLO-TXT-010: Say what the player sees

**MUST.** Describe what the player sees and can do, never how the code does it: no `modData`, prefab, fallback, director, spawn or the other words the style sheet lists. Every price, fine, hour and count in a text MUST be the number in the code.

Why: players read texts, not code, and a text that is wrong about the game is a bug.

Checked by: `check.pl` (jargon), review.

### RLO-TXT-011: A line in What's new

**MUST.** Add one line to `reallife_changes_next` in `en.json`, `pt.json` and `zh-cn.json` for every change a player will notice:

1. Begin a fix with `Fixed: `, `Corrigido: ` and `已修复：`.
2. Give a new feature's line no prefix.
3. Replace "Nothing yet.", "Nada ainda." and "还没有。" if the row says that.
4. **MUST NOT** add a line to a numbered version's row; the version is numbered at release.

Why: the in-game What's new and the release's change note are built from this row.

Checked by: `locales.py` (the markers), review.

### RLO-TXT-012: The Guide is written in the game

**MUST.** Write a feature's help in its in-game Guide page (`*Guide.cs` and its locale keys), with every number as a `{fact}` filled from the code (`Meta/FeatureList.cs`, `.Fact(...)`).

Why: the Guide pages are the one source for every copy of the help, and a `{fact}` cannot go stale.

Checked by: review.
