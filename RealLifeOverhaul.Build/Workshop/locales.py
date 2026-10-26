# Checks every translation beside en.json (Assets/Mods/RealLifeOverhaul/Locales/<language>.json, each named by the code the
# game gives the language in its own StreamingAssets/locale/locale.json: pt.json is Brazilian Portuguese, es.json Spanish).
#
#   python locales.py            check: prints one line per language and "LOCALES OK", or the faults
#   python locales.py --fill     first puts every translation in en.json's order and drops keys en.json no longer has, and
#                                gives every key pt.json or zh-cn.json lacks the English text; then checks. Run it after adding
#                                English texts, so that those two are never short of a key; what it filled is listed as still
#                                English until somebody translates it.
#
# The mod has nine languages (LANGUAGES). en, pt and zh-cn (REQUIRED) must be there with every key. German, French, Spanish,
# Russian, Dutch and Turkish are checked as soon as their file is there, and may lack keys: a language without a file, or a key its file
# lacks, is read in English. The game looks a key up in the player's language and then in its fallback language, English
# (Localizor.LocalizorManager.GetLocalization; LocalizorSettings.fallbackLanguage is "en"), and it reads a mod's
# Locales/<code>.json for both (ContractCheck's LanguageChecks holds the game to this). What they lack is translated later
# from not-in-repo/translate (split.py --missing, then merge.py).
#
# A translation passes when it has no key en.json lacks, its keys are in en.json's order (and, for a required language, are
# all of en.json's), and every text keeps its {placeholders}, its <tags> (balanced as in the English), its line breaks, its
# heading and bullet marks, its "|" separators, its %s/%d formats, its web addresses and a space at either end; no text is
# empty, has a dash or has more exclamation marks than the English; a notification (a key the sources raise with a
# NotificationType, as CatalogueCheck's NoticeCheck finds them) is at most 240 characters; What's new's empty next row is that
# language's NOTHING text, and its fixes are marked with that language's FIXED as often as the English marks them; and a text
# is in the language's own words: Chinese and Russian texts must hold their own script, and a German, French, Spanish,
# Portuguese, Dutch or Turkish text of four words or more may not be the English unchanged, unless its key is on ENGLISH_OK (texts that are only
# names, kept in Latin letters). not-in-repo/translate/merge.py imports this file and refuses a translation by the same rules.
import io
import json
import os
import re
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
MOD = os.path.join(HERE, '..', '..', 'Assets', 'Mods', 'RealLifeOverhaul')
LOC = os.path.join(MOD, 'Locales')
FILL = '--fill' in sys.argv
# The mod's languages: the game's code (the file name), the language's name in English. English is the source.
LANGUAGES = (('en', 'English'), ('pt', 'Brazilian Portuguese'), ('zh-cn', 'Simplified Chinese'), ('de', 'German'),
             ('fr', 'French'), ('es', 'Spanish'), ('ru', 'Russian'), ('nl', 'Dutch'), ('tr', 'Turkish'))
REQUIRED = ('en', 'pt', 'zh-cn')
# What's new's next row when nothing is in it yet; the release scripts (make_version.py, next_join.py, next_dedupe.py) know these.
NOTHING = {'en': 'Nothing yet.', 'pt': 'Nada ainda.', 'zh-cn': '还没有。', 'de': 'Noch nichts.', 'fr': "Rien pour l'instant.",
           'es': 'Nada todavía.', 'ru': 'Пока ничего.', 'nl': 'Nog niets.', 'tr': 'Henüz yok.'}
# What's new: each fix begins with this (the change note and next_dedupe.py split on it). zh-cn's rows before 0.9 have none.
FIXED = {'en': 'Fixed: ', 'pt': 'Corrigido: ', 'zh-cn': '已修复：', 'de': 'Behoben: ', 'fr': 'Corrigé : ', 'es': 'Corregido: ',
         'ru': 'Исправлено: ', 'nl': 'Opgelost: ', 'tr': 'Düzeltildi: '}
