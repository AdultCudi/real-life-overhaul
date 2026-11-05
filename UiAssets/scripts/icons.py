"""The interface's icons, from their pinned sources to the outlines the mod fills.

    py -3 -I UiAssets/scripts/icons.py            # Scripts/Ui/UiIconPaths.Data.cs
    py -3 -I UiAssets/scripts/icons.py --check    # fails if the committed outlines differ from the icons

The CS2 direction (1 Nov 2026, design/DIRECTION_CS2.md) gives every section, tab, tool and status a colourful icon with
a meaning, all from one licensed set tinted by category. That set is Phosphor (MIT, LICENSE-Phosphor.txt in UiAssets/fonts,
which the bundle ships with the fonts' licences). Its SVGs are in UiAssets/icons/<weight> as phosphor-icons/core publishes
them, each pinned below by its SHA-256.

Each SVG's shapes are read in document order, one layer each with its opacity: a duotone's faint back shape first, its
outline over it. Curves and arcs (fontTools' path parser turns an arc into cubics) are cut into straight edges that stay
within a quarter unit of the curve on Phosphor's 256-unit box, and every point is rounded to a quarter unit. The outlines
go into Scripts/Ui/UiIconPaths.Data.cs, which UiIconRaster fills at runtime into the white texture UiIcons tints, so an
icon needs no image or font file in the bundle.

Needs Python 3 with fontTools (pip install fonttools). Nothing here touches the game.
"""
import hashlib
import io
import math
import os
import sys
import xml.etree.ElementTree as ET

from fontTools.svgLib.path import parse_path

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.normpath(os.path.join(HERE, "..", ".."))
SOURCES = os.path.join(ROOT, "UiAssets", "icons")
DATA = os.path.join(ROOT, "Assets", "Mods", "RealLifeOverhaul", "Scripts", "Ui", "UiIconPaths.Data.cs")

TOLERANCE = 0.25   # units of the 256-unit box an edge may stray from its curve: a tenth of a texel at 96
QUANTUM = 4        # stored points a unit: quarter units, so the box is 0 to 1024 and fits a short
BOX = 256 * QUANTUM
SVG = "{http://www.w3.org/2000/svg}"
LINE = 180         # columns a generated line of numbers stays within

