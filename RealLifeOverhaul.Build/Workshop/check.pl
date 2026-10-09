#!/usr/bin/perl
# Mechanical text checks over en.json (line-oriented "key": "value") or plain text files.
# usage: check.pl [-v] file...   (-v lists every hit)
use strict; use warnings; use utf8;
binmode(STDOUT, ':utf8');
my $v = 0; if (@ARGV && $ARGV[0] eq '-v') { $v = 1; shift; }
my @stock = map { qr/$_/i } (
 '\bseamless', '\bimmersi', '\brobust\b', '\belevate', '\bdelve', '\bdive[sd]? in(to)?\b', '\bdiving into\b', '\bwhether you', '\bnot just\b', '\bnot only\b', 'it\'?s worth noting', '\bworth noting\b',
 '\bin order to\b', '\brest assured\b', '\btapestry\b', '\btestament\b', '\bvibrant\b', '\bbustling\b', '\bunleash', '\bunlock the\b', '\bgame[- ]chang', '\bcutting[- ]edge\b', '\bstate[- ]of[- ]the[- ]art\b',
 '\bleverag', '\butili[sz]e', '\bfurthermore\b', '\bmoreover\b', '\badditionally\b', '\bnavigate the\b', '\blandscape of\b', '\brealm\b', '\bjourney of\b', '\bembark', '\bfoster', '\bharness', '\bpivotal\b', '\bcrucial\b',
 '\bmeticulous', '\bintricate', '\bcomprehensive\b', '\bholistic', '\bsynerg', '\bparadigm', '\bempower', '\bstreamline', '\beffortless', '\bstunning\b', '\bbreathtaking', '\bnext[- ]level\b', '\btruly\b', '\bsimply put\b',
 '\bat its core\b', '\bat the end of the day\b', '\bneedless to say\b', '\bit is important to note\b', '\bplease note\b', '\bkeep in mind\b', '\bbear in mind\b', '\bfeel free to\b', '\bdon\'?t hesitate\b',
 '\bawaits\b', '\bbrings? .* to life\b', '\bcomes? alive\b', '\blike never before\b', '\bto the next level\b', '\bexperience the\b', '\benhanc', '\boptimal\b', '\bboasts?\b', '\bplethora\b', '\bmyriad\b', '\bnuance', '\bunderscore',
 '\bshowcas', '\bgroundbreaking', '\brevolution', '\bunparalleled', '\bever[- ]evolving', '\bdynamic(ally)?\b', '\bliving, breathing\b', '\brich(ly)? detailed\b', '\bhassle', '\bpowerful\b', '\bsuper easy\b', '\bin today\'?s\b',
 '\bto ensure\b', '\bensur(e|es|ing) (that|a|the)\b', '\bas an AI\b', '\bI hope this\b', '\bin conclusion\b', '\boverall,', '\bessentially\b', '\bbasically\b', '\bquite literally\b', '\bvarious\b', '\bcurated\b',
 '\bwhole new\b', '\bbrand[- ]new\b', '\bexciting\b', '\bamazing\b', '\bincredibl', '\bfantastic\b', '\bawesome\b', '\bepic\b', '\bultimate\b', '\bunforgettable', '\bfully[- ]fledged', '\bfeature[- ]rich', '\baction[- ]packed',
 '\bworry[- ]free', '\bpeace of mind\b', '\bgo-to\b', '\bmust-have\b', '\bone-stop\b', '\btailored\b', '\bbespoke\b', '\bintuitive', '\buser[- ]friendly', '\bout of the box\b', '\bunder the hood\b', '\bbehind the scenes\b',
 '\bdeep dive\b', '\bdouble[- ]edged', '\bstands as\b', '\bserves as\b', '\bplays a (key|vital|crucial) role\b', '\ba wide (range|variety|array)\b', '\bnotably\b', '\bimportantly\b', '\binterestingly\b', '\bultimately\b',
);
my @jargon = map { qr/$_/i } ('\bmodData\b', '\bnavmesh\b', '\bnavigation mesh\b', '\bprefabs?\b', '\bbridges?\b', '\bstand-?ins?\b', '\bfallbacks?\b', '\bpooled\b', '\bcolliders?\b', '\bclips?\b', '\bdirectors?\b', '\bbundles?\b', '\bagents?\b', '\bkinematic\b', '\bnull\b', '\blogged\b',
 '\brigidbod', '\braycast', '\bcoroutine', '\bGameObject', '\bshader', '\bmesh(es)?\b', '\bLOD\b', '\bspawn', '\bdespawn', '\bNPCs?\b', '\bPlayerPrefs\b', '\bAPI\b', '\bhook(s|ed)?\b', '\bruntime\b', '\binstantiat', '\bplaceholder', '\bskeleton\b', '\brig(ged)?\b', '\bbake[ds]?\b(?! (beans|potato))', '\bpool\b', '\bcached?\b', '\bdebug', '\bsingleton', '\bcallback', '\bframe time\b', '\bdraw calls?\b', '\btexture streaming\b');