CHANGES = re.compile(r'reallife_changes_\d{3}_\d+$')
SCRIPT = {'zh-cn': '[一-鿿]', 'ru': '[А-Яа-яЁё]'}   # a text in these languages must hold their own script
SCRIPT_WORDS = {'zh-cn': 1, 'ru': 3}               # ... once it has this many Latin words (fewer: a name or a unit, as "Mbps")
SAME_WORDS = 4                                     # a Latin-script translation identical to an English text of this many words is untranslated
NOTICE_MOST = 240                                  # characters: a toast holds about 240 (Workshop/STYLE.md, NoticeCheck)
# Texts that may stay as the English in any translation: placeholders alone, version numbers, and invented names kept in Latin letters.
ENGLISH_OK = {
    # 26 Oct 2026, the 0.57.2 language batch: the construction game's firms and investors, two football clubs and a school trust are names
    'reallife_bld_plant_firm', 'reallife_bld_engineer_firm', 'reallife_bld_firm_0', 'reallife_bld_firm_1', 'reallife_bld_merchant_0', 'reallife_bld_merchant_1', 'reallife_bld_merchant_2', 'reallife_bld_investor_0', 'reallife_bld_investor_1', 'reallife_lmk_club_4', 'reallife_lmk_club_5', 'reallife_schools_trust_1',
    'reallife_study_campus_school', 'reallife_work_employer_school', 'reallife_study_campus_university', 'reallife_map_row_university', 'reallife_study_sign', 'reallife_ledger_masthead', 'reallife_ledger_online_name_ledger', 'reallife_ledger_online_name_exchange', 'reallife_ledger_online_name_street', 'reallife_ledger_online_name_review', 'reallife_flare_ledger_name',
    'reallife_lst_test_name', 'reallife_lst_open_way', 'reallife_study_course_mba',
    'reallife-friend', 'reallife_leisure_drinks_choice', 'reallife_social_people_birthday_in', 'reallife_social_with',
    'reallife_travel_toast', 'reallife_travel_say', 'reallife-travel', 'reallife_travel_hotel_garment_hostel',
    'reallife_travel_hotel_industry_hostel', 'reallife_travel_hotel_kitchen_hotel', 'reallife_travel_hotel_downtown_hotel',
    'reallife_travel_hotel_murray_hotel', 'reallife_travel_hotel_midtown_grand', 'reallife_travel_hotel_kitchen_tower',
    'reallife_travel_hotel_hamptons_cottages', 'reallife_career_promo_name', 'reallife_career_promo_boss', 'reallife_career_promo_mate_1',
    'reallife_career_promo_mate_2', 'reallife_career_cleaning_name', 'reallife_career_cleaning_boss', 'reallife_career_cleaning_mate_1',
    'reallife_career_cleaning_mate_2', 'reallife_career_food_boss', 'reallife_career_food_mate_1', 'reallife_career_food_mate_2',
    'reallife_career_retail_name', 'reallife_career_retail_boss', 'reallife_career_retail_mate_1', 'reallife_career_retail_mate_2',
    'reallife_career_courier_name', 'reallife_career_courier_boss', 'reallife_career_courier_mate_1', 'reallife_career_courier_mate_2',
    'reallife_career_cab_name', 'reallife_career_cab_boss', 'reallife_career_cab_mate_1', 'reallife_career_cab_mate_2',
    'reallife_biz_desk_say', 'reallife_justice_raw', 'reallife_justice_chambers_1', 'reallife_justice_chambers_2',
    'reallife_justice_chambers_3', 'reallife_meta_header', 'reallife_civic_page', 'reallife_civic_first_1', 'reallife_civic_first_2',
    'reallife_civic_first_3', 'reallife_civic_first_4', 'reallife_civic_first_5', 'reallife_civic_first_6', 'reallife_civic_first_7',
    'reallife_civic_first_8', 'reallife_civic_first_9', 'reallife_civic_first_10', 'reallife_civic_first_11', 'reallife_civic_first_12',
    'reallife_civic_first_13', 'reallife_civic_first_14', 'reallife_civic_first_15', 'reallife_civic_first_16', 'reallife_civic_first_17',
    'reallife_civic_first_18', 'reallife_civic_first_19', 'reallife_civic_first_20', 'reallife_civic_last_1', 'reallife_civic_last_2',
    'reallife_civic_last_3', 'reallife_civic_last_4', 'reallife_civic_last_5', 'reallife_civic_last_6', 'reallife_civic_last_7',
    'reallife_civic_last_8', 'reallife_civic_last_9', 'reallife_civic_last_10', 'reallife_civic_last_11', 'reallife_civic_last_12',
    'reallife_civic_last_13', 'reallife_civic_last_14', 'reallife_civic_last_15', 'reallife_civic_last_16', 'reallife_civic_last_17',
    'reallife_civic_last_18', 'reallife_civic_last_19', 'reallife_civic_last_20', 'reallife_civic_party_1', 'reallife_civic_party_2',
    'reallife_civic_party_3', 'reallife_civic_party_4', 'reallife_civic_party_5', 'reallife_civic_party_6', 'reallife_civic_party_7',
    'reallife_civic_party_8', 'reallife_civic_contractor_1', 'reallife_civic_contractor_2', 'reallife_civic_contractor_3',
    'reallife_civic_contractor_4', 'reallife_civic_contractor_5', 'reallife_civic_contractor_6', 'reallife_civic_contractor_7',
    'reallife_civic_contractor_8', 'reallife_family_text', 'reallife_creator_tag', 'reallife_changes_001_title',
    'reallife_changes_001_1_title', 'reallife_opt_value', 'reallife_opt_value_percent', 'reallife_changes_001_2_title',
    'reallife_changes_001_3_title', 'reallife_changes_001_4_title', 'reallife_changes_001_5_title', 'reallife_law_desk_toast',
    'reallife_lic_toast', 'reallife_changes_001_6_title', 'reallife-taxi', 'reallife_econ_row_rate', 'reallife_social_text',
    'reallife_social_card_toast', 'reallife_leisure_button', 'reallife_leisure_text', 'reallife_leisure_card_toast', 'reallife_civic_text',
    'reallife_changes_002_0_title', 'reallife_co_row_pay', 'reallife_co_insurer_0', 'reallife_co_insurer_1', 'reallife_co_insurer_2',
    'reallife_co_accountant_0', 'reallife_co_accountant_1', 'reallife_co_accountant_2', 'reallife_changes_002_1_title',
    'reallife_online_row_orders', 'reallife_online_ord_goods', 'reallife_svc_trend_level', 'reallife_svc_buyer_investor_0',
    'reallife_svc_buyer_investor_1', 'reallife_svc_buyer_investor_2', 'reallife_svc_buyer_chain_0', 'reallife_svc_buyer_chain_1',
    'reallife_svc_buyer_chain_2', 'reallife_svc_confirm_sell_text', 'reallife_brand_chain_0', 'reallife_brand_chain_1',
    'reallife_brand_chain_2', 'reallife_changes_002_2_title', 'reallife_opt_text', 'reallife_changes_002_3_title',
    'reallife_changes_002_4_title', 'reallife_util_toast', 'reallife_util_provider_water', 'reallife_net_mbps',
    'reallife_net_provider_aerowave', 'reallife_net_provider_brightvein', 'reallife_net_provider_copperhill',
    'reallife_net_provider_kestrel', 'reallife_bb_percent', 'reallife_bb_share_item', 'reallife_net_provider_sold',
    'reallife_changes_003_0_title', 'reallife_mkt_ticket_status', 'reallife_mkt_lower_rsi', 'reallife_mkt_lower_macd',
    'reallife_mkt_chart_rsi', 'reallife_mkt_chart_macd', 'reallife_changes_003_1_title', 'reallife_changes_003_2_title',
    'reallife_changes_003_3_title', 'reallife_changes_003_4_title', 'reallife_changes_003_5_title', 'reallife_changes_003_6_title',
    'reallife_meta_title_multiplayer', 'reallife_changes_003_7_title', 'reallife_changes_003_8_title', 'reallife_flare_header',
    'reallife_flare_app', 'reallife_flare_hire_percent', 'reallife_changes_003_9_title', 'reallife_changes_004_0_title',
    'reallife_fame_talk_line', 'reallife_fame_percent', 'reallife_changes_004_1_title', 'reallife_changes_004_2_title',
    'reallife_changes_004_3_title', 'reallife_changes_004_4_title', 'reallife_changes_004_5_title', 'reallife_changes_004_6_title',
    'reallife_changes_004_7_title', 'reallife_career_food_name', 'reallife_util_provider_power', 'reallife_campus_why_closed',
    'reallife_newbuildings_street_provinglane', 'reallife_newbuildings_street_birchwoodgreen',
    'reallife_career_builders_name', 'reallife_career_builders_boss', 'reallife_career_builders_mate_1', 'reallife_career_builders_mate_2', 'reallife_career_inspection_boss', 'reallife_career_inspection_mate_1', 'reallife_career_inspection_mate_2', 'reallife_career_architects_name', 'reallife_career_architects_boss', 'reallife_career_architects_mate_1', 'reallife_career_architects_mate_2', 'reallife_trades_tender_rate',   # the building trades' names: proper names, never translated
    'reallife_newbuildings_street_uptown_willow', 'reallife_newbuildings_street_uptown_birch', 'reallife_newbuildings_street_uptown_elm',
    'reallife_newbuildings_street_uptown_maple', 'reallife_newbuildings_street_uptown_cedar', 'reallife_newbuildings_street_uptown_ninth',
    'reallife_newbuildings_street_uptown_eighth', 'reallife_newbuildings_street_uptown_seventh', 'reallife_newbuildings_street_uptown_sixth',
    'reallife_newbuildings_street_uptown_fifth', 'reallife_newbuildings_street_uptown_fourth', 'reallife_newbuildings_street_uptown_third',
    'reallife_newbuildings_street_uptown_second', 'reallife_newbuildings_street_uptown_first', 'reallife_newbuildings_street_uptown_hill',
    'reallife_newbuildings_street_uptown_ridge', 'reallife_newbuildings_street_uptown_grove', 'reallife_newbuildings_street_uptown_park',
    'reallife_newbuildings_street_uptown_lake', 'reallife_newbuildings_street_uptown_uptownavenue',
    'reallife_newbuildings_street_uptown_summitavenue', 'reallife_newbuildings_street_uptown_crestavenue'
}