# phosphor-icons/core at the commit the files were taken from, and each file's SHA-256: a changed file is a new icon, taken
# on purpose. Each row is the UiIcon it draws, Phosphor's name and weight, and the pin, in UiIcon's order.
COMMIT = "2b75f3ad12b420c9504ef05df8d2564a28f8500e"
ICONS = [
    ("Search", "magnifying-glass", "bold", "b73e393b20bff0aaee96b9e325d0276fe1ab5fc81b5080633a95827bd14ebae6"),
    ("Check", "check", "bold", "d0ca4e324ff5bb3a1a3bacb9f7580359b8e03cc6862a614d5ed14458db64bedf"),
    ("Close", "x", "bold", "d540487912a267d83c495954b24ca07981002fda05ee2ea0b492d8fc188d1c3e"),
    ("Back", "caret-left", "bold", "b78b6f532b53b9847340961848cb9e4f5be7e1da07e9dcf11c60cf64aa3986b7"),
    ("Next", "caret-right", "bold", "03cadd956d715541432ec8dc2eda1c53ca341af7d3ccecb8dd32c5b9747e290f"),
    ("Up", "caret-up", "bold", "daf7107329787e8f4fd5b5ccf194cf5ae00a0d44d004421696dad770da06371a"),
    ("Down", "caret-down", "bold", "76a97545e1b923bc13bcc15d7bcbb7f5530105e6eaa98a18c1e30d23e3622843"),
    ("Phone", "phone", "duotone", "9fe2afc9a3e387300640fb9a83aa8bed793882301780ae247cc5cb514cfb730a"),
    ("Message", "chat-circle-text", "duotone", "af780c6315531363c568d9d74c6cecbab33454fec1d70d298ad5995b18da1f10"),
    ("Send", "paper-plane-tilt", "duotone", "a8eb912cee783545493f39946640bf1d93768051addfbab56b040d552e519c52"),
    ("Pin", "push-pin", "duotone", "b558ce57300e6d9108d525d045d63e1de64f575bcdaf4772f7f019f7b12000bc"),
    ("Plus", "plus", "bold", "3d20a4b2e00657baeb922bed94f13fbfa288968b738991d047dd252cb64005d8"),
    ("Minus", "minus", "bold", "f9be4875a49d6bc9931891127335a396beacb420c7076e068abd9e1505348396"),
    ("Person", "user", "duotone", "58708fccb6ecc622a23f9fcd4aae24912c660d8f3d502185693ba562bef85674"),
    ("People", "users-three", "duotone", "8df43f36d7329c365afa0672e2367af16a7bfc7d8820838dfe7358cce1a76aba"),
    ("Briefcase", "briefcase", "duotone", "6d64f87d3b91ce799edcfc5d7e3cf2d058234ac9f52a8f4883c7c29d6bc9069f"),
    ("Building", "buildings", "duotone", "d6843f7a68d132f539effacbfc5151d6a506ee983f1a409ad98a868d8be14713"),
    ("Coin", "coins", "duotone", "23551f8da502c3290481cd0ae01c518628f2ca851689effacfa901e7c54aafad"),
    ("Truck", "truck", "duotone", "278e738442932c789d06c0f7abce9214cb1daa32059b4e36d4e9d86a6097bddc"),
    ("Sofa", "armchair", "duotone", "49150111cdbb0a77f747f79265462973490e2885b684aff9319cea14bf4f0deb"),
    ("Flag", "flag", "duotone", "2b3bb7625aa29bbd03919087199c4d58aeb67dc5e3755a36c31f04a078d364f8"),
    ("Star", "star", "fill", "42451b34121b695bdaab88fdcf3eacec9cda9ac36d222349609426dcb9f04b48"),
    ("Bell", "bell", "duotone", "77f99c9aa7747cc92eae7c225d96e894c40275559e0f40d7ddb7513653a87c19"),
    ("Clock", "clock", "duotone", "2f54d3367b48e1c7579b3fb33963240ee4060bd55ddd4e3c39cd59f840499539"),
    ("Calendar", "calendar-blank", "duotone", "7749f48b233c75868ce25af38c537e4b60a44a87681c5b9b2cde6ed3e89f3acd"),
    ("MapPin", "map-pin", "duotone", "c21ced095b724128b17c25ce60f40e32d98d14339038d818cf79140dc1e04349"),
    ("Info", "info", "duotone", "dec9ea1394c618d6d576ba43e0f08eea265eaf20046e98bc3da19eb4b8da73e8"),
    ("Warning", "warning", "duotone", "5ae0c2cd3e764e0e82f03685657353b65499d6b231745af8516c140e8b1ba4af"),
    ("Trash", "trash", "duotone", "a027137fcada51425999e4305ef8c9baec2328364c092fe94b48fa9eec7adc6b"),
    ("Chart", "chart-bar", "duotone", "7c53ca5599782f50da2591abafd76d04d9fc9388877041911cb97ea585373d2f"),
    ("Heart", "heart", "duotone", "b1bc538e4f7626a2af3b3ac3dca543d8f8e628e9b45991fc7acb63ccaf2c289e"),
    ("Home", "house", "duotone", "bd9f3502ab7d63c3cc8a3cf91657e5974188ede3bd1ac7249333d9ce5d0d932f"),
    ("Store", "storefront", "duotone", "9e755963f1672b9d45a199797cfb3479dd13517690b08f76bb013a08043cc2b3"),
    ("Wrench", "wrench", "duotone", "67c0b89c2688777cc25a14a7f78c39fc5463f7bc5257f1393f46d48b36422716"),
    ("Cap", "graduation-cap", "duotone", "83f2327ca5d41d02b601b41a7d127961890c4495e7dfd1c5ccdd77c5c3eee5f2"),
    ("Bank", "bank", "duotone", "5dffd93d7fcc81e6faed6f69b327506418af5b39aa3c6baa59fbe5afe25804ff"),
    ("Gear", "gear", "duotone", "844f6e041e4622283f651e76882c784af0627f950a6a2536e923b12e4ae5e590"),
    ("Sun", "sun", "duotone", "6a180204ea92f2501de811908395e6dad15ddf2ca0a577f420fad8bef0b4c5ca"),
    ("Moon", "moon", "duotone", "64c8f8b7ba1ad4ae1e892a34ba895744747d9bcd8daaf1d924051c69c4c64650"),
    ("Spark", "sparkle", "duotone", "c4740b4365131487e4b49b54fc554a6cbdde28aaad74c5a8a8be2195b5fb4c19"),
    ("TrendUp", "trend-up", "bold", "eb0256a0f8130f381199b208d7bdc4abb1d82d9ca3cf47c0ec7025321de7ced2"),
    ("TrendDown", "trend-down", "bold", "853b0a6015fe757cb69bc54418d571121452589e66b4004ceacbbd2d5648fd9f"),
    ("StarHalf", "star-half", "fill", "0327b7d0fd6325139b65674e1f7391cb3ffa0de5521a3df2c1f2b11e10615be7"),
    ("Play", "play", "fill", "6d2a75bc5700a68dec50516ebae121c307807e4e6fcca5c863e5b4af006df880"),
    ("Pause", "pause", "fill", "156d3cdfa5cea803caec50b35f49bc635070c8bd84e7fe78df94b025017ccee1"),
    ("FastForward", "fast-forward", "fill", "8be39d806d46384a635c53ffef51e9c19f91fcbb54901fc52c59ee290edbaf8b"),
    ("CloudSun", "cloud-sun", "duotone", "5c3ca930a501926897d6c10acf5dc79c3c6ff9d32c0a292608e1b7df2313c633"),
    ("Wallet", "wallet", "duotone", "b3a8bd8ee9727f61f660cbf35032772c7e8b20e7be0666d436908a1a949ecbd2"),
    ("Lightning", "lightning", "duotone", "aab43bf156d9047ff4002359cecedc867c8e3fecd9d1475635ea4e7b8416267c"),
    ("ForkKnife", "fork-knife", "duotone", "e041753e7555d98ceb8d7f71d7b36fc572190d22440bbded87af3f080fa99e95"),
    ("SmileyHappy", "smiley", "fill", "2f993043ef4566931f5ad81be6d1030734d2dbad5e5d7c5d380debfeaeeb9399"),
    ("SmileyMeh", "smiley-meh", "fill", "0d383b08fec00ade3e4baf926de8fa99a760dd9df02da777c709cbc58f8a9fdb"),
    ("SmileySad", "smiley-sad", "fill", "b7fa4a3bfedc94e03be417f6778bdc60776590496b7fd311abed0351f89b9bf6"),
    ("Thermometer", "thermometer-simple", "duotone", "6ae133a7c725fc30d46ea46bbfd29a9627788a17b04d5ceeec88c630b3192055"),
    ("Locate", "crosshair", "duotone", "0ec929c7d60c416ccdbf7956f817123567ebd4554bcf52cff5acb3e35fbb7623"),
    ("Eye", "eye", "duotone", "695dc8f0c5793b700905ced99225cb6bd891cd49ba5f00eb3eaff744a4bd1cf6"),
    ("AddressBook", "address-book", "duotone", "82423db30fcf53e8e7d076df824489b48ff13a72bde1a8997671512a79b64f34"),
    ("IdCard", "identification-card", "duotone", "bc8cd160e651589885fedac0154c2c900401edb7d69488867b24b08ce5ea60d1"),
    ("Navigation", "navigation-arrow", "duotone", "e8d0e0c6837631ce41e3ed167c354404e049c6bf77a506d6f085929d8a3a1967"),
    ("Handshake", "handshake", "duotone", "4f8827d067f5b777902e4e6e0f1e6ccc6b60c5d34605d65ecb8020137072ebb9"),
    ("Baby", "baby", "duotone", "0bf8b640a70a82cdd31f36aab2f4756429c6b9e72e3d11c006409226a4177522"),
    ("Key", "key", "duotone", "c84fa84d090e51e1a43b2eebf3182c3cfb2c98cf64a2a7fab35b748ee2744c77"),
    ("GenreAction", "sword", "duotone", "cd2fbecfe734ebc1cb0ddc44c02aa0d6a18882f3deab9fbf7a929df499be0c2c"),
    ("GenreAdventure", "compass", "duotone", "00b82ffc59e709c6605a7fe02a0c541c49e2d26c4d58f5dad557ddaeae76e745"),
    ("GenreRpg", "castle-turret", "duotone", "012aeb510fdd98380e1db77f1791216a9dc248f3b8d4815917c557887f43d0eb"),
    ("GenreStrategy", "strategy", "duotone", "9215a7a079cc724467ecbbf401557f76d58dc9b794e47c92fad395d721a7c7fb"),
    ("GenreSimulation", "city", "duotone", "6af86d242c376695fafddbfba28788488e85bd760cb670c49d54ebf21f8a1a4a"),
    ("GenreSports", "soccer-ball", "duotone", "95c2b16b6bbe07f1b484d0abc87b7dc4d218da022466cd874d000089c64ea466"),
    ("GenreRacing", "flag-checkered", "duotone", "290dc65ec4b671444321ad632d761a265bfe9a8daa3d67016f417fea3b38c846"),
    ("GenrePuzzle", "puzzle-piece", "duotone", "21b5ecfdd65a1aaced52daf72e1ac405e4de7c2a1363a9065800a40fb15cb602"),
    ("GenreHorror", "skull", "duotone", "7bc1680496a8815dabef9ad3e7ec24f0668c91ad861b5303403322457e0268f8"),
    ("GenreShooter", "target", "duotone", "f7e6e8cb3ff844db1ac5b65e34257651d7a55f68eb97da574ac369fcc259343f"),
    ("GenreCasual", "dice-five", "duotone", "99d41cb228da4157ecc6a3599a304b86ca4646b217537f26c1b840c8be27d379"),
    ("GenreFamily", "balloon", "duotone", "296586621d30e8c6cb27c3eee3234f3c754c1fd676bb12a0a1c375d374e144c4"),
    ("Code", "code", "duotone", "bafb91fe5a241756d0102490d48d7146c74e81831414a6f6d3792835c5f65ed1"),
    ("PaintBrush", "paint-brush", "duotone", "0b51caafe4ae1a139e7da7bd4051f199fdf466ef9cc2ba6b1a30058eaf93285a"),
    ("Bug", "bug", "duotone", "cdcb8c9b604c6c3d5fa6ff8b31ca8a9520570415cc30035cb39cb7cb9c10d1aa"),
    ("Lightbulb", "lightbulb", "duotone", "498e42a870d662fd36e7dba9b53fc82c3cdbe05452ad0534f3de8033dd07a2a6"),
    ("Headset", "headset", "duotone", "e8251d024d60adba471f395f063fbbb94fa50d2f929813e4f1206536e1f67c83"),
    ("Megaphone", "megaphone", "duotone", "04f73a1727252b5650265cb294d713988c5b353e83714c8674d64376170f678c"),
    ("Database", "database", "duotone", "2543df52f9d509307cd7d08d8bf2ba9033aa23207791c32e2526841ef869ea1c"),
    ("ShieldCheck", "shield-check", "duotone", "e42c34a900022e60b41136a5744989af1cfb977247079afbfaf5a8440b86fd91"),
    ("HardDrives", "hard-drives", "duotone", "ded508a98776d1f04adf05371d1f1fc9e8be8ddcf3a221e7d749af5f486dd452"),
    ("Presentation", "presentation-chart", "duotone", "4ad46ec6414bcccda58f70b9819d2467536519a107f94e6b98022027721a532f"),
    ("Grid", "squares-four", "duotone", "08872949367b6a6f2f0578ff77364f6c8ebb407942629a28b322194512927381"),
    ("GameController", "game-controller", "duotone", "0ea6d7fd6dc328b3e2e1041b3575639e61a9546082553c0f4ca6101ee8f9f6b3"),
    ("ChartLineUp", "chart-line-up", "duotone", "86b5bfdff2eb2bc6fc8545a71950aef4185356ee9bde18191d543fee97ff4e30"),
    ("ChartPie", "chart-pie-slice", "duotone", "17cc2863e8f8c219ed0e854a62cdbb31ebfa12a71cf7e462f2e3ed53ef029f4d"),
    ("Trophy", "trophy", "duotone", "3e9f32441fc0315f359dbab71322d9e069cada283527918360d6789a84c7bf14"),
    ("Rocket", "rocket-launch", "duotone", "d4caccf2aa11c37305809a59aa6727385b8ad04e35b14e0f6f8e1c47c99e0b13"),
    ("Package", "package", "duotone", "9665a11c41ebab91ba1e36a7a8934dbc9ea30b45d3e4f011c8f63c5ca8e5352b"),
    ("IdBadge", "identification-badge", "duotone", "241e6d5808758ddfd480a9588f71e6991f4ee224dcf4da5ff01c403e760da0e0"),
    ("Camera", "camera", "duotone", "16aab74ac3605daf0b2b55f2eb5cea741f42a0a49894e4fd1c1211bfeb7e1f16"),
    ("Image", "image", "duotone", "32c9e73a6a35d9a34649c21549737b7d129dc191836d93975d921f41184edb14"),
    ("MusicNotes", "music-notes", "duotone", "3bef98ba3b52c89aa02fa9e77d76aae93cebe71f116afa1eb5df57ff21d17d02"),
    ("Newspaper", "newspaper", "duotone", "dfca2a911dee7a43b71df373f18799fddb2f143475b5ac15229bfd21786e97cb"),
    ("MapTrifold", "map-trifold", "duotone", "51fc6df1cb4a67fcf8ec828735f3d2eb163c14d998b2bd34b1507782bf40bb5c"),
    ("ShoppingCart", "shopping-cart", "duotone", "3d96cebc7e046dc071d2fe56e94d54d3784fe084e58e7f456857f1179320d76f"),
    ("ShoppingBag", "shopping-bag", "duotone", "7c8666c1b779180724ed0536b926b8358e8cec579a91670fc8c3718b25e86776"),
    ("Cpu", "cpu", "duotone", "90317b07d6f560aebf37e2aa29fa5750f3f4f959797c610054cc750bbbebab9c"),
    ("Fire", "fire", "duotone", "f1346a9ca22fbf7a384920ffa9991549f0e37de320dd7425de705bcfe5b9c980"),
    ("Heartbeat", "heartbeat", "duotone", "8d6d067a060af5d379c75a8b51227304aff9ed076b95761f0c9aa4db610da748"),
    ("Car", "car", "duotone", "e479dc66d99f4f4b2b478250212237a0bbc46b6d693ff58c6da3072b37d33307"),
    ("Hammer", "hammer", "duotone", "810a65ab7d44732714b3eced6fb39f6e3391a061ef8abe5d9325376b33acc8d4"),
    ("Scales", "scales", "duotone", "e31256959626a331a7949762141f67020126347e9421c66f04ed696b008002b6"),
    ("Globe", "globe", "duotone", "b9fd5ea9c984f786b47ac59f3aa913e3748d40f45f8eb212874718adc38e02b4"),
    ("Envelope", "envelope-simple", "duotone", "8cd858097658d41520072e3232a85185b697828751bb691bd54cd4960fc2d574"),
    ("Ear", "ear", "duotone", "dc0f31f132022269b4f603fd8f5df2dc4e9a6b8a9157757a751cf00138fe4ff6"),
    ("Factory", "factory", "duotone", "39141f1b2caca5afef2dc243ec0c5c97c3243a58d7f9fd7d5537d0e2e1a55139"),
    ("Coffee", "coffee", "duotone", "f8285e75660c4ed9830ed42416bf3a839cb52c30a3bf2f19ba303e9ed6fb05a1"),
    ("Receipt", "receipt", "duotone", "48bd25a0c53d8c5e49105a4208fd1297d30d89024eb04c5825983433fe256cc6"),
    ("PiggyBank", "piggy-bank", "duotone", "7e7cd1997ce99297e65565a1f02255654dd0818d071d7f3b9771a55c7c9e0371"),
    ("Tag", "tag", "duotone", "3d1cc09e77869fd52efab0633cfb5ad050632f5f1c1ed1851298915ae3d80909"),
    ("Percent", "percent", "duotone", "fd52fb7203e16246355a64685fc1c570f2df8a0937dd1b282c67bc8ec6379a33"),
    ("Drop", "drop", "duotone", "b263461c657b14c1c9c1e845e05e71d73afac85260c23bfe34b28901788b3286"),
    ("Brain", "brain", "duotone", "21c4456e71486b819599cc5feecde1451dc172c82f40ae10f8469f434567979b"),
    ("Cloud", "cloud", "duotone", "d1a4338eccb1f8ea3353303980f15859a0a1bbc6af9d1546613182705a9b5c16"),
    ("CloudRain", "cloud-rain", "duotone", "b96c8f795c1c4964cb5331fd2d89a2a4d3d6f3148de1541539c87f0f19a02f93"),
    ("CloudSnow", "cloud-snow", "duotone", "538d41a27d912d5a133d0b9b5073b807fe905f11cb51987b4baf4fb5a929df61"),
    ("SkipForward", "skip-forward", "duotone", "a76c0760fa058cbfbf9a1b7da7fb7fb43145dee3bfc24c4c381d1497bdf86e2c"),
    ("Train", "train", "duotone", "a29072c2bb72e15f7f05327e298cdedacddbcf7dc2b20d9ab11234deae4dd1fc"),
    ("ListChecks", "list-checks", "duotone", "aa71967b96fc44f2e40fabf3b297e333925c994cc22a2da2177980b7eea5a55e"),
    ("ClipboardText", "clipboard-text", "duotone", "d1e13568193ac0b89bef7b92cf351d9253f850480ccc1a52faf9061114bc8aac"),
    ("Ring", "circle", "bold", "98864523b27ed74ae7792549a77adfe7ed459238385bbb50fe77c9f92c16bb7f"),
    ("FloppyDisk", "floppy-disk", "duotone", "f71f3cd35caa70eb63a6e12fb50b0ac33051f2889cc010f7423ee8342a0d36a4"),
    ("Power", "power", "duotone", "663fcfcebf142cc92bf7e0d89bbb877349431dfa8761cbadbe12708d7d0c5406"),
    ("SignOut", "sign-out", "duotone", "4a44a628329ed8d4e17167610e27f289f1e068b27db1a5e8284415b0c245a6cf"),
    ("Pencil", "pencil-simple", "duotone", "03fa15e242468e14db0f279ddfc308bbb5c195af5b306680d37c4e26a86bed9a"),
    ("Layout", "layout", "duotone", "f8d0876ac047847e913ad4de06f2c44a88bdc7b583538808757b47857f87af62"),
    ("Monitor", "monitor", "duotone", "93d85c4c8cc8014c2a83ff4c0ac176166c99f12b5948d22807aacfd3925c6088"),
    ("SpeakerHigh", "speaker-high", "duotone", "df9b42e5af71a3f87a7588397ba9ad73f70133cbc1352e3f916ac8c2f380207b"),
    ("Keyboard", "keyboard", "duotone", "4484955d19ca1e9054e2b9faf9085c34dd8292d18dd01090c6873205d0817b1b"),
    ("Sliders", "sliders-horizontal", "duotone", "76260b62780cba20278d8c45eb9107801090414b0fbb8a6b67c97860664d7237"),
    ("Cube", "cube", "duotone", "5a4833e706e0d7431e607b930ce22af06fcb8ee29ff05da50cb12701a8e64e5b"),
    ("Lock", "lock", "duotone", "08412a11cc0ebb4d842904f2930ebdf66d12439e95bfe6c5a7174505c0280149"),
    ("Reset", "arrow-counter-clockwise", "duotone", "7f8253410467897776eff66017822f3b9b44f15a317d9d19b9a0ba07e277118e"),
    ("Upload", "upload-simple", "duotone", "f95c3c5150c3c15e27828fd1600cc8b00a60c236b7c911da89941da4bfdb336e"),
    ("Books", "books", "duotone", "7898d95403db718d42cd3e4d8b9785a2c295076d500d4adbc174bd817167a303"),
    ("Download", "download-simple", "duotone", "68ffe027868f3e33b2788d5cc246dbcb47a86f210b39a2866ab7e2d1cddee9a4"),
    ("ToolCursor", "crosshair", "bold", "cb8556097f1484cbd969a2184cdd053ba797e6bfab1d26af4a45c80c2a3d4736"),
    ("ToolTrend", "line-segment", "bold", "d42385d3eeccac9bb1b6848ad6ba7ec3bdaf3d65cb3b6f352d750411a8951ea4"),
    ("ToolLevel", "arrows-out-line-horizontal", "bold", "278ff4e8fd05a855e973ef49b82af25ddbe38b4dbea0f9b919e28e2c17873248"),
    ("ToolFib", "text-align-justify", "bold", "836ca30495096d188203a0c5ba65132b1e3f9c7bb11633a9b413d211e14661b3"),
    ("ToolRay", "arrow-line-right", "bold", "bc729d7f66840096625de8809e5b879c8f23938338839190a62d528f6d0b9996"),
    ("ToolRect", "rectangle", "bold", "145ee233dcc88fd083bc3fb19d4d2c3435143b4afe72ef900f41dc2877f7dcab"),
    ("ToolMeasure", "ruler", "bold", "af0be341c9df709e4e5df249eda88c773dbf6b068145e42d78c0cb31ca66a6ab"),
    ("ToolText", "text-t", "bold", "ca48a422f871e674081c74d6b29fdfd04b7aa86604e0acdab64387146bc7124b"),
    ("ToolSnap", "magnet", "bold", "8f2b700696fa755437dfa1a6458a63ed89c337deffd715855800ebe8132ee59b"),
    ("Expand", "arrows-out", "bold", "0ca920a5995da8e36ac9f744766f95adf63c781968b6f989539a95e93060c62d"),
    ("Shrink", "arrows-in", "bold", "033baba7ff0a4f380be329c28a64f2d435cc85732505950e9e21c6b6818c8014"),
    ("Timer", "timer", "duotone", "fda04f76dea917e8196fabe509c0fe283ab0d4edca4001a0f965901905e4f0c5"),
    ("Crop", "crop", "duotone", "c984b4135e3ecf587146c75ee3c4d54abb565241a655c14f314d9ba519a4494f"),
    ("Rotate", "arrow-clockwise", "duotone", "66db0c380b84dcb949ab83e2670443c012c72e73c59197e1173cc9abc3abc781"),
    ("Exposure", "plus-minus", "duotone", "10d1a1fe7c422d59fb0f16444344e45336083e206f607b52a10bdbe1d594cc1d"),
    ("Level", "align-center-horizontal-simple", "duotone", "96c4f770af24c3df114a727831dc3b10f3b2bd75ea6d7c7d2f32a52bbaa04692"),
    ("FlipCamera", "camera-rotate", "duotone", "00a393c9766ed671dc5dfd7641892f25ea4cfcc2bbfa0022fd709e2a8046b2f7"),
    ("EyeSlash", "eye-slash", "duotone", "7f411a87d8cd961217d0a15ee31dba79bf00d5396585092ae0e0e919d317a534"),
    ("FrameCorners", "frame-corners", "duotone", "191a46ae6739f685a87b18d2a39d5115d5c812a3b5a3fd01257de19f5905566a"),
    ("Funnel", "funnel", "duotone", "ba1eb55b4d6abd28131ac98dc5269957291f76d09ed3de4eef78c21c8cf73bb7"),
    ("PortraitFocus", "user-focus", "duotone", "7e0815457e65da664500eb64b4a38b5f6bfcfd3ddc7b02728bf83a423911bfd8"),
    ("Walk", "person-simple-walk", "duotone", "26ab881dea91b7484cd115af1abd38448f26790fb5045538fb31f762384c0be7"),
    ("Taxi", "taxi", "duotone", "26efeae2bd5ff23693ee20b011d3029375c180346f5f569d6176a0906bf542b7"),
    ("Bookmark", "bookmark-simple", "duotone", "b560161da8079c67c381dcfd1ae69b39e8eed3815370cf2adc6ba4b3bea04aff"),
]

