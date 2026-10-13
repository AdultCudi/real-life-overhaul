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
# The Chinese file (zh-cn.json) is held to more: a text of three Latin words or more must hold a Chinese character, unless
# it is on the short list ENGLISH_OK of texts that are only names, and %s/%d formats and web addresses must be kept.
import io
import json
import os
import re
import sys

LOC = os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..', 'Assets', 'Mods', 'RealLifeOverhaul', 'Locales')
FILL = '--fill' in sys.argv
STRICT = ('zh-cn.json',)
# Texts that stay as the English in zh-cn.json: placeholders alone, version numbers, and invented names kept in Latin letters.
ENGLISH_OK = {
    'reallife_lst_test_name', 'reallife_lst_open_way',
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
    'reallife_brand_chain_2', 'reallife_changes_002_2_title', 'reallife_opt_text', 'reallife_changes_002_3_title', 'reallife_bank_app',
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
    'reallife_changes_004_7_title'
}


def marks(v):
    return (sorted(re.findall(r'\{[^{}]*\}', v)), sorted(re.findall(r'</?[a-zA-Z][^<>]*>', v)), v.count('\n'),
            [re.match(r'\s*(#+ |- |\d+\. )?', line).group(0) for line in v.split('\n')], v.count('|'), v[:1] == ' ', v[-1:] == ' ',
            sorted(re.findall(r'%[0-9.]*[sdfi]', v)), sorted(re.findall(r'https?://\S+', v)))


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
        if name in STRICT and k not in ENGLISH_OK and (t == v or not re.search(r'[一-鿿]', t)) and len(re.findall(r'[A-Za-z]{3,}', re.sub(r'\{[^{}]*\}|<[^<>]*>', '', t))) >= 1:
            bad.append('%s: not translated into Chinese (or add it to ENGLISH_OK if it is only a name)' % k)
    for line in bad[:30]:
        print('%s: %s' % (name, line))
    faults += len(bad)
    print('%s: %d texts, %d still English (four words or more)%s' % (name, len(there), english, '' if bad else ': PASS'))
print('LOCALES OK' if faults == 0 else 'LOCALES: %d fault(s)' % faults)
sys.exit(0 if faults == 0 else 1)