TAG = re.compile(r'</?[a-zA-Z][^<>]*>')
PLACEHOLDER = re.compile(r'\{[^{}]*\}')
DASH = re.compile('[–—]| - ')   # the style sheet's dashes: none in any language


def marks(v):
    return (sorted(PLACEHOLDER.findall(v)), sorted(TAG.findall(v)), v.count('\n'),
            [re.match(r'\s*(#+ |- |\d+\. )?', line).group(0) for line in v.split('\n')], v.count('|'), v[:1] == ' ', v[-1:] == ' ',
            sorted(re.findall(r'%[0-9.]*[sdfi]', v)), sorted(re.findall(r'https?://\S+', v)))


def balanced(v, paired):
    """True when every tag of a name in paired that v opens is closed, innermost first, and none is closed unopened."""
    stack = []
    for tag in TAG.findall(v):
        name = re.match(r'</?([a-zA-Z]+)', tag).group(1).lower()
        if name not in paired:
            continue
        if tag.startswith('</'):
            if not stack or stack.pop() != name:
                return False
        else:
            stack.append(name)
    return not stack


def latin_words(v):
    return len(re.findall(r'[A-Za-z]{3,}', TAG.sub('', PLACEHOLDER.sub('', v))))


def text_faults(code, key, v, t, notices=()):
    """Why the translation t of the English text v (key) is refused, in language code; an empty list when it passes."""
    if not isinstance(t, str):
        return ['not a text']
    if v.strip() and not t.strip():
        return ['empty']
    bad = []
    a, b = marks(v), marks(t)
    if a[0] != b[0]:
        bad.append('placeholders differ from the English (%s, not %s)' % (' '.join(b[0]) or 'none', ' '.join(a[0]) or 'none'))
    if a[1] != b[1]:
        bad.append('tags differ from the English (%s, not %s)' % (' '.join(b[1]) or 'none', ' '.join(a[1]) or 'none'))
    else:
        paired = {re.match(r'</([a-zA-Z]+)', x).group(1).lower() for x in a[1] if x.startswith('</')}
        if not balanced(t, paired):
            bad.append('tags are not opened and closed as in the English')
    if a[2] != b[2]:
        bad.append('%d line breaks, the English has %d' % (b[2], a[2]))
    elif a[3] != b[3]:
        bad.append('the heading, bullet or numbered marks at the start of a line differ from the English')
    if a[4] != b[4]:
        bad.append('%d "|" separators, the English has %d' % (b[4], a[4]))
    if a[5:7] != b[5:7]:
        bad.append('a space at the start or end differs from the English')
    if a[7] != b[7] or a[8] != b[8]:
        bad.append('the %s/%d formats or web addresses differ from the English')
    if DASH.search(t) and not DASH.search(v):
        bad.append('a dash (an em or en dash, or a hyphen with a space either side) the English does not have')
    if t.count('!') + t.count('！') > v.count('!'):
        bad.append('more exclamation marks than the English has (%d)' % v.count('!'))
    if key in notices and len(t) > NOTICE_MOST:
        bad.append('a notification of %d characters, over the %d a toast holds' % (len(t), NOTICE_MOST))
    if (key == 'reallife_changes_next' or (code != 'zh-cn' and CHANGES.match(key))) and v.count(FIXED['en']) != t.count(FIXED[code]):
        bad.append('%d fixes marked %s, the English marks %d' % (t.count(FIXED[code]), json.dumps(FIXED[code], ensure_ascii=False), v.count(FIXED['en'])))
    if key == 'reallife_changes_next' and v == NOTHING['en'] and t != NOTHING.get(code, t):
        bad.append("What's new's empty next row must be exactly %s" % json.dumps(NOTHING[code], ensure_ascii=False))
    if key not in ENGLISH_OK:
        if code in SCRIPT:
            if latin_words(t) >= SCRIPT_WORDS[code] and (t == v or not re.search(SCRIPT[code], t)):
                bad.append('not translated (no %s letters; if the text is only a name, its key goes on ENGLISH_OK in locales.py)' % dict(LANGUAGES)[code])
        elif t == v and latin_words(v) >= SAME_WORDS:
            bad.append('the English unchanged (if the text is only a name, its key goes on ENGLISH_OK in locales.py)')
    return bad