# Phosphor's fill weight of every duotone icon, its second variant: the chunky solid glyph a toolbar's tile asks for
# (UiIcons.Get with filled). Same commit, same name as the icon's row, each file's SHA-256.
FILLS = {
    "Phone": "4546c9d7d0a26f08ec5a35f2ca3e12d8ef76767ba60c2effa6f214d7f070dcb6",
    "Message": "151178dbebb30d17ebc6b36878213426a9e7f66fb05b275ddfbc259f8cbe385d",
    "Send": "ed177b450c32042258e3d80f002c3659a60c0b21fd0bc784245525d070301dfc",
    "Pin": "b3a7a363401c6e09ccd0fc5a7f6813c3183dab69b07eaca3419acf2fafe942b3",
    "Person": "9e011a83732426181c4e18ebfe0322635ed7a222ccd572072b1233a91f88fd0b",
    "People": "aca109558096eca83a60ded74a65b5ab9d341857f0c76f7b89bf93e3bd6df971",
    "Briefcase": "da09cff8a2bdb1c75b756b14f999e876d1301338a271e9e147a1039db18b3011",
    "Building": "887859507bd2d8e88416f359c5bd42ca23ebb63141a71c3276fca8e31d3cf252",
    "Coin": "8eaf954f6a6ad2b0abb0862093ddb7a18e240be1a52efe2729bb44676f4b2315",
    "Truck": "90a19738e1b64bb697d0f6d1fc9a506d3d5325c2b173ca08427ba2093bc7d89a",
    "Sofa": "6574e020ec65dd629865953e6d49b9287e57a18e6e769d66ebfd4e78290c99f0",
    "Flag": "fe8b8db7361da173e7c053c6854dafffe9f638d919e2b703e68b5bdf7accc66c",
    "Bell": "26437565edc7418a2c326a74078fb4519aa2316e84fcf1adefbdd1b2c89f4ac5",
    "Clock": "4abe98cfc61350de60baf70ce93e85b3a9acbb80e34f9cf19688661aaddbc140",
    "Calendar": "405596825bf3705baa7d289ed1242106fded80e34967927549a0413f4303a0ac",
    "MapPin": "0480a057e6ae95fc2176e33564b1ea6d6b6e31e2398bd9e789f7097baf3315df",
    "Info": "8240b810f7a3b076271a829dff61c7dd64257a781c30f29c938f88835f11136a",
    "Warning": "90595f8a478ac1f156871ac8d8a35c7c4e372b8d101dad9aeb1bceadf9a2c7f0",
    "Trash": "f78767cc15e1a7d6eea49c4efb515cf6fceaf07fbc421e8ce18373d07c14b673",
    "Chart": "a417cc075c304374043e7d47f7622606b15f14e4c65663fd7a8a9898d768362a",
    "Heart": "d90560d4f762f1b345bd2322cd1ef696926ffa74b0c1396933b6d926faf8341e",
    "Home": "4783cdf0ae5f5ec363df6f77167a41642aac42fe8d73251b9a61232ab43f355d",
    "Store": "c4f89c48138eb0e7269cd8286bb5774135cd5cfd6ea10c588c8c62591353dd83",
    "Wrench": "a04703c10ffb2adaae16d1e434c76cd013938027d74adc5d80c66ab8eae9c61d",
    "Cap": "0324f19528344327607dd2ee731d1313f4bb7d40086c81e5c5305c8214a39975",
    "Bank": "2c223fc8f62ed7df0f45cdfe47808dda2833a01c23402b134684a3de3857b85f",
    "Gear": "564f53f72b0bd4371dc14a3d39a3fcf4c5a1a74d285eb56fff465a954f993ec8",
    "Sun": "343fb225b7f7dddd7b6b80e1cec533cf7839da96e0513c6da40e88dd24306154",
    "Moon": "099574dba1662cf9cdff50a1859ae8cc605a9b6a10bace00d89a0f52205e76df",
    "Spark": "9872a9255717f4ecc0f01b028b9436042e0f49391e4ce7a57f61aa466684fad1",
    "CloudSun": "6d9f3caa7031ffb5847c358a6743fd7536f371ef0c4e40ef3bdcf3617d28255d",
    "Wallet": "e419d13382b85966f1261978c5d6264589d837afd7f40329dfb21e9295c2a888",
    "Lightning": "024d190cc1ce86eb59184eb7a598604ad28946248243fbc0cf07b9887babf88d",
    "ForkKnife": "ff917789bb287fb51e80f346f5bf5cf96e2c4cf56d2637c4fe1d0b1238694024",
    "Thermometer": "5d5d33781c5df0827211acd9d5e3231927db953723f74dbb537cda2ff2cda4d5",
    "Locate": "27084169d0c34bc4d1365e2867a76112062f5164fd58378ff39006212eabe8a6",
    "Eye": "86381b51cb6f305019503b4c5d4b02fe8df5941bab3f1475ee873006a8708420",
    "AddressBook": "6bd364522cacf08cdb8eab8db2b8a02e123433da84baca956eadbdcc65e38a00",
    "IdCard": "1ab3b4b94b739bdfb871cdae5455be56e0ad59639d3f3b23f908858a5e0c7b69",
    "Navigation": "49ebafa744283494e96e3b507cd3bcd1e3bf1574fcbc9869d2ec361056e52eb9",
    "Handshake": "d9fc30d1f2431296e695a99dfac569aef54785fa32819ca23d960e4374b603b5",
    "Baby": "33e21a6a72fa6394f53264d5f6eaa97860b9aaeb36c7aea63e3df3708f29bec5",
    "Key": "25be8ade7785508b97dc19ed87ae952351d7cfe20a5fbdbe9655df2fe6af4e4a",
    "GenreAction": "39d95d1eec8ba2fb846298015c330a7d10dd85fd39b6e400774646788afc3fb2",
    "GenreAdventure": "9663ec833384ba767b617ba3c256a3e00a23bd666810e2f2214171896498b6dc",
    "GenreRpg": "f6e506372e87364bb58dfeec8cb599895b2adf3d2149cb3b5f0999bfe62d8781",
    "GenreStrategy": "4184e07159a4fa109f0b9e87f57fd2517167f0831cfac8ba6ecd64edf6d0cb85",
    "GenreSimulation": "44debfb12c2870ead7c71655d021375a7b74563f86bd4c68ba831961dfa47381",
    "GenreSports": "0be7b183aec422483bbac41794eccd7411b9b00efb0dca98912a43a4192e6a2b",
    "GenreRacing": "d08b7eec6bca94d316bd003ac72c72c18a0ac5997d2648d7011f767314db1e7f",
    "GenrePuzzle": "1067dab98fff3cdaf27b39f0ebf6e0caa9de0500e72b1655ba932d3e17f06e6d",
    "GenreHorror": "1a1eab22a03268af2f34ab05aab19eee5b01e739e2df3fedca1ff683f3fcc3fd",
    "GenreShooter": "bd73f0e44c0df18febb0ac9231b2b51a354e0199a39996d773397b9cd283a7cb",
    "GenreCasual": "2b03cd50e0c21464a5b30a88f3900900e6636d9ee6191414a892fc5975b6b8f3",
    "GenreFamily": "15137f7538d65dfffcbd0d13210567f5d54464fa4b6cb0d86622e63aee1972ba",
    "Code": "51cc35968a2f0960da2d7555e5b80925a0fcece8b277f816f549211482da10cb",
    "PaintBrush": "182a006d86888c3c40f5c9b1707fe0b3e6e6ea3c4c9a12210e6113d182c1a588",
    "Bug": "91e955a8df4bdf30dc335e12efba12db482bf112a0609c3918c10872d3c1994b",
    "Lightbulb": "31539e7dee97567a4fa3e5c8c8830cf4e23a1050d073b6f871ebb07d1694e664",
    "Headset": "2a9eb5ea36f879947c7222ceddb84b66b89d91340c580edcfa51662f8015393c",
    "Megaphone": "e0d7d1199a6dd6538eff4dc6d8c557ad7cc6092be99db3412c670b15c97ab506",
    "Database": "4d58652b0dd16a1c6de08174c3ec6617da5d90dd2fa4d5f0147bfa44722fa5ec",
    "ShieldCheck": "16ebdabca38405206b1d36869ae74b0e9f69d34a23bc77d7cbe61dfa34bef1d7",
    "HardDrives": "16f26a807a05ded561ff3322879e698b70d77f07df5dabda451825ddd8f5ba28",
    "Presentation": "bf11a77aede260982281758bbb80fd7ce507c99dfa16afcd0aba89bbf9385ac2",
    "Grid": "728b2dbfa447ec3fec54f0f8b3b52cec573c51da00c00a6d73f699027758340c",
    "GameController": "9dcb7af7b4854bb2da15ab1852292428c4d95314b06cd25aa5f45f21b3134884",
    "ChartLineUp": "a2cb143280fca34c428684ee17674accf22733ff79e040b89510a03b068fceb9",
    "ChartPie": "102befbf7b1e76f13d5766c2a76dda120b208daf4b6803c45b872f41c6b7a4e2",
    "Trophy": "6d12bce1ad52193fe62e6d9cba30fbce19b355f3f096cafc316c3bc36c1f7ebe",
    "Rocket": "fed02fcf57da975870e6898085b8e469a695dafac38ddae2d92b169678c661a9",
    "Package": "4fa88054eefc32099e7fdc802cf4b4a1368cbbd914f06ab673fb54c11202bd49",
    "IdBadge": "772e0caaa75808fa6b1ed4a7389d45dd3b0bd3f96c9fbffee480faae32791846",
    "Camera": "c7528f079621e9b93eded98c1579463ec3b888dc821d5c238fc50287431d46c6",
    "Image": "4f4dbea3adfc054509fed7f8e0f7d50fbf716904e37fc55387d29018d15b52fd",
    "MusicNotes": "ddfa03bf39856870a4898b830525136583179dc019aa0ab8689eb5b7a65bc50d",
    "Newspaper": "0ef3aa31d14d9a103b8f10af48707520216608dbda84d2a9d07d4af3ae347652",
    "MapTrifold": "09ce0b41cc6bae13a9e89b78e9c7c4c64053d3e62d4f94b47ee62224387f0400",
    "ShoppingCart": "32193c360cbdce38028355bb0b20143e19c351c2ca5c8d7256e42cbe2643c59b",
    "ShoppingBag": "3625cfab5daaf82fb0b48d148ea87b4627c371fbea3360c59061fae37685e2c4",
    "Cpu": "03265526233e4f07528aee24d2dee2b0a70e9c080c7cc22b9066486fe62c4b10",
    "Fire": "512355fb2a156f0c39485a28bc6f81cf1df3de35faed9c5ad62c30cec5d8d63c",
    "Heartbeat": "d1ceb917a56d1ea875d4a08da0fef2586313a4f4896d48d352a3b6b9ece1cffb",
    "Car": "ba61d87b3526dec2adee1311ebc83518a0c85893384b068d2dd7b2c51d4dc917",
    "Hammer": "e1f85a791f0c18b239c2f7597dca64b4e8b55975af8c26f5364fab9278a18ec6",
    "Scales": "819cda55e5eba2727fe2026b28ef693b9a3c949f7df0908636f62dea9d699592",
    "Globe": "13c9e6860d02e943124e0fd55cd593dff53d5efe713f7ec39f68acdbfaa04150",
    "Envelope": "6cde9c0a359e967e36a6d3e54b720c67df75ce78f39e9adc7f29a7799bd09b54",
    "Ear": "ce76268c801c9b01210cf5e1d5949a001d816e1c3af4f77a4a5eb16fa7aaa71a",
    "Factory": "e370a3f063dc7825f65f1d980a77822489fcb2673d3da991d30034df4e47cd10",
    "Coffee": "6ce4e8690b466b14ecd1b1b51dd38b4e4ec12e5ce0b9d244f0038b6570c2fa94",
    "Receipt": "c8ea3afaa7142f69d8b40ee939390b34ef85c10dd932820bd5f145c6ccccba9f",
    "PiggyBank": "5c6958d4a302538ca70df3664c905e05ddb7762e3f7cd72f6c5ab65f0ccdc3df",
    "Tag": "e47e31e6769f655f8a58ad4d243c6fdba13902b02eb70fa3126e58d9b5febb5b",
    "Percent": "5dcd4b8a7fd5177aa49c58a0a6467c2340e0041f1fbc016e3f2ddb186bd2754b",
    "Drop": "547974bcf9a14a058c7622a2412bdf25700bd83e610128829f96687e7f8499e6",
    "Brain": "084f1324acaa199634182fec64d2594e2725fd52128251de15664ba27dd9da38",
    "Cloud": "f6cd33004a4073b56c2df63264711617c8a6cddfba89b19c7029b77b43f55ece",
    "CloudRain": "d2f32ce7bf06a9225f5c466d1f2c683cb729526d67c8b5f4741cd270b01e8cf3",
    "CloudSnow": "b63199e4b3dac2c77522126fafefb0bd0c444d9be13c5cf59379e135ab61648a",
    "SkipForward": "2f13f5cbeab47627d30782e7942001e7f80f7da993df053d3be2b8c6cb737ae2",
    "Train": "559c7f2fbed4fe9cb1ccb38900b6a2f24f9c2d0ce3c77347dddeeca4c7914a08",
    "ListChecks": "eefe95747f02e0aa9f183c75aac7ffdf87f69317cd775c383a606344cc7c78ae",
    "ClipboardText": "f455443ef50d7e60aecd2b25411be752b444943b91afdb7775900473d603a468",
    "FloppyDisk": "5cdcf21a2857922007b846b5ad02490374cf1a9bdf724d8be1fa9240ad3e246c",
    "Power": "42ad4a90770ba84dba25b95b8295d9f287f69115037a5063c04fcfe4e3a3256e",
    "SignOut": "2f2f2255d122c68badef3828451ac608c35b0a4b50a70242ca1a18029d44dd8f",
    "Pencil": "b78e7b71cb19d43e235983a883d50ab78cf9227f9864106e1aa4f366ee317aa8",
    "Layout": "56fcdfd673bc6fa51940957e9ea9b5609858193402bcf5ff0b02a278bbed540f",
    "Monitor": "0261a47b5f08e4c58f657ddb0596b64e59cc397f93085aecd2e05d8220249c20",
    "SpeakerHigh": "c266c407915382db3866570f816934b804c1b84bd47d971fad0e95ac5107dd6b",
    "Keyboard": "43d265edc6585ecc2a8a28edd15abe550ad5aa9ffe916c439e8cd76d6ea7df54",
    "Sliders": "59fdf39771e7939dd315912702f3c8241a9dfeb1f5b6512a074525884765a76c",
    "Cube": "9c681bfc8547fbb142d5df4d69ad923af5c422fea1013e6d69c32cef9ced3de3",
    "Lock": "7c3bb342922c2eb313d14a196d50dd897f7048e336da9382c418242e59c4cf79",
    "Reset": "e3d0f0459b6cbb52c485987d8612d77095202cf96c0b5e39b203686b004d864f",
    "Upload": "3d1c38449725c61048e136e7ab491b020fbf92bffc5581c3abf59b363323d8af",
    "Books": "308aa85d7c0f3aedebc77ac3a3dff0ac47d5957a4cd80f5a134304300ce8a138",
    "Download": "dee151da85ad71e90a882a556481e2069a154b8892063c9f91b621a0e06ad479",
    "Timer": "6cd85224477ca070b3a97b44ed3da8dc8e39c9fd76c529a69852e0abd352ce12",
    "Crop": "40fe628a321f48377e5aebd41da80b5d3ce8318fb1d7cdfda30afe86c98716c6",
    "Rotate": "78963c5a0ad83b74ab6b382640759bc3b8b4f57afb770e7575ad884586476bd6",
    "Exposure": "58c4989636ed62c97ae271521eaf4f741ced691eca7719a80e78d258d17e6403",
    "Level": "68f19f3c539982a658dbacb6844d819df76ff3389e8d371b0046764d75c3b6d0",
    "FlipCamera": "4ae415ae66182a66085050aee5d34fef5cc2a6d25aeb8452a8b01916c8cae8df",
    "EyeSlash": "fc34ad807da63ae5f99a235618cddc1b2bef8d98d7c95004d38b61312540b90d",
    "FrameCorners": "33896f65db543919b48a3a20bb17eacd7747e877af026d49c82da28e5c9fdeb6",
    "Funnel": "fd3cd3e6c43a5f76b34e1bb987061c7bff95133d94d4d72668c123e782747417",
    "PortraitFocus": "fdfd93a0bf89db5ac8c7f72ef581f890def3df830f5233da62bdc7e98ee77be3",
    "Walk": "1bd98cb5854e25fb93e4fcb59cb3ebe2af4c4fc1d4ad9cc28f52508ebb35bbee",
    "Taxi": "5b0fcb03fa1bf56b8a701aece4017de93aebc9a62bd0ae938e3cef3b537cb015",
    "Bookmark": "379dfd32059dc79b50e9e51a92ff94e028f0325262a985ad8136b0d4c661db65",
}

