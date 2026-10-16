## What this changes

<!-- One or two sentences. Link the issue or the Discord thread where it was talked over. -->

## How it was tested

<!-- What you did in the game and what you saw. If it was not run in the game, say so. -->

## Checklist

See [the rules for new code](https://github.com/AdultCudi/real-life-overhaul/blob/main/docs/contributing/rules.md) for why each of these matters, and [the review checklist](https://github.com/AdultCudi/real-life-overhaul/blob/main/docs/standards/git-and-review.md#rlo-git-007-the-review-checklist) and [the definition of done](https://github.com/AdultCudi/real-life-overhaul/blob/main/docs/standards/git-and-review.md#rlo-git-008-the-definition-of-done) for what the review looks at.

- [ ] The build ends with `0 Warning(s)` and `0 Error(s)`
- [ ] Every checker ends with `ALL CHECKS PASSED`, and `locales.py` with `LOCALES OK`
- [ ] It costs nothing when idle: no allocation, search or text built per frame
- [ ] It fails safe: a fault logs once and switches its feature off
- [ ] A save made with it still loads without the mod (`reallife:` keys, `reallife-` contacts)
- [ ] It is safe in a Going Public session: deterministic, nothing new on the network, nobody else's things changed
- [ ] New texts are in `en.json`, `pt.json` and `zh-cn.json`, at the end, in the house style (`RealLifeOverhaul.Build/Workshop/STYLE.md`), true of the code, with a line in What's new
- [ ] No game files, no decompiled code, no assets I may not share
- [ ] No personal details, and no attribution lines for tools, in the code or the commit messages
- [ ] The version number is unchanged