def file_faults(code, en, there, notices=(), complete=None):
    """Every fault of a whole translation: (key or None, reason). complete: whether every key of en.json must be there
    (by default, for the required languages)."""
    bad = []
    if complete is None:
        complete = code in REQUIRED
    missing = [k for k in en if k not in there]
    extra = [k for k in there if k not in en]
    if missing and complete:
        bad.append((None, '%d key(s) missing: %s' % (len(missing), ', '.join(missing[:8]) + (' ...' if len(missing) > 8 else ''))))
    if extra:
        bad.append((None, '%d key(s) en.json does not have: %s' % (len(extra), ', '.join(extra[:8]) + (' ...' if len(extra) > 8 else ''))))
    if not extra and list(there.keys()) != [k for k in en if k in there]:
        bad.append((None, 'the keys are in another order than en.json'))
    for k, v in en.items():
        if k in there:
            bad.extend((k, reason) for reason in text_faults(code, k, v, there[k], notices))
    return bad


CALL = re.compile(r'\b\w+\s*\(\s*(?:UI\.Notification\.)?NotificationType\.\w+\s*,\s*')
LITERAL = re.compile(r'"(reallife_[a-z0-9_]+)"')


def _argument(text, start):
    i, depth = start, 0
    while i < len(text):
        c = text[i]
        if c == '"':
            i += 1
            while i < len(text) and text[i] != '"':
                i += 2 if text[i] == '\\' else 1
        elif c in '([{':
            depth += 1
        elif c in ')]}':
            if depth == 0:
                break
            depth -= 1
        elif c == ',' and depth == 0:
            break
        i += 1
    return text[start:i]