# The shapes an icon may be drawn with, and the attributes each may carry. Anything else stops the run, so an icon is never
# drawn without a part it has: a line or a stroke has no area to fill, a group or a transform is not followed here.
SHAPES = {
    "path": {"d"},
    "circle": {"cx", "cy", "r"},
    "ellipse": {"cx", "cy", "rx", "ry"},
    "rect": {"x", "y", "width", "height", "rx", "ry"},
    "polygon": {"points"},
    "polyline": {"points"},
}


def sha256(path):
    with open(path, "rb") as f:
        return hashlib.sha256(f.read()).hexdigest()


def source(name, weight):
    return os.path.join(SOURCES, weight, name + ("" if weight == "regular" else "-" + weight) + ".svg")


def rel(path):
    return os.path.relpath(path, ROOT).replace("\\", "/")


class Edges:
    """A fontTools pen that cuts every curve into straight edges and keeps each closed contour as a list of points."""

    def __init__(self):
        self.contours = []
        self._open = None

    def moveTo(self, p):
        self._close()
        self._open = [p]

    def lineTo(self, p):
        self._open.append(p)

    def curveTo(self, *points):
        if len(points) != 3:
            raise ValueError("a cubic with %d points" % len(points))
        p0 = self._open[-1]
        p1, p2, p3 = points
        # Wang's bound: this many even steps of t keep every edge within TOLERANCE of the cubic.
        ax, ay = p0[0] - 2 * p1[0] + p2[0], p0[1] - 2 * p1[1] + p2[1]
        bx, by = p1[0] - 2 * p2[0] + p3[0], p1[1] - 2 * p2[1] + p3[1]
        bend = max(math.hypot(ax, ay), math.hypot(bx, by))
        steps = max(1, math.ceil(math.sqrt(0.75 * bend / TOLERANCE)))
        for i in range(1, steps + 1):
            t = i / steps
            u = 1 - t
            self._open.append((u * u * u * p0[0] + 3 * u * u * t * p1[0] + 3 * u * t * t * p2[0] + t * t * t * p3[0],
                               u * u * u * p0[1] + 3 * u * u * t * p1[1] + 3 * u * t * t * p2[1] + t * t * t * p3[1]))

    def qCurveTo(self, *points):
        # TrueType style: on-curve points implied halfway between two off-curve ones.
        start = self._open[-1]
        for i, c in enumerate(points[:-1]):
            end = points[-1] if i == len(points) - 2 else ((c[0] + points[i + 1][0]) / 2, (c[1] + points[i + 1][1]) / 2)
            self.curveTo((start[0] + 2 * (c[0] - start[0]) / 3, start[1] + 2 * (c[1] - start[1]) / 3),
                         (end[0] + 2 * (c[0] - end[0]) / 3, end[1] + 2 * (c[1] - end[1]) / 3), end)
            start = end

    def closePath(self):
        self._close()

    def endPath(self):
        self._close()   # a fill closes an open subpath too

    def _close(self):
        if self._open:
            self.contours.append(self._open)
        self._open = None


