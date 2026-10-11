# Checks every translation beside en.json (Assets/Mods/RealLifeOverhaul/Locales/<language>.json, named as the game names its
# own: pt.json is Brazilian Portuguese).
#
#   python locales.py            check: prints one line per language and "LOCALES OK", or the faults
#   python locales.py --fill     first gives every key a translation lacks the English text, in en.json's order, and drops keys
#                                en.json no longer has; then checks. Run it after adding English texts, so that a translation
#                                is never short of a key; what it filled is listed as still English until somebody translates it.
#
# A translation passes when it has exactly en.json's keys in en.json's order and every text keeps its {placeholders}, its
# <tags>, its line breaks, its heading and bullet marks, its "|" separators and a space at either end.
import io
import json
import os
import re
import sys

LOC = os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..', 'Assets', 'Mods', 'RealLifeOverhaul', 'Locales')
FILL = '--fill' in sys.argv


def marks(v):
    return (sorted(re.findall(r'\{[^{}]*\}', v)), sorted(re.findall(r'</?[a-zA-Z][^<>]*>', v)), v.count('\n'),
            [re.match(r'\s*(#+ |- |\d+\. )?', line).group(0) for line in v.split('\n')], v.count('|'), v[:1] == ' ', v[-1:] == ' ')


def write(path, like, texts):
    nl = '\r\n' if b'\r\n' in like else '\n'
    lines = ['  ' + json.dumps(k, ensure_ascii=False) + ': ' + json.dumps(v, ensure_ascii=False) for k, v in texts.items()]
    io.open(path, 'wb').write(b'\xef\xbb\xbf' + ('{' + nl + (',' + nl).join(lines) + nl + '}' + nl).encode('utf-8'))


raw = io.open(os.path.join(LOC, 'en.json'), 'rb').read()
en = json.loads(raw.decode('utf-8-sig'))
faults = 0
for name in sorted(os.listdir(LOC)):
    if not name.endswith('.json') or name == 'en.json':
        continue
    path = os.path.join(LOC, name)
    there = json.loads(io.open(path, 'rb').read().decode('utf-8-sig'))
    if FILL:
        filled = [k for k in en if k not in there]
        dropped = [k for k in there if k not in en]
        if filled or dropped or list(there.keys()) != list(en.keys()):
            there = {k: there.get(k, en[k]) for k in en}
            write(path, raw, there)
            print('%s: %d key(s) given the English text, %d dropped: %s' % (name, len(filled), len(dropped), ', '.join(filled[:12]) + (' ...' if len(filled) > 12 else '')))
    bad = []
    missing = [k for k in en if k not in there]
    extra = [k for k in there if k not in en]
    if missing:
        bad.append('%d key(s) missing (run locales.py --fill): %s' % (len(missing), ', '.join(missing[:8])))
    if extra:
        bad.append('%d key(s) en.json does not have: %s' % (len(extra), ', '.join(extra[:8])))
    if not missing and not extra and list(there.keys()) != list(en.keys()):
        bad.append('the keys are in another order than en.json (run locales.py --fill)')
    english = 0
    for k, v in en.items():
        t = there.get(k)
        if t is None:
            continue
        if marks(v) != marks(t):
            bad.append('%s: placeholders, tags, line breaks, marks or end spaces differ from the English' % k)
        elif v.strip() and not t.strip():
            bad.append('%s: empty' % k)
        english += 1 if t == v and len(re.findall(r'[A-Za-z]{3,}', v)) >= 4 else 0
    for line in bad[:30]:
        print('%s: %s' % (name, line))
    faults += len(bad)
    print('%s: %d texts, %d still English (four words or more)%s' % (name, len(there), english, '' if bad else ': PASS'))
print('LOCALES OK' if faults == 0 else 'LOCALES: %d fault(s)' % faults)
sys.exit(0 if faults == 0 else 1)