def notice_keys(mod=MOD):
    """The keys the sources raise as notifications: each call whose first argument is a NotificationType, its second
    argument's literal keys (the scan of CatalogueCheck/NoticeCheck.cs)."""
    keys = set()
    for root, _, files in os.walk(os.path.join(mod, 'Scripts')):
        for name in files:
            if name.endswith('.cs'):
                text = io.open(os.path.join(root, name), encoding='utf-8-sig').read()
                for m in CALL.finditer(text):
                    keys.update(LITERAL.findall(_argument(text, m.end())))
    return keys


def read(path):
    return json.loads(io.open(path, 'rb').read().decode('utf-8-sig'))


def write(path, like, texts):
    """Writes texts as the mod's locales are kept: a BOM, two spaces, one key a line, and like's line endings."""
    nl = '\r\n' if b'\r\n' in like else '\n'
    lines = ['  ' + json.dumps(k, ensure_ascii=False) + ': ' + json.dumps(v, ensure_ascii=False) for k, v in texts.items()]
    io.open(path, 'wb').write(b'\xef\xbb\xbf' + ('{' + nl + (',' + nl).join(lines) + nl + '}' + nl).encode('utf-8'))


def main():
    sys.stdout.reconfigure(encoding='utf-8', errors='replace')
    raw = io.open(os.path.join(LOC, 'en.json'), 'rb').read()
    en = json.loads(raw.decode('utf-8-sig'))
    notices = notice_keys()
    codes = dict(LANGUAGES)
    faults = 0
    if len(notices) < 800:
        print('only %d notification keys were found in the sources (the scan is broken)' % len(notices))
        faults += 1
    for name in sorted(os.listdir(LOC)):
        if name.endswith('.json') and name[:-5] not in codes:
            print('%s: not one of the mod\'s languages (%s); the game reads a file only by its own code for a language' % (name, ', '.join(codes)))
            faults += 1
    for code, language in LANGUAGES[1:]:
        name = code + '.json'
        path = os.path.join(LOC, name)
        if not os.path.exists(path):
            if code in REQUIRED:
                print('%s: missing (%s must be there)' % (name, language))
                faults += 1
            else:
                print('%s: not there yet (%s is read in English)' % (name, language))
            continue
        there = read(path)
        if FILL:
            filled = [k for k in en if k not in there] if code in REQUIRED else []
            dropped = [k for k in there if k not in en]
            kept = {k: there[k] if k in there else en[k] for k in en if k in there or code in REQUIRED}
            if list(kept.items()) != list(there.items()):
                there = kept
                write(path, raw, there)
                print('%s: %d key(s) given the English text, %d dropped: %s' % (name, len(filled), len(dropped), ', '.join(filled[:12]) + (' ...' if len(filled) > 12 else '')))
        bad = file_faults(code, en, there, notices)
        for key, reason in bad[:30]:
            print('%s: %s%s' % (name, key + ': ' if key else '', reason))
        faults += len(bad)
        english = sum(1 for k, v in en.items() if there.get(k) == v and latin_words(v) >= SAME_WORDS)
        lacking = sum(1 for k in en if k not in there)
        print('%s: %d texts, %d still English (four words or more)%s%s' % (name, len(there), english,
              ', %d not translated yet (read in English)' % lacking if lacking and code not in REQUIRED else '', '' if bad else ': PASS'))
    print('LOCALES OK' if faults == 0 else 'LOCALES: %d fault(s)' % faults)
    return 0 if faults == 0 else 1


if __name__ == '__main__':
    sys.exit(main())