def number(element, name, default=None):
    text = element.get(name)
    return default if text is None else float(text)


def shape_path(tag, el):
    """A circle, ellipse, rect or polygon as the path SVG fills for it."""
    if tag == "path":
        return el.get("d")
    if tag in ("circle", "ellipse"):
        cx, cy = number(el, "cx", 0.0), number(el, "cy", 0.0)
        rx = number(el, "r") if tag == "circle" else number(el, "rx")
        ry = rx if tag == "circle" else number(el, "ry")
        return "M%r,%r A%r,%r 0 1 0 %r,%r A%r,%r 0 1 0 %r,%rZ" % (cx - rx, cy, rx, ry, cx + rx, cy, rx, ry, cx - rx, cy)
    if tag == "rect":
        x, y, w, h = number(el, "x", 0.0), number(el, "y", 0.0), number(el, "width"), number(el, "height")
        rx, ry = number(el, "rx"), number(el, "ry")
        rx = ry if rx is None else rx
        ry = rx if ry is None else ry
        rx, ry = min(rx or 0.0, w / 2), min(ry or 0.0, h / 2)
        if rx <= 0 or ry <= 0:
            return "M%r,%r h%r v%r h%r Z" % (x, y, w, h, -w)
        return ("M%r,%r H%r A%r,%r 0 0 1 %r,%r V%r A%r,%r 0 0 1 %r,%r H%r A%r,%r 0 0 1 %r,%r V%r A%r,%r 0 0 1 %r,%rZ"
                % (x + rx, y, x + w - rx, rx, ry, x + w, y + ry, y + h - ry, rx, ry, x + w - rx, y + h, x + rx,
                   rx, ry, x, y + h - ry, y + ry, rx, ry, x + rx, y))
    coords = [float(v) for v in el.get("points").replace(",", " ").split()]
    if len(coords) < 6 or len(coords) % 2:
        raise ValueError("a polygon needs three or more x, y pairs")
    return "M" + " L".join("%r,%r" % (coords[i], coords[i + 1]) for i in range(0, len(coords), 2)) + "Z"