my @american = map { qr/$_/ } ('\b[Cc]olor', '\b[Ll]icense[sd]?\b(?! plate)', '\b[Ll]icensing\b', '\b[Cc]urb', '\b[Tt]ires?\b', '\b[Cc]enter', '\b[Nn]eighbor', '\b[Ff]avor', '\b[Hh]onor', '\b[Bb]ehavior', '\b[Gg]ray\b', '\b[Jj]ewelry', '\b[Tt]raveler|[Tt]raveled|[Tt]raveling', '\b[Cc]anceled|[Cc]anceling', '\b[Oo]rganiz', '\b[Rr]ealiz', '\b[Rr]ecogniz', '\b[Aa]pologiz', '\b[Dd]efense\b', '\b[Oo]ffense', '\b[Pp]rogram\b', '\b[Aa]nalyz', '\b[Mm]eters?\b(?! reading)', '\b[Ll]iters?\b', '\b[Tt]heater', '\b[Ss]pecializ', '\b[Aa]luminum', '\b[Pp]ajamas', '\b[Ss]keptic', '\b[Pp]low', '\b[Hh]arbor', '\b[Ll]abor\b', '\b[Rr]umor', '\b[Hh]umor\b', '\b[Ff]ulfill\b', '\b[Ee]nroll\b', '\b[Jj]udgment', '\b[Mm]aneuver', '\b[Cc]ozy', '\b[Aa]irplane', '\b[Ss]idewalk', '\b[Gg]as station', '\b[Gg]asoline', '\b[Ee]levator', '\b[Vv]acation', '\b[Dd]iaper', '\b[Mm]ath\b', '\b[Ss]occer', '\b[Tt]akeout', '\b[Mm]ovie', '\b[Ss]avor', '\b[Vv]apor', '\b[Pp]arlor', '\b[Cc]ataloge?d?\b', '\b[Dd]ialog\b', '\b[Cc]hecks? (for|of) \$', '\b[Ss]tandardiz', '\b[Mm]inimiz', '\b[Mm]aximiz', '\b[Cc]ustomiz', '\b[Ee]mphasiz', '\b[Aa]uthoriz', '\b[Pp]rioritiz', '\b[Ss]ummariz', '\b[Mm]emoriz', '\b[Cc]riticiz', '\b[Ff]inaliz', '\b[Pp]enaliz', '\b[Mm]old\b', '\b[Ss]tory\b(?= building)', '\b[Tt]otaled\b', '\b[Ll]abeled\b', '\b[Mm]odeled\b', '\b[Ff]ueled\b', '\b[Ss]ignaled\b', '\b[Jj]ewelers?\b', '\b[Cc]hili\b', '\b[Ww]oolen\b', '\b[Ss]kill?ful\b(?<!skilful)');
my @gun = map { qr/$_/i } ('\bguns?\b', '\bfirearm', '\bshoot-?out', '\bweapon', '\bpistol', '\brifle', '\bshotgun', '\bammo\b', '\bammunition', '\bshooting range', '\bgunshop', '\bholster', '\barmed\b', '\bshoot(s|ing)?\b', '\bbullet', '\bgunfire', '\bgunman', '\bgunshot', '\bshots? fired\b', '\brevolver', '\bhandgun', '\bconcealed carry');
my @rules = (
 ['01 em dash', sub { $_[0] =~ /\x{2014}/ }], ['02 en dash', sub { $_[0] =~ /\x{2013}/ }],
 ['03 " - " used as a dash', sub { my $t = $_[0]; $t =~ s/(^|\\n)- //g; $t =~ / - / }],
 ['04 "--"', sub { $_[0] =~ /--/ }], ['05 ellipsis character', sub { $_[0] =~ /\x{2026}/ }],
 ['06 three dots', sub { $_[0] =~ /\.\.\./ }], ['07 double space', sub { my $t = $_[0]; $t =~ s/(\\n|^) +//g; $t =~ /\S  +\S/ }],
 ['08 curly quotes', sub { $_[0] =~ /[\x{2018}\x{2019}\x{201C}\x{201D}]/ }], ['09 other non-ASCII', sub { my $t = $_[0]; $t =~ s/[\x{2014}\x{2013}\x{2026}\x{2018}\x{2019}\x{201C}\x{201D}]//g; $t =~ /[^\x00-\x7F]/ ? $& : 0 }],
 ['10 stock word or phrase', sub { for my $r (@stock) { return $& if $_[0] =~ $r } 0 }],
 ['11 internal jargon', sub { for my $r (@jargon) { return $& if $_[0] =~ $r } 0 }],
 ['12 American spelling or word', sub { for my $r (@american) { return $& if $_[0] =~ $r } 0 }],
 ['13 gun words', sub { for my $r (@gun) { return $& if $_[0] =~ $r } 0 }],
 ['14 space before punctuation', sub { $_[0] =~ /\S [,.;!?](\s|$|\\n)/ }],
 ['15 repeated ! or ?', sub { $_[0] =~ /!!|\?\?|!\?|\?!/ }], ['16 leading or trailing space', sub { $_[0] =~ /^ | $/ }],
 ['17 sales-pitch question', sub { $_[0] =~ /\b(Ever wondered|Looking for|Want to|Tired of|Ready to)\b[^?.]*\?/ }],
 ['18 "[{key}]" key hint form', sub { $_[0] =~ /\[\{[a-z_]*key[a-z_]*\}\]/ }],
);
my %count; my %hits;
for my $file (@ARGV) {
  open(my $fh, '<:encoding(UTF-8)', $file) or die "$file: $!";
  my $isjson = $file =~ /\.json$/; my $n = 0;
  while (my $line = <$fh>) {
    $n++; $line =~ s/[\r\n]+$//; $line =~ s/^\x{FEFF}//;
    my ($key, $val);
    if ($isjson) { next unless $line =~ /^\s*"([^"]+)":\s*"(.*)",?\s*$/; ($key, $val) = ($1, $2); $val =~ s/\\"/"/g; }
    else { my $short = $file; $short =~ s/.*[\\\/]//; ($key, $val) = ("$short:$n", $line); }
    for my $rule (@rules) {
      my ($name, $test) = @$rule;
      my $r = $test->($val);
      if ($r) { $count{$name}++; push @{$hits{$name}}, "$key" . ($r ne '1' ? " [$r]" : '') . ($v ? ": " . substr($val, 0, 400) : ''); }
    }
  }
}
for my $rule (@rules) {
  my $name = $rule->[0];
  printf "%-32s %d\n", $name, $count{$name} // 0;
  if ($v && $hits{$name}) { print "    $_\n" for @{$hits{$name}}; }
}