def quantise(contours, where):
    """Each contour's points in quarter units, repeats dropped. A contour with no area left is dropped too."""
    out = []
    for contour in contours:
        points = []
        for x, y in contour:
            p = (int(math.floor(x * QUANTUM + 0.5)), int(math.floor(y * QUANTUM + 0.5)))
            if not 0 <= p[0] <= BOX or not 0 <= p[1] <= BOX:
                raise SystemExit("ICONS: %s draws outside its 256-unit box at %r" % (where, (x, y)))
            if not points or points[-1] != p:
                points.append(p)
        while len(points) > 1 and points[0] == points[-1]:
            points.pop()
        if len(points) >= 3:
            out.append(points)
    return out


def read(path):
    """The icon's layers in document order (the back first), each (opacity in hundredths, contours)."""
    where = rel(path)
    svg = ET.parse(path).getroot()
    if svg.tag != SVG + "svg" or svg.get("viewBox") != "0 0 256 256" or svg.get("fill", "currentColor") != "currentColor":
        raise SystemExit("ICONS: %s is not a 256-unit Phosphor SVG filled in the current colour" % where)
    if set(svg.attrib) - {"viewBox", "fill"}:
        raise SystemExit("ICONS: %s's root carries %s, which icons.py does not follow" % (where, sorted(set(svg.attrib) - {"viewBox", "fill"})))
    layers = []
    for el in svg:
        tag = el.tag[len(SVG):] if el.tag.startswith(SVG) else el.tag
        if tag not in SHAPES or len(el):
            raise SystemExit("ICONS: %s holds a <%s>, which icons.py does not draw" % (where, tag))
        unknown = set(el.attrib) - SHAPES[tag] - {"opacity"}
        if unknown:
            raise SystemExit("ICONS: %s's <%s> carries %s, which icons.py does not follow" % (where, tag, sorted(unknown)))
        opacity = int(math.floor(float(el.get("opacity", "1")) * 100 + 0.5))
        if not 0 < opacity <= 100:
            raise SystemExit("ICONS: %s's <%s> has an opacity of %s" % (where, tag, el.get("opacity")))
        pen = Edges()
        parse_path(shape_path(tag, el), pen)
        contours = quantise(pen.contours, where)
        if contours:
            layers.append((opacity, contours))
    if not layers:
        raise SystemExit("ICONS: %s draws nothing" % where)
    return layers


def values(layers):
    """An icon's array: its layer count, then each layer's opacity and contour count, and each contour's point count, its
    first point and every next point as its step from the one before (a step takes two or three digits, a point four)."""
    out = [len(layers)]
    for opacity, contours in layers:
        out += [opacity, len(contours)]
        for contour in contours:
            out.append(len(contour))
            px, py = 0, 0
            for x, y in contour:
                out += [x - px, y - py]
                px, py = x, y
    return out


def array(name, numbers):
    lines, line = [], ""
    for v in numbers:
        piece = "%d," % v
        if line and 12 + len(line) + len(piece) > LINE:
            lines.append("            " + line)
            line = ""
        line += piece
    lines.append("            " + line)
    return "        private static readonly short[] %s =\n        {\n%s\n        };\n" % (name, "\n".join(lines))


def write_data(icons):
    out = io.StringIO()
    out.write("// Generated by UiAssets/scripts/icons.py from the pinned Phosphor icons in UiAssets/icons: do not edit by hand. Run the\n")
    out.write("// script again when an icon, ICONS or FILLS changes, and commit this file with it (icons.py --check holds them together).\n")
    out.write("namespace RealLifeOverhaul.Ui\n{\n")
    out.write("    /// <summary>\n")
    out.write("    /// Every icon's outlines from Phosphor (MIT, phosphor-icons/core %s), cut into straight edges for UiIconRaster. A\n" % COMMIT[:8])
    out.write("    /// duotone icon has a second variant, Phosphor's fill weight, the solid glyph a toolbar's tile asks for. An icon's array\n")
    out.write("    /// is its layer count, then each layer, the back first: its opacity in hundredths, its contour count, then each\n")
    out.write("    /// contour's point count, its first point and every next point as its step from the one before, all as x, y in\n")
    out.write("    /// quarter units of the 256-unit box, y down. Cost: nothing a frame.\n")
    out.write("    /// </summary>\n")
    out.write("    internal static partial class UiIconPaths\n    {\n")
    out.write("        internal const int Box = %d;   // the 256-unit box in the quarter units the points are kept in\n\n" % BOX)
    out.write("        /// <summary>\n")
    out.write("        /// An icon's outlines, its fill weight when filled is asked for and it has one, else its main weight. Null for None\n")
    out.write("        /// and for the glyphs drawn in code (the keys' and the pad's).\n")
    out.write("        /// </summary>\n")
    out.write("        internal static short[] Of(UiIcon icon, bool filled)\n        {\n            switch (icon)\n            {\n")
    for ui, _, _, fill in icons:
        if fill is None:
            out.write("                case UiIcon.%s: return %s;\n" % (ui, ui))
        else:
            out.write("                case UiIcon.%s: return filled ? %sFill : %s;\n" % (ui, ui, ui))
    out.write("                default: return null;\n            }\n        }\n")
    for ui, label, layers, fill in icons:
        out.write("\n        // %s\n" % label)
        out.write(array(ui, values(layers)))
        if fill is not None:
            out.write("\n        // %s fill\n" % label.split()[0])
            out.write(array(ui + "Fill", values(fill)))
    out.write("    }\n}\n")
    return out.getvalue().replace("\n", "\r\n")


def pinned_read(path, pinned, seen):
    got = sha256(path) if os.path.exists(path) else "missing"
    if got != pinned:
        raise SystemExit("ICONS: %s is not the pinned file (%s, wanted %s)" % (rel(path), got, pinned))
    seen.add(os.path.normcase(path))
    return read(path)


def main():
    check = "--check" in sys.argv
    duotones = {ui for ui, _, weight, _ in ICONS if weight == "duotone"}
    if set(FILLS) != duotones:
        raise SystemExit("ICONS: FILLS pins the fill weight of every duotone row and nothing else (%s)" % sorted(set(FILLS) ^ duotones))
    seen, names, icons = set(), set(), []
    for ui, name, weight, pinned in ICONS:
        if ui in names:
            raise SystemExit("ICONS: %s has two rows" % ui)
        names.add(ui)
        layers = pinned_read(source(name, weight), pinned, seen)
        fill = pinned_read(source(name, "fill"), FILLS[ui], seen) if ui in FILLS else None
        icons.append((ui, "%s %s" % (name, weight), layers, fill))
    for folder, _, files in os.walk(SOURCES):
        for file in files:
            if os.path.normcase(os.path.join(folder, file)) not in seen:
                raise SystemExit("ICONS: %s is in neither ICONS nor FILLS: add its row or take it out" % rel(os.path.join(folder, file)))
    text = write_data(icons)
    points = sum(len(c) for _, _, layers, fill in icons for _, contours in layers + (fill or []) for c in contours)
    if check:
        if not os.path.exists(DATA):
            raise SystemExit("ICONS: Scripts/Ui/UiIconPaths.Data.cs is missing: run icons.py and commit it")
        with open(DATA, "r", encoding="utf-8-sig", newline="") as f:
            if f.read() != text:
                raise SystemExit("ICONS: Scripts/Ui/UiIconPaths.Data.cs is not what the icons give: run icons.py and commit it")
        print("ICONS: outlines as the icons give them (%d icons, %d points)" % (len(icons), points))
        return
    with open(DATA, "w", encoding="utf-8", newline="") as f:
        f.write(text)
    print("outlines: %s (%d icons, %d points)" % (rel(DATA), len(icons), points))


if __name__ == "__main__":
    main()
