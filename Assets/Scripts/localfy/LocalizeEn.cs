using System.Collections.Generic;

/// <summary>
/// Built-in English localization text.
///
/// The built-in names are compiled directly into this C# file.
/// Newer online translations can still be added or overridden at runtime.
/// </summary>
public static class LocalizeEn
{
    public const string TranslationUrl = "https://raw.githubusercontent.com/UmaTL/hachimi-tl-en/refs/heads/main/localized_data/text_data_dict.json";
    public const string CharaTranslationTableKey = "6";
    public const string MobTranslationTableKey = "59";

    private static readonly Dictionary<int, string> RuntimeCharaNames =
        new Dictionary<int, string>();

    private static readonly Dictionary<int, string> RuntimeMobNames =
        new Dictionary<int, string>();

    // Built-in character names.
    public const string Chara1001 = "Special Week";
    public const string Chara1002 = "Silence Suzuka";
    public const string Chara1003 = "Tokai Teio";
    public const string Chara1004 = "Maruzensky";
    public const string Chara1005 = "Fuji Kiseki";
    public const string Chara1006 = "Oguri Cap";
    public const string Chara1007 = "Gold Ship";
    public const string Chara1008 = "Vodka";
    public const string Chara1009 = "Daiwa Scarlet";
    public const string Chara1010 = "Taiki Shuttle";
    public const string Chara1011 = "Grass Wonder";
    public const string Chara1012 = "Hishi Amazon";
    public const string Chara1013 = "Mejiro McQueen";
    public const string Chara1014 = "El Condor Pasa";
    public const string Chara1015 = "T.M. Opera O";
    public const string Chara1016 = "Narita Brian";
    public const string Chara1017 = "Symboli Rudolf";
    public const string Chara1018 = "Air Groove";
    public const string Chara1019 = "Agnes Digital";
    public const string Chara1020 = "Seiun Sky";
    public const string Chara1021 = "Tamamo Cross";
    public const string Chara1022 = "Fine Motion";
    public const string Chara1023 = "Biwa Hayahide";
    public const string Chara1024 = "Mayano Top Gun";
    public const string Chara1025 = "Manhattan Cafe";
    public const string Chara1026 = "Mihono Bourbon";
    public const string Chara1027 = "Mejiro Ryan";
    public const string Chara1028 = "Hishi Akebono";
    public const string Chara1029 = "Yukino Bijin";
    public const string Chara1030 = "Rice Shower";
    public const string Chara1031 = "Ines Fujin";
    public const string Chara1032 = "Agnes Tachyon";
    public const string Chara1033 = "Admire Vega";
    public const string Chara1034 = "Inari One";
    public const string Chara1035 = "Winning Ticket";
    public const string Chara1036 = "Air Shakur";
    public const string Chara1037 = "Eishin Flash";
    public const string Chara1038 = "Curren Chan";
    public const string Chara1039 = "Kawakami Princess";
    public const string Chara1040 = "Gold City";
    public const string Chara1041 = "Sakura Bakushin O";
    public const string Chara1042 = "Seeking the Pearl";
    public const string Chara1043 = "Shinko Windy";
    public const string Chara1044 = "Sweep Tosho";
    public const string Chara1045 = "Super Creek";
    public const string Chara1046 = "Smart Falcon";
    public const string Chara1047 = "Zenno Rob Roy";
    public const string Chara1048 = "Tosen Jordan";
    public const string Chara1049 = "Nakayama Festa";
    public const string Chara1050 = "Narita Taishin";
    public const string Chara1051 = "Nishino Flower";
    public const string Chara1052 = "Haru Urara";
    public const string Chara1053 = "Bamboo Memory";
    public const string Chara1054 = "Biko Pegasus";
    public const string Chara1055 = "Marvelous Sunday";
    public const string Chara1056 = "Matikanefukukitaru";
    public const string Chara1057 = "Mr. C.B.";
    public const string Chara1058 = "Meisho Doto";
    public const string Chara1059 = "Mejiro Dober";
    public const string Chara1060 = "Nice Nature";
    public const string Chara1061 = "King Halo";
    public const string Chara1062 = "Matikanetannhauser";
    public const string Chara1063 = "Ikuno Dictus";
    public const string Chara1064 = "Mejiro Palmer";
    public const string Chara1065 = "Daitaku Helios";
    public const string Chara1066 = "Twin Turbo";
    public const string Chara1067 = "Satono Diamond";
    public const string Chara1068 = "Kitasan Black";
    public const string Chara1069 = "Sakura Chiyono O";
    public const string Chara1070 = "Sirius Symboli";
    public const string Chara1071 = "Mejiro Ardan";
    public const string Chara1072 = "Yaeno Muteki";
    public const string Chara1073 = "Tsurumaru Tsuyoshi";
    public const string Chara1074 = "Mejiro Bright";
    public const string Chara1075 = "Daring Tact";
    public const string Chara1076 = "Sakura Laurel";
    public const string Chara1077 = "Narita Top Road";
    public const string Chara1078 = "Yamanin Zephyr";
    public const string Chara1079 = "Furioso";
    public const string Chara1080 = "Transcend";
    public const string Chara1081 = "Espoir City";
    public const string Chara1082 = "North Flight";
    public const string Chara1083 = "Symboli Kris S";
    public const string Chara1084 = "Tanino Gimlet";
    public const string Chara1085 = "Daiichi Ruby";
    public const string Chara1086 = "Mejiro Ramonu";
    public const string Chara1087 = "Aston Machan";
    public const string Chara1088 = "Satono Crown";
    public const string Chara1089 = "Cheval Grand";
    public const string Chara1090 = "Verxina";
    public const string Chara1091 = "Vivlos";
    public const string Chara1092 = "Dantsu Flame";
    public const string Chara1093 = "K.S.Miracle";
    public const string Chara1094 = "Jungle Pocket";
    public const string Chara1095 = "Believe";
    public const string Chara1096 = "No Reason";
    public const string Chara1097 = "Still in Love";
    public const string Chara1098 = "Copano Rickey";
    public const string Chara1099 = "Hokko Tarumae";
    public const string Chara1100 = "Wonder Acute";
    public const string Chara1102 = "Sounds of Earth";
    public const string Chara1103 = "Royce and Royce";
    public const string Chara1104 = "Katsuragi Ace";
    public const string Chara1105 = "Neo Universe";
    public const string Chara1106 = "Hishi Miracle";
    public const string Chara1107 = "Tap Dance City";
    public const string Chara1108 = "Duramente";
    public const string Chara1109 = "Rhein Kraft";
    public const string Chara1110 = "Cesario";
    public const string Chara1111 = "Air Messiah";
    public const string Chara1112 = "Daring Heart";
    public const string Chara1113 = "Fusaichi Pandora";
    public const string Chara1114 = "Buena Vista";
    public const string Chara1115 = "Orfevre";
    public const string Chara1116 = "Gentildonna";
    public const string Chara1117 = "Win Variation";
    public const string Chara1118 = "Admire Groove";
    public const string Chara1119 = "Dream Journey";
    public const string Chara1120 = "Calstone Light O";
    public const string Chara1121 = "Durandal";
    public const string Chara1124 = "Bubble Gum Fellow";
    public const string Chara1126 = "Sakura Chitose O";
    public const string Chara1127 = "Fenomeno";
    public const string Chara1128 = "Blast Onepiece";
    public const string Chara1129 = "Almond Eye";
    public const string Chara1130 = "Lucky Lilac";
    public const string Chara1131 = "Gran Alegria";
    public const string Chara1132 = "Loves Only You";
    public const string Chara1133 = "Chrono Genesis";
    public const string Chara1134 = "Curren Bouquetd'or";
    public const string Chara1135 = "Stay Gold";
    public const string Chara1136 = "Red Desire";
    public const string Chara1137 = "Kiseki";
    public const string Chara1138 = "Forever Young";
    public const string Chara1139 = "Casino Drive";
    public const string Chara1140 = "Marche Lorraine";
    public const string Chara1141 = "Epiphaneia";
    public const string Chara1142 = "Logotype";
    public const string Chara1143 = "Victoire Pisa";
    public const string Chara1144 = "Rose Kingdom";
    public const string Chara1145 = "Rulership";
    public const string Chara2001 = "Happy Meek";
    public const string Chara2002 = "Bitter Glasse";
    public const string Chara2003 = "Little Cocon";
    public const string Chara2004 = "Montjeu";
    public const string Chara2005 = "Venus Paques";
    public const string Chara2006 = "Rigantona";
    public const string Chara2007 = "Sonon Elfie";
    public const string Chara2008 = "ST-2";
    public const string Chara9001 = "Hayakawa Tazuna";
    public const string Chara9002 = "Director Akikawa";
    public const string Chara9003 = "Otonashi Etsuko";
    public const string Chara9004 = "Trainer Kiryūin";
    public const string Chara9005 = "Anshinzawa Sasami";
    public const string Chara9006 = "Kashimoto Riko";
    public const string Chara9007 = "Beauty Anshinzawa";
    public const string Chara9008 = "Light Hello";
    public const string Chara9040 = "Darley Arabian";
    public const string Chara9041 = "Godolphin Barb";
    public const string Chara9042 = "Byerley Turk";
    public const string Chara9043 = "Satake Mei";
    public const string Chara9044 = "Tsurugi Ryoka";
    public const string Chara9045 = "Sugar Lights";
    public const string Chara9046 = "Saint Lite";
    public const string Chara9047 = "Speed Symboli";
    public const string Chara9048 = "Haiseiko";
    public const string Chara9049 = "Tucker Bryne";
    public const string Chara9050 = "Hoshina Kiyoko";
    public const string Chara9051 = "Yunohana Bloom";

    // mob名字
    public const string Mob8000 = "Jewel Nephrite";
    public const string Mob8001 = "Bridge Comp";
    public const string Mob8002 = "Mini Baillonii";
    public const string Mob8003 = "Freistaat";
    public const string Mob8004 = "Excellency";
    public const string Mob8005 = "Aquafall";
    public const string Mob8006 = "Viper Pierce";
    public const string Mob8007 = "Dearest Gift";
    public const string Mob8008 = "Mini Lily";
    public const string Mob8009 = "Coincidence";
    public const string Mob8010 = "Breeze Shuttle";
    public const string Mob8011 = "Choco Choco";
    public const string Mob8012 = "Athame";
    public const string Mob8013 = "Sowasowa";
    public const string Mob8014 = "Mini Orchid";
    public const string Mob8015 = "Ribbon Fugue";
    public const string Mob8016 = "Sax Rhythm";
    public const string Mob8017 = "Reed Magazine";
    public const string Mob8018 = "Dominant Power";
    public const string Mob8019 = "Brown Montblanc";
    public const string Mob8020 = "Vital Dynamo";
    public const string Mob8021 = "Chanson Jeanne";
    public const string Mob8022 = "Mini Rose";
    public const string Mob8023 = "Avec Dream";
    public const string Mob8024 = "Sao Novel";
    public const string Mob8025 = "Kaiko Ichiban";
    public const string Mob8026 = "Jumbo Geniessen";
    public const string Mob8027 = "Agile Talent";
    public const string Mob8028 = "Power Charger";
    public const string Mob8029 = "Sarasate Opera";
    public const string Mob8030 = "Algol";
    public const string Mob8031 = "Ribbon Nocturne";
    public const string Mob8032 = "Harp Rhythm";
    public const string Mob8033 = "Springwell";
    public const string Mob8034 = "Clarinet Rhythm";
    public const string Mob8035 = "Duo Perte";
    public const string Mob8036 = "Hush Hush";
    public const string Mob8037 = "Propellizer";
    public const string Mob8038 = "Basal Shoot";
    public const string Mob8039 = "Jewel Crystal";
    public const string Mob8040 = "Beyond Revlimi";
    public const string Mob8041 = "Book of Sugar";
    public const string Mob8042 = "Frilled Mandarin";
    public const string Mob8043 = "Bravados";
    public const string Mob8044 = "Missionary";
    public const string Mob8045 = "Duo Clipeus";
    public const string Mob8046 = "Aquafjord";
    public const string Mob8047 = "Ice Hopper";
    public const string Mob8048 = "Coffee Parfait";
    public const string Mob8049 = "Bravoire";
    public const string Mob8050 = "Culacula";
    public const string Mob8051 = "Reflector";
    public const string Mob8052 = "Bella Prateria";
    public const string Mob8053 = "Hard Lacqeur";
    public const string Mob8054 = "Electrified";
    public const string Mob8055 = "Lady Adamant";
    public const string Mob8056 = "Spooky Knight";
    public const string Mob8057 = "Tup Shimati";
    public const string Mob8058 = "Uptree";
    public const string Mob8059 = "Carnwennan";
    public const string Mob8060 = "Maroon Sky";
    public const string Mob8061 = "Viola Rhythm";
    public const string Mob8062 = "Ribbon Madrigal";
    public const string Mob8063 = "Reed Fantasy";
    public const string Mob8064 = "Harmonia Grace";
    public const string Mob8065 = "Maiden Charm";
    public const string Mob8066 = "Kustawi";
    public const string Mob8067 = "More Than Anything";
    public const string Mob8068 = "Beauty Again";
    public const string Mob8069 = "Reckless Shot";
    public const string Mob8070 = "Septagon Summoner";
    public const string Mob8071 = "Encore One More";
    public const string Mob8072 = "Polka Step";
    public const string Mob8073 = "Shadow Stalker";
    public const string Mob8074 = "Dispatcher";
    public const string Mob8075 = "Vortex Twist";
    public const string Mob8076 = "Gleam Atrium";
    public const string Mob8077 = "Special Parfait";
    public const string Mob8078 = "Faster than Ray";
    public const string Mob8079 = "Royal Marine";
    public const string Mob8080 = "Hexa Canyon";
    public const string Mob8081 = "Berry Master";
    public const string Mob8082 = "Fleur de Chemin";
    public const string Mob8083 = "Tombolo";
    public const string Mob8084 = "Vihuela Rhythm";
    public const string Mob8085 = "Lavien Rose";
    public const string Mob8086 = "Great House";
    public const string Mob8087 = "Walkie Talkie";
    public const string Mob8088 = "Starry Pride";
    public const string Mob8089 = "Mini Cosmos";
    public const string Mob8090 = "Saga Goes On";
    public const string Mob8091 = "Spool Mover";
    public const string Mob8092 = "Follow The Sun";
    public const string Mob8093 = "Wistcraft";
    public const string Mob8094 = "Ribbon Operetta";
    public const string Mob8095 = "Aqua Ocean";
    public const string Mob8096 = "Tutor Support";
    public const string Mob8097 = "Speechless Hack";
    public const string Mob8098 = "Work Faithful";
    public const string Mob8099 = "Overdrain";
    public const string Mob8100 = "Excite Stuff";
    public const string Mob8101 = "Mini Ball Sum";
    public const string Mob8102 = "Tsuukaa";
    public const string Mob8103 = "Shout My Name";
    public const string Mob8104 = "Local Stream";
    public const string Mob8105 = "Hot Dynamite";
    public const string Mob8106 = "Royal Servant";
    public const string Mob8107 = "Duo Sipar";
    public const string Mob8108 = "Polite Salute";
    public const string Mob8109 = "Tide and Flow";
    public const string Mob8110 = "Jewel Topaz";
    public const string Mob8111 = "Hearty Letter";
    public const string Mob8112 = "Jewel Spinel";
    public const string Mob8113 = "Tocotoco";
    public const string Mob8114 = "Sweet Parfait";
    public const string Mob8115 = "Royal Coronet";
    public const string Mob8116 = "Against Gale";
    public const string Mob8117 = "Ontologist";
    public const string Mob8118 = "Aqua Lagoon";
    public const string Mob8119 = "Confusion";
    public const string Mob8120 = "At One Mile";
    public const string Mob8121 = "Ribbon Lullaby";
    public const string Mob8122 = "Reed Photobook";
    public const string Mob8123 = "Britain Prime";
    public const string Mob8124 = "Coruscanti";
    public const string Mob8125 = "Tender Step";
    public const string Mob8126 = "Mucha Lady";
    public const string Mob8127 = "Ribbon Capriccio";
    public const string Mob8128 = "Duo Svol";
    public const string Mob8129 = "Torch and Book";
    public const string Mob8130 = "For Shoes";
    public const string Mob8131 = "Jewel Azurite";
    public const string Mob8132 = "Ribbon Minnet";
    public const string Mob8133 = "Dorje";
    public const string Mob8134 = "Ribbon Elegy";
    public const string Mob8135 = "Navigate Light";
    public const string Mob8136 = "Kleine Kiste";
    public const string Mob8137 = "Swift Axel";
    public const string Mob8138 = "Dropping Link";
    public const string Mob8139 = "Reed Critique";
    public const string Mob8140 = "Toujours";
    public const string Mob8141 = "Frilled Peach";
    public const string Mob8142 = "Ballet Step";
    public const string Mob8143 = "Celeb Actress";
    public const string Mob8144 = "Six Pack";
    public const string Mob8145 = "Rose Bouquet Toss";
    public const string Mob8146 = "Honest Words";
    public const string Mob8147 = "Econoanimal";
    public const string Mob8148 = "Ukaldi";
    public const string Mob8149 = "Place in Heaven";
    public const string Mob8150 = "Primal Dawn";
    public const string Mob8151 = "Shofar Rhythm";
    public const string Mob8152 = "High Time Soon";
    public const string Mob8153 = "Finest Day";
    public const string Mob8154 = "Terpander";
    public const string Mob8155 = "Quartet Accord";
    public const string Mob8156 = "Choke Point";
    public const string Mob8157 = "Piccola Variant";
    public const string Mob8158 = "Holiday Hike";
    public const string Mob8159 = "Seventh Queen";
    public const string Mob8160 = "Poisonous";
    public const string Mob8161 = "Polyhymnia";
    public const string Mob8162 = "Set Your Record";
    public const string Mob8163 = "Ribbon Finale";
    public const string Mob8164 = "Basileon Touch";
    public const string Mob8165 = "Ribbon Humming";
    public const string Mob8166 = "Ribbon Mambo";
    public const string Mob8167 = "Reed Poetry";
    public const string Mob8168 = "Sacoche";
    public const string Mob8169 = "Apple Cider";
    public const string Mob8170 = "Westside";
    public const string Mob8171 = "Caramel Parfait";
    public const string Mob8172 = "Itsutsuba Clover";
    public const string Mob8173 = "Black Grimoire";
    public const string Mob8174 = "Awilda";
    public const string Mob8175 = "Landsknecht";
    public const string Mob8176 = "Contest Rival";
    public const string Mob8177 = "Waga Hado";
    public const string Mob8178 = "Bow and Shield";
    public const string Mob8179 = "Sunset Groom";
    public const string Mob8180 = "Duo Buckler";
    public const string Mob8181 = "Sidecar";
    public const string Mob8182 = "Salsa Step";
    public const string Mob8183 = "Sharanga";
    public const string Mob8184 = "Biproduction";
    public const string Mob8185 = "Little Trattoria";
    public const string Mob8186 = "Jewel Peridot";
    public const string Mob8187 = "Soprano Rhythm";
    public const string Mob8188 = "Immediate";
    public const string Mob8189 = "Alive Karin";
    public const string Mob8190 = "Weiss Manager";
    public const string Mob8191 = "Double Surround";
    public const string Mob8192 = "Narcissus";
    public const string Mob8193 = "Rhythmic Leap";
    public const string Mob8194 = "Bitter Parfait";
    public const string Mob8195 = "Turcke";
    public const string Mob8196 = "Rumba Step";
    public const string Mob8197 = "Innocent Grimoire";
    public const string Mob8198 = "Atrum Grimoire";
    public const string Mob8199 = "Ney Rhythm";
    public const string Mob8200 = "Frilled Pine";
    public const string Mob8201 = "Cupid Shoot";
    public const string Mob8202 = "Ribbon Sirvente";
    public const string Mob8203 = "Breeze Drone";
    public const string Mob8204 = "Mini Clematis";
    public const string Mob8205 = "Tavatimsa";
    public const string Mob8206 = "Sudden Attack";
    public const string Mob8207 = "Srivatsa";
    public const string Mob8208 = "Early Sprout";
    public const string Mob8209 = "Long West";
    public const string Mob8210 = "Ribbon Hymns";
    public const string Mob8211 = "Mini Narcissus";
    public const string Mob8212 = "Tutunui";
    public const string Mob8213 = "Sunfish Ray";
    public const string Mob8214 = "Hot Dance";
    public const string Mob8215 = "Squeeze Out";
    public const string Mob8216 = "Decision";
    public const string Mob8217 = "Arcade Champ";
    public const string Mob8218 = "Cymbal Rhythm";
    public const string Mob8219 = "Showman's Act";
    public const string Mob8220 = "Predatrice";
    public const string Mob8221 = "Musha Musha";
    public const string Mob8222 = "Ribbon Gavotte";
    public const string Mob8223 = "Flying Turkey";
    public const string Mob8224 = "Leaf Leaf";
    public const string Mob8225 = "Punipuni";
    public const string Mob8226 = "Cornet Rhythm";
    public const string Mob8227 = "Pampa Grande";
    public const string Mob8228 = "Tip of Tongue";
    public const string Mob8229 = "Tetrabiblos";
    public const string Mob8230 = "Pink Chouchou";
    public const string Mob8231 = "Breeze Glider";
    public const string Mob8232 = "Occident Four";
    public const string Mob8233 = "Divinity";
    public const string Mob8234 = "Keyboard Rhythm";
    public const string Mob8235 = "Bravo Deux";
    public const string Mob8236 = "Myon Myon";
    public const string Mob8237 = "Snow Frost";
    public const string Mob8238 = "Jagdplaute";
    public const string Mob8239 = "Classic Comedy";
    public const string Mob8240 = "Gran Shamal";
    public const string Mob8241 = "Muruga";
    public const string Mob8242 = "Town Hangout";
    public const string Mob8243 = "Duo Priwen";
    public const string Mob8244 = "Destinate";
    public const string Mob8245 = "Yappy Lucky";
    public const string Mob8246 = "Batabata";
    public const string Mob8247 = "Dueling Stella";
    public const string Mob8248 = "Aqua Oasis";
    public const string Mob8249 = "Feel Freude";
    public const string Mob8250 = "Ribbon Pastoral";
    public const string Mob8251 = "Roseful Vase";
    public const string Mob8252 = "Summer Bonfire";
    public const string Mob8253 = "Bird and Cliff";
    public const string Mob8254 = "Mykonos Chalk";
    public const string Mob8255 = "Grand Feast";
    public const string Mob8256 = "Lime Chouchou";
    public const string Mob8257 = "Black Ebony";
    public const string Mob8258 = "Kauriraris";
    public const string Mob8259 = "Breeze Cessna";
    public const string Mob8260 = "Resort Icon";
    public const string Mob8261 = "Colichemarde";
    public const string Mob8262 = "Pastime Joy";
    public const string Mob8263 = "Valois Marron";
    public const string Mob8264 = "Ribbon Rondo";
    public const string Mob8265 = "Short Sleeper";
    public const string Mob8266 = "Beating Pulse";
    public const string Mob8267 = "Silver Sazanka";
    public const string Mob8268 = "Petite Folklore";
    public const string Mob8269 = "Cinnamon Milk";
    public const string Mob8270 = "Dirham Coin";
    public const string Mob8271 = "Frilled Banana";
    public const string Mob8272 = "Reed Historia";
    public const string Mob8273 = "Heroine Advents";
    public const string Mob8274 = "Superior Bloom";
    public const string Mob8275 = "Frilled Lime";
    public const string Mob8276 = "Feel The Fate";
    public const string Mob8277 = "Dolichos Runner";
    public const string Mob8278 = "Magyar Rondo";
    public const string Mob8279 = "German Cake";
    public const string Mob8280 = "Aqua Spring";
    public const string Mob8281 = "Outstand Gig";
    public const string Mob8282 = "Life Grateful";
    public const string Mob8283 = "I Am Queen";
    public const string Mob8284 = "Breeze Balloon";
    public const string Mob8285 = "Must Choose Me";
    public const string Mob8286 = "Gray Chouchou";
    public const string Mob8287 = "Gutenberg";
    public const string Mob8288 = "Anguta";
    public const string Mob8289 = "Ay Tanri";
    public const string Mob8290 = "Jewel Zircon";
    public const string Mob8291 = "Belongings";
    public const string Mob8292 = "Ivory Chouchou";
    public const string Mob8293 = "Autumn Mountain";
    public const string Mob8294 = "Farm Volition";
    public const string Mob8295 = "Desert Baby";
    public const string Mob8296 = "Unchanging";
    public const string Mob8297 = "Fairies Echo";
    public const string Mob8298 = "Sangarius";
    public const string Mob8299 = "Trumpet Rhythm";
    public const string Mob8300 = "Amber Chouchou";
    public const string Mob8301 = "Sharp Attract";
    public const string Mob8302 = "Memo Labyrinth";
    public const string Mob8303 = "Silver Chouchou";
    public const string Mob8304 = "Gimme One Love";
    public const string Mob8305 = "Jewel Tourmaline";
    public const string Mob8306 = "Blue Aquamarine";
    public const string Mob8307 = "Neptunus";
    public const string Mob8308 = "Holy Choir";
    public const string Mob8309 = "Paikea";
    public const string Mob8310 = "Ribbon Dirge";
    public const string Mob8311 = "Breeze Chopper";
    public const string Mob8312 = "Natale Notte";
    public const string Mob8313 = "Gullintanni";
    public const string Mob8314 = "Ketchup Step";
    public const string Mob8315 = "One Inch of Love";
    public const string Mob8316 = "Bianco Grimoire";
    public const string Mob8317 = "Albedo Belladonna";
    public const string Mob8318 = "Marionette Waltz";
    public const string Mob8319 = "Imagine Success";
    public const string Mob8320 = "Cossack Step";
    public const string Mob8321 = "Dreaminess Days";
    public const string Mob8322 = "Eisentaenzer";
    public const string Mob8323 = "Crescent Ace";
    public const string Mob8324 = "Duo Scutum";
    public const string Mob8325 = "Frilled Orange";
    public const string Mob8326 = "Zamburak";
    public const string Mob8327 = "Going Noble";
    public const string Mob8328 = "Jewel Amethyst";
    public const string Mob8329 = "Revival Lyric";
    public const string Mob8330 = "With Caspar";
    public const string Mob8331 = "Olino Corriente";
    public const string Mob8332 = "Casual Snap";
    public const string Mob8333 = "Gigant Grendel";
    public const string Mob8334 = "Out of Black";
    public const string Mob8335 = "Marsyas";
    public const string Mob8336 = "Break Step";
    public const string Mob8337 = "Jewel Rubellite";
    public const string Mob8338 = "Pudding Parfait";
    public const string Mob8339 = "Zipangu Applause";
    public const string Mob8340 = "Illapa";
    public const string Mob8341 = "Greed Hollow";
    public const string Mob8342 = "Oboe Rhythm";
    public const string Mob8343 = "Ababinili";
    public const string Mob8344 = "Intense Remark";
    public const string Mob8345 = "Mini Herb";
    public const string Mob8346 = "Duo Targe";
    public const string Mob8347 = "Reed Suspense";
    public const string Mob8348 = "Ribbon Aubade";
    public const string Mob8349 = "Frilled Apple";
    public const string Mob8350 = "Imperial Thalys";
    public const string Mob8351 = "Mini Zinnia";
    public const string Mob8352 = "Dokadoka";
    public const string Mob8353 = "Stay Charlene";
    public const string Mob8354 = "Orange Chouchou";
    public const string Mob8355 = "Tour d'Ivoire";
    public const string Mob8356 = "Oboro Evening";
    public const string Mob8357 = "Flamenco Step";
    public const string Mob8358 = "Sweet Cabin";
    public const string Mob8359 = "Jebat";
    public const string Mob8360 = "Yum Yum Parfait";
    public const string Mob8361 = "My Treat";
    public const string Mob8362 = "Prime Season";
    public const string Mob8363 = "Sam Garden";
    public const string Mob8364 = "Flower Net";
    public const string Mob8365 = "Frilled Grape";
    public const string Mob8366 = "Reverent";
    public const string Mob8367 = "Waltz Step";
    public const string Mob8368 = "Key Card";
    public const string Mob8369 = "Flute Rhythm";
    public const string Mob8370 = "Alley Cat";
    public const string Mob8371 = "Black Tipped";
    public const string Mob8372 = "Bronze Chouchou";
    public const string Mob8373 = "Heart Blowup";
    public const string Mob8374 = "Polar Dipper";
    public const string Mob8375 = "Jakarta Funk";
    public const string Mob8376 = "Blanc Grimoire";
    public const string Mob8377 = "Rapid Builder";
    public const string Mob8378 = "Memorial Golazo";
    public const string Mob8379 = "Circuit Breaker";
    public const string Mob8380 = "Dalmatian";
    public const string Mob8381 = "Duo Ecu";
    public const string Mob8382 = "Breeze Kite";
    public const string Mob8383 = "Mini Veronica";
    public const string Mob8384 = "Enchufla";
    public const string Mob8385 = "Jazz Step";
    public const string Mob8386 = "Yuiitsu Muni";
    public const string Mob8387 = "Ferment Win";
    public const string Mob8388 = "Pan Pacific";
    public const string Mob8389 = "Duo Aspis";
    public const string Mob8390 = "Trad Parfait";
    public const string Mob8391 = "Chalemie Rhythm";
    public const string Mob8392 = "Battle of Elah";
    public const string Mob8393 = "Vassago";
    public const string Mob8394 = "Fly Field";
    public const string Mob8395 = "Jewel Onyx";
    public const string Mob8396 = "Variable Sight";
    public const string Mob8397 = "Silver Berry";
    public const string Mob8398 = "Higgs Spray";
    public const string Mob8399 = "Brutal Rush";
    public const string Mob8400 = "Heracleion Myth";
    public const string Mob8401 = "Lovely Silhouette";
    public const string Mob8402 = "Frilled Melon";
    public const string Mob8403 = "Nautical Tool";
    public const string Mob8404 = "Solar Ray";
    public const string Mob8405 = "Shore Lighthouse";
    public const string Mob8406 = "Create Send";
    public const string Mob8407 = "Thousand Voltaire";
    public const string Mob8408 = "Tap Step";
    public const string Mob8409 = "Septentrion";
    public const string Mob8410 = "Code of Heart";
    public const string Mob8411 = "Ribbon Carol";
    public const string Mob8412 = "Fierce Kick";
    public const string Mob8413 = "Ajisai Gekko";
    public const string Mob8414 = "Original Shine";
    public const string Mob8415 = "Mollfrith";
    public const string Mob8416 = "Piccolo Rhythm";
    public const string Mob8417 = "Jewel Garnet";
    public const string Mob8418 = "Tomoenage";
    public const string Mob8419 = "Nereid Rendezvous";
    public const string Mob8420 = "Hunahpu";
    public const string Mob8421 = "Be Kaiser";
    public const string Mob8422 = "Powerful Torque";
    public const string Mob8423 = "Tactical One";
    public const string Mob8424 = "Ribbon Minuet";
    public const string Mob8425 = "Ampere Unit";
    public const string Mob8426 = "Mini Marigold";
    public const string Mob8427 = "Aeneas";
    public const string Mob8428 = "Traffic Lights";
    public const string Mob8429 = "Tamaxchi";
    public const string Mob8430 = "Jewel Sphene";
    public const string Mob8431 = "Moist Eyes";
    public const string Mob8432 = "Gorgeous Parfait";
    public const string Mob8433 = "Crazy In Love";
    public const string Mob8434 = "So Dramatic";
    public const string Mob8435 = "Mini Cotton";
    public const string Mob8436 = "Antagonist";
    public const string Mob8437 = "Makila";
    public const string Mob8438 = "Izcalli";
    public const string Mob8439 = "Bravo Zwei";
    public const string Mob8440 = "Hoeroa";
    public const string Mob8441 = "Tipping Tap";
    public const string Mob8442 = "Synth Field";
    public const string Mob8443 = "Colorful Pastel";
    public const string Mob8444 = "Selen Spark";
    public const string Mob8445 = "Multicell Call";
    public const string Mob8446 = "Ribbon Virelai";
    public const string Mob8447 = "Fruit Parfait";
    public const string Mob8448 = "Mini Dandelion";
    public const string Mob8449 = "Ribbon Mazurka";
    public const string Mob8450 = "Aqua River";
    public const string Mob8451 = "Tropical Sky";
    public const string Mob8452 = "Aqua Geyser";
    public const string Mob8453 = "Venabulum";
    public const string Mob8454 = "Cithara Rhythm";
    public const string Mob8455 = "Requiem Wisp";
    public const string Mob8456 = "Royal Tartan";
    public const string Mob8457 = "Weiss Grimoire";
    public const string Mob8458 = "Pukapuka";
    public const string Mob8459 = "Girly Smile";
    public const string Mob8460 = "Wicked Lady";
    public const string Mob8461 = "Maleficus";
    public const string Mob8462 = "Skanda";
    public const string Mob8463 = "Primera Chica";
    public const string Mob8464 = "Takeoff Plane";
    public const string Mob8465 = "Frozen Sky";
    public const string Mob8466 = "Nexus Force";
    public const string Mob8467 = "Jarajara";
    public const string Mob8468 = "Breeze Plane";
    public const string Mob8469 = "Frilled Lemon";
    public const string Mob8470 = "Rural Leisure";
    public const string Mob8471 = "Blue Chouchou";
    public const string Mob8472 = "Replication";
    public const string Mob8473 = "Mini Lavender";
    public const string Mob8474 = "Aggregation";
    public const string Mob8475 = "Bravely Ko";
    public const string Mob8476 = "Mini Cactus";
    public const string Mob8477 = "Jewel Calcite";
    public const string Mob8478 = "Krasnaya";
    public const string Mob8479 = "Sand Commando";
    public const string Mob8480 = "Touring Bike";
    public const string Mob8481 = "Bayt al-Hikmah";
    public const string Mob8482 = "Elegant General";
    public const string Mob8483 = "Time Ticking";
    public const string Mob8484 = "Everyone Likes";
    public const string Mob8485 = "Long Caravan";
    public const string Mob8486 = "Encoder";
    public const string Mob8487 = "Ribbon March";
    public const string Mob8488 = "Mechanical Vapor";
    public const string Mob8489 = "Break Chain";
    public const string Mob8490 = "Chattering Cheek";
    public const string Mob8491 = "Zip Liner";
    public const string Mob8492 = "Symptom Dash";
    public const string Mob8493 = "It's Calling";
    public const string Mob8494 = "Stenz";
    public const string Mob8495 = "Mini Pansy";
    public const string Mob8496 = "Breeze Airship";
    public const string Mob8497 = "Soir Celeste";
    public const string Mob8498 = "Pleasant Clerk";
    public const string Mob8499 = "Jewel Ruby";
    public const string Mob8500 = "Kaiser Palace";
    public const string Mob8501 = "Tunneling Voice";
    public const string Mob8502 = "Mint Drop";
    public const string Mob8503 = "Ribbon Etude";
    public const string Mob8504 = "Third Party";
    public const string Mob8505 = "Tudor Garden";
    public const string Mob8506 = "Ribbon Threnody";
    public const string Mob8507 = "Masquerade Eye";
    public const string Mob8508 = "Oishii Parfait";
    public const string Mob8509 = "Irresistible";
    public const string Mob8510 = "Xbalanque";
    public const string Mob8511 = "Dunna";
    public const string Mob8512 = "Hard Control";
    public const string Mob8513 = "Chief Purser";
    public const string Mob8514 = "Kumbhakarna";
    public const string Mob8515 = "Duo Januwiyah";
    public const string Mob8516 = "Pristine Song";
    public const string Mob8517 = "Reed Novel";
    public const string Mob8518 = "Khaki Chouchou";
    public const string Mob8519 = "Cosmo Scraper";
    public const string Mob8520 = "Shorty Shot";
    public const string Mob8521 = "Frilled Berry";
    public const string Mob8522 = "Mini Daisy";
    public const string Mob8523 = "Let's Jump";
    public const string Mob8524 = "Chronicle Oath";
    public const string Mob8525 = "Red Chouchou";
    public const string Mob8526 = "Jewel Emerald";
    public const string Mob8527 = "Castanet Rhythm";
    public const string Mob8528 = "Horn Rhythm";
    public const string Mob8529 = "On Stage Revue";
    public const string Mob8530 = "Wiseman Rays";
    public const string Mob8531 = "Duo Tariqah";
    public const string Mob8532 = "Wakuwaku Ribbon";
    public const string Mob8533 = "Compromise";
    public const string Mob8534 = "Cheer Rhythm";
    public const string Mob8535 = "Ogress";
    public const string Mob8536 = "Beta Cubism";
    public const string Mob8537 = "Bravo Second";
    public const string Mob8538 = "Boom A Bang";
    public const string Mob8539 = "Slaine";
    public const string Mob8540 = "Heart Seizer";
    public const string Mob8541 = "Cembalo Rhythm";
    public const string Mob8542 = "Indigo Chouchou";
    public const string Mob8543 = "Ribbon Ballad";
    public const string Mob8544 = "Bonheur Sonata";
    public const string Mob8545 = "Green Chouchou";
    public const string Mob8546 = "Kinderschatz";
    public const string Mob8547 = "Haste Fire";
    public const string Mob8548 = "Spain Gelato";
    public const string Mob8549 = "Domiziana";
    public const string Mob8550 = "Cravat";
    public const string Mob8551 = "Gold Chouchou";
    public const string Mob8552 = "Hydro Chop";
    public const string Mob8553 = "Mini Lotus";
    public const string Mob8554 = "Colosseo Fight";
    public const string Mob8555 = "Astraea Noche";
    public const string Mob8556 = "Marine Seagull";
    public const string Mob8557 = "Straight Bullet";
    public const string Mob8558 = "Jewel Sapphire";
    public const string Mob8559 = "Maritime Shipper";
    public const string Mob8560 = "Feudal Tenure";
    public const string Mob8561 = "Blitz Eclaire";
    public const string Mob8562 = "Frilled Cherry";
    public const string Mob8563 = "Moon Pop";
    public const string Mob8564 = "Rye Field";
    public const string Mob8565 = "Luminous Escudo";
    public const string Mob8566 = "Jewel Malachite";
    public const string Mob8567 = "Indian Breath";
    public const string Mob8568 = "Reed S.F.";
    public const string Mob8569 = "Phoenicia Deal";
    public const string Mob8570 = "Dragoon Spear";
    public const string Mob8571 = "Sunny Weather";
    public const string Mob8572 = "Yggdra Valley";
    public const string Mob8573 = "Ephemeron";
    public const string Mob8574 = "Dark Grimoire";
    public const string Mob8575 = "Spring Happy";
    public const string Mob8576 = "Insight Catch";
    public const string Mob8577 = "Akinakes";
    public const string Mob8578 = "Fife Rhythm";
    public const string Mob8579 = "Turbo Detonator";
    public const string Mob8580 = "Unison Flag";
    public const string Mob8581 = "Beam Of Love";
    public const string Mob8582 = "Paraiso Sky";
    public const string Mob8583 = "Heart Of Sweet";
    public const string Mob8584 = "Anima Animus";
    public const string Mob8585 = "Aqua Lake";
    public const string Mob8586 = "Hula Halau";
    public const string Mob8587 = "Brooklyn Isle";
    public const string Mob8588 = "Reed Essay";
    public const string Mob8589 = "Eastern Diner";
    public const string Mob8590 = "Missing Nights";
    public const string Mob8591 = "Chemical Wash";
    public const string Mob8592 = "Noir Grimoire";
    public const string Mob8593 = "Slow Motion";
    public const string Mob8594 = "Azalea Bonheur";
    public const string Mob8595 = "Heart Scorcher";
    public const string Mob8596 = "Ribbon Scherzo";
    public const string Mob8597 = "Jewel Coral";
    public const string Mob8598 = "Ribbon Paean";
    public const string Mob8599 = "Merciless Queen";
    public const string Mob8600 = "Soldar Passione";
    public const string Mob8601 = "State of Art";
    public const string Mob8602 = "Dukedom Poppy";
    public const string Mob8603 = "Paladin Sword";
    public const string Mob8604 = "Dianthus Bouton";
    public const string Mob8605 = "Daddy's Boots";
    public const string Mob8606 = "Lovely Patricia";
    public const string Mob8607 = "Make You Gasp";
    public const string Mob8608 = "Virtue Mind";
    public const string Mob8609 = "Energetic";
    public const string Mob8610 = "Brusquement";
    public const string Mob8611 = "Suger Nymph";
    public const string Mob8612 = "Harp Alpha";
    public const string Mob8613 = "Shiina Fréjus";
    public const string Mob8614 = "Nemo Tiara";
    public const string Mob8615 = "Shine Praise";
    public const string Mob8616 = "Aurum Star";
    public const string Mob8617 = "Peerless Shout";
    public const string Mob8618 = "Tsuzuki Reigning";
    public const string Mob9001 = "Schildpatt";
    public const string Mob9002 = "Blutstein";
    public const string Mob9003 = "Herbstlaub";
    public const string Mob9004 = "Wasserlilie";
    public const string Mob9005 = "Hochdruck";
    public const string Mob9006 = "Gefrieren";
    public const string Mob9007 = "Lippenstift";
    public const string Mob9008 = "Lidschatten";
    public const string Mob9009 = "Sehnsucht";
    public const string Mob9010 = "Schwiegsam";
    public const string Mob9011 = "Ametista";
    public const string Mob9012 = "Sardonica";
    public const string Mob9013 = "Albicocco";
    public const string Mob9014 = "Giaggiolo";
    public const string Mob9015 = "Umidità";
    public const string Mob9016 = "Altopiano";
    public const string Mob9017 = "Orecchino";
    public const string Mob9018 = "Insalata";
    public const string Mob9019 = "Birichinata";
    public const string Mob9020 = "Missione";
    public const string Mob9021 = "Scintillement";
    public const string Mob9022 = "Entêtement";
    public const string Mob9023 = "Nuage";
    public const string Mob9024 = "Saturne";
    public const string Mob9025 = "Pluton";
    public const string Mob9026 = "Sambol";
    public const string Mob9027 = "Mallarmé";
    public const string Mob9028 = "Éluard";
    public const string Mob9029 = "Aujourd'hui";
    public const string Mob9030 = "La Foudre";
    public const string Mob9031 = "Olivier Odorant";
    public const string Mob9032 = "Alkekenge";
    public const string Mob9033 = "Lutwidge";
    public const string Mob9034 = "Proms";
    public const string Mob9035 = "Bors";
    public const string Mob9036 = "Arestans";
    public const string Mob9037 = "Reynardine";
    public const string Mob9038 = "Eneuawc";
    public const string Mob9039 = "Killaraus";
    public const string Mob9040 = "Gwendolen";
    public const string Mob9041 = "Scuin";
    public const string Mob9042 = "Sea Tangle";
    public const string Mob9043 = "Shadhavar";
    public const string Mob9044 = "Shams Alnahar";
    public const string Mob9045 = "Aziza";
    public const string Mob9046 = "Jharia";
    public const string Mob9047 = "Sauber";
    public const string Mob9048 = "Tariq Almajd";
    public const string Mob9049 = "Rodina";
    public const string Mob9050 = "Izdihar";
    public const string Mob9051 = "Murjaana";
    public const string Mob9052 = "Najwā";
    public const string Mob9053 = "Clicker";
    public const string Mob9054 = "Chillwave";
    public const string Mob9055 = "Immovable";
    public const string Mob9056 = "Verity Talker";
    public const string Mob9057 = "Minder";
    public const string Mob9058 = "Leah Kinsley";
    public const string Mob9059 = "Peace Sticker";
    public const string Mob9060 = "Bunch of Fun";
    public const string Mob9061 = "Unlock the Key";
    public const string Mob9100 = "Copal Eye";
    public const string Mob9101 = "Calm Sea";
    public const string Mob9102 = "Nobile Cavaliere";
    public const string Mob9103 = "Minty Berry";
    public const string Mob9104 = "Vine Blossom";
    public const string Mob9105 = "Emerald Mace";
    public const string Mob9106 = "Star Searcher";
    public const string Mob9107 = "Windy Daisy";
    public const string Mob9108 = "Artemis Comet";
    public const string Mob9109 = "Maschera Potente";
    public const string Mob9110 = "Lively Oasis";
    public const string Mob9111 = "Loyal Oslo";
    public const string Mob9112 = "Detroit Metro";
    public const string Mob9113 = "Witty Riddler";
    public const string Mob9114 = "Vegas Mist";
    public const string Mob9115 = "Energie Parme";
    public const string Mob9116 = "Somnus Domina";
    public const string Mob9117 = "Cramoisi Rigoureux";
    public const string Mob9118 = "Brama Coraggio";
    public const string Mob9119 = "Serene Indigo";
    public const string Mob9120 = "Floral Zeal";
    public const string Mob9121 = "Wisteria Dome";
    public const string Mob9122 = "Belle Peridot";
    public const string Mob9123 = "Petite Carotte";
    public const string Mob9124 = "Nova Seeker";
    public const string Mob9125 = "Iris Peach";
    public const string Mob9126 = "Fulmine Viola";
    public const string Mob9127 = "Citrus Crown";
    public const string Mob9128 = "Naranja Viva";
    public const string Mob9129 = "Polaris Light";
    public const string Mob9130 = "Heldenmut";
    public const string Mob9131 = "Etoile Papillon";
    public const string Mob9132 = "Coral Spark";
    public const string Mob9133 = "Rosso Fuoco";
    public const string Mob9134 = "Top Scholar";
    public const string Mob9135 = "Lotus Princess";
    public const string Mob9136 = "Librarius";
    public const string Mob9137 = "Aurum Ruby";
    public const string Mob9138 = "Dawn Smasher";
    public const string Mob9139 = "Magic Liana";
    public const string Mob9140 = "Ice Quartz";
    public const string Mob9141 = "Sunlight Road";
    public const string Mob9142 = "Rosetta Lince";
    public const string Mob9143 = "Freedom Fire";
    public const string Mob9144 = "Forschung";
    public const string Mob9145 = "Noble Esprit";
    public const string Mob9146 = "Sol Aureo";
    public const string Mob9147 = "Mirage Soul";
    public const string Mob9148 = "Shiny Mimosa";
    public const string Mob9149 = "Star Pride";
    public const string Mob9150 = "Dunkelblau";
    public const string Mob9151 = "Check the Route";
    public const string Mob9152 = "Little Vamp";
    public const string Mob9153 = "Pastel Mist";
    public const string Mob9154 = "Ignis Magnus";
    public const string Mob9155 = "Silberkrone";
    public const string Mob9156 = "Mountain Keeper";
    public const string Mob9157 = "Phantom Doll";
    public const string Mob9158 = "Roulette Eye";
    public const string Mob9159 = "Psychic Stun";
    public const string Mob9160 = "Solar White";
    public const string Mob9161 = "Autumn Soprano";
    public const string Mob9162 = "Sommerfluss";
    public const string Mob9163 = "Rainy Season";
    public const string Mob9164 = "Neo Horizon";
    public const string Mob9165 = "Coast Map";
    public const string Mob9166 = "Prairie Knight";
    public const string Mob9167 = "Petunia Parasol";
    public const string Mob9168 = "Luminous Iris";
    public const string Mob9169 = "Winery Betty";
    public const string Mob9170 = "Sahara Oasis";
    public const string Mob9171 = "Andes Mystery";
    public const string Mob9172 = "Marshmallow Owl";
    public const string Mob9173 = "Glacier Maroon";
    public const string Mob9174 = "Pathmaker";
    public const string Mob9175 = "Admiral Navy";
    public const string Mob9176 = "Crystal Boots";
    public const string Mob9177 = "Wie der Blitz";
    public const string Mob9178 = "Lavender Cologne";
    public const string Mob9179 = "Dahlia's Whisper";
    public const string Mob9180 = "Herald of Honor";
    public const string Mob9181 = "Noble Dahlia";
    public const string Mob9182 = "Tropical Python";
    public const string Mob9183 = "Acquario Blu";
    public const string Mob9184 = "Bright Wisp";
    public const string Mob9185 = "Moonveil";
    public const string Mob9186 = "Funky Park";
    public const string Mob9187 = "Vivid Thistle";
    public const string Mob9188 = "Pumpkin Town";
    public const string Mob9189 = "Geisterbahn";
    public const string Mob9190 = "Discomeister";
    public const string Mob10001 = "Again Needed";
    public const string Mob10002 = "Kakushiba Gulliver";
    public const string Mob10003 = "Daikou Kunsh";
    public const string Mob10004 = "Carribean Music";
    public const string Mob10005 = "Elmi Fly";
    public const string Mob10006 = "Eternal Road";
    public const string Mob10007 = "Shiroiro Stone";
    public const string Mob10008 = "Monsieur Family";
    public const string Mob10009 = "Kagoshima Oyashiro";
    public const string Mob10010 = "Maymon Palmer";
    public const string Mob10011 = "Tui Condor";
    public const string Mob10012 = "Victory Dragoon";
    public const string Mob10013 = "Mist Sancy";
    public const string Mob10014 = "Azuma Matsushiba";
    public const string Mob10015 = "Monsieur Therapy";
    public const string Mob10016 = "Kaku Kallocki";
    public const string Mob10017 = "Good Good Good";
    public const string Mob10018 = "Nokami Crescendo";
    public const string Mob10019 = "Monsieur Choppy";
    public const string Mob10020 = "Shiroiro Arrow";
    public const string Mob10021 = "Gimme Maxtor";
    public const string Mob10022 = "Maymon Marchese";
    public const string Mob10023 = "Top Vire";
    public const string Mob10024 = "Red Pine Victoria";
    public const string Mob10025 = "White Beauty";
    public const string Mob10026 = "Femme Girl";
    public const string Mob10027 = "Monsieur Cyclamen";
    public const string Mob10028 = "Joryu Top";
    public const string Mob10029 = "Resurrection";
    public const string Mob10030 = "Neos Fujimasa";
    public const string Mob10031 = "Song Time";
    public const string Mob10032 = "Tsuchii Global";
    public const string Mob10033 = "Higher Solar";
    public const string Mob10034 = "Center Cast";
    public const string Mob10035 = "Dai Yushun";
    public const string Mob10036 = "Fuji Sword";
    public const string Mob10037 = "King Shin";
    public const string Mob10038 = "Duo Jet";
    public const string Mob10039 = "Choichi George";
    public const string Mob10052 = "Hachieno Star";
    public const string Mob10053 = "Pasqual";
    public const string Mob10054 = "Sunano Lady";
    public const string Mob10055 = "Maymon Ardan";
    public const string Mob10056 = "World Tap";
    public const string Mob10057 = "True Birthday";
    public const string Mob10058 = "Girls Sunny";
    public const string Mob10059 = "Gogo Message";
    public const string Mob10060 = "Win Win Hawk";
    public const string Mob10061 = "Squash Ball";
    public const string Mob10062 = "Myou Taisei";
    public const string Mob10063 = "Foster Lord";
    public const string Mob10064 = "World Bogan";
    public const string Mob10065 = "Fresh Thunder";
    public const string Mob10066 = "Assembly Tesio";
    public const string Mob10067 = "Global Toughness";
    public const string Mob10068 = "Tsuchii Miracle";
    public const string Mob10069 = "Sasa Genesis";
    public const string Mob10070 = "Sankinotazou";
    public const string Mob10071 = "Akamatsu Center";
    public const string Mob10072 = "Miketsu Torch";
    public const string Mob10073 = "Tiger Rocket";
    public const string Mob10074 = "Toyonaka Heinrich";
    public const string Mob10075 = "Saint Singer";
    public const string Mob10076 = "Sloanes Draco";
    public const string Mob10077 = "Great Chief";
    public const string Mob10078 = "Saint Rightvire";
    public const string Mob10079 = "First Rung";
    public const string Mob10080 = "Unison Tenyo";
    public const string Mob10081 = "Azumanou Gon";
    public const string Mob10082 = "Sir Century";
    public const string Mob10083 = "Kiyono Velour";
    public const string Mob10084 = "Kin-Irono Hitomi";
    public const string Mob10085 = "History Friend";
    public const string Mob10086 = "Algamama";
    public const string Mob10087 = "Ef One Symbol";
    public const string Mob10088 = "Starlight Jam";
    public const string Mob10089 = "Femme Faire";
    public const string Mob10090 = "Unison Winner";
    public const string Mob10091 = "Orai Paradise";
    public const string Mob10092 = "Global Pole";
    public const string Mob10093 = "Attended Star";
    public const string Mob10094 = "Halo Mileage";
    public const string Mob10095 = "Kohaku Shishi";
    public const string Mob10096 = "Maymon Chagall";
    public const string Mob10097 = "Scene Master";
    public const string Mob10098 = "Bongo Enevre";
    public const string Mob10099 = "Mikasa Kid";
    public const string Mob10100 = "Cyclamen Charib";
    public const string Mob10101 = "Oran Peak";
    public const string Mob10102 = "Hind";
    public const string Mob10103 = "Minesimba";
    public const string Mob10104 = "Daynelle Rename";
    public const string Mob10105 = "Inspire Ovita";
    public const string Mob10106 = "Dimidi";
    public const string Mob10107 = "Mugi Top Runner";
    public const string Mob10108 = "Sata Point";
    public const string Mob10109 = "Haruiro Chitose";
    public const string Mob10110 = "MT Hurricane";
    public const string Mob10111 = "Connect Hat";
    public const string Mob10112 = "Kinugekko";
    public const string Mob10113 = "Kazu Typhoon";
    public const string Mob10114 = "Thanks Stage";
    public const string Mob10115 = "Iitoyo Drive";
    public const string Mob10116 = "Chariosky";
    public const string Mob10117 = "Tatsuno Zeal";
    public const string Mob10118 = "Haruiro Great";
    public const string Mob10119 = "Binza Oji";
    public const string Mob10120 = "Haruiro Glory";
    public const string Mob10121 = "Brother Seitei";
    public const string Mob10122 = "Global Mars";
    public const string Mob10123 = "Fujiken Watch";
    public const string Mob10124 = "Authority Dance";
    public const string Mob10125 = "Amatera Big";
    public const string Mob10126 = "Position Trap";
    public const string Mob10127 = "Tomiyama Victory";
    public const string Mob10128 = "Lightning Top";
    public const string Mob10129 = "Daynelle Thunder";
    public const string Mob10130 = "Maino Transcend";
    public const string Mob10131 = "Toyonaka Presto";
    public const string Mob10132 = "Yashu King";
    public const string Mob10133 = "Omar Caesar";
    public const string Mob10134 = "Crest Carol";
    public const string Mob10135 = "Pretty Steel";
    public const string Mob10136 = "Azumi Palace";
    public const string Mob10137 = "Polar Star";
    public const string Mob10138 = "Inspier Kent";
    public const string Mob10139 = "Rule Model";
    public const string Mob10140 = "Aspaolo";
    public const string Mob10141 = "Respedeza Shin O";
    public const string Mob10142 = "Peaceful Cinema";
    public const string Mob10143 = "Kabuto Prince";
    public const string Mob10144 = "Lemon Neck";
    public const string Mob10145 = "Girls Guilsha";
    public const string Mob10146 = "Misohagi River";
    public const string Mob10147 = "Akamatsu Shock";
    public const string Mob10148 = "Gees Sharp";
    public const string Mob10149 = "Global Smart";
    public const string Mob10150 = "Flash Friend";
    public const string Mob10151 = "Two Rhythm King";
    public const string Mob10152 = "Good Sweets";
    public const string Mob10153 = "Higher Doctor";
    public const string Mob10154 = "Nanson Sleuth";
    public const string Mob10155 = "Hansho Warger";
    public const string Mob10156 = "Taiju Golden";
    public const string Mob10157 = "Orient Seven";
    public const string Mob10158 = "MT Storm";
    public const string Mob10159 = "Akamatsu Hachiman";
    public const string Mob10160 = "Platinum OG";
    public const string Mob10162 = "Rose Knight";
    public const string Mob10163 = "Verdia Gogo";
    public const string Mob10164 = "Mute Hant";
    public const string Mob10165 = "Hinode Flag";
    public const string Mob10166 = "Hero Keat";
    public const string Mob10167 = "Remain Eldora";
    public const string Mob10168 = "Sonic Run";
    public const string Mob10169 = "Bishoku Kaitaku";
    public const string Mob10170 = "Constructor";
    public const string Mob10171 = "Denan Return";
    public const string Mob10172 = "Taiju Touraku";
    public const string Mob10173 = "Luisillo";
    public const string Mob10174 = "Bright Marker";
    public const string Mob10175 = "Erumi Warrior";
    public const string Mob10176 = "Maymon Lambert";
    public const string Mob10177 = "Iki-Iki Middle";
    public const string Mob10178 = "Manpai Emperor";
    public const string Mob10179 = "Sentiment";
    public const string Mob10180 = "Espace";
    public const string Mob10181 = "Oowa Advance";
    public const string Mob10182 = "Brave Emperor";
    public const string Mob10183 = "Holy Gleam";
    public const string Mob10184 = "Runner Typhoon";
    public const string Mob10185 = "Divinization";
    public const string Mob10186 = "Koumatsu Inazuma";
    public const string Mob10187 = "Marine Holiday";
    public const string Mob10188 = "Takeoff Star";
    public const string Mob10189 = "Jet Tail";
    public const string Mob10190 = "Sata Bright";
    public const string Mob10191 = "Matsuhiro Chief";
    public const string Mob10192 = "Flag-flag";
    public const string Mob10193 = "Hanagata Party";
    public const string Mob10194 = "West Boss";
    public const string Mob10195 = "TM Koutei";
    public const string Mob10196 = "Dahlia";
    public const string Mob10197 = "Terpsichore";
    public const string Mob10198 = "Toraoka";
    public const string Mob10199 = "Hulama Way";
    public const string Mob10200 = "Rain";
    public const string Mob10201 = "Vivid Blue";
    public const string Mob10202 = "Demira";
    public const string Mob10203 = "Bolu Blue";
    public const string Mob10204 = "Ambaral";
    public const string Mob10205 = "Light Vision";
    public const string Mob10206 = "Nile Lip";
    public const string Mob10207 = "Plum Collect";
    public const string Mob10208 = "Chew Me More";
    public const string Mob10209 = "Mint Shot";
    public const string Mob10210 = "Yancha Verdia";
    public const string Mob10211 = "Native Hero";
    public const string Mob10212 = "Heartfelt Fruit";
    public const string Mob10213 = "Top of Rise";
    public const string Mob10214 = "Anthra Glass";
    public const string Mob10215 = "Elirast";
    public const string Mob10216 = "Godly Sail";
    public const string Mob10218 = "Umbro Mignon";
    public const string Mob10219 = "Miyagi No Bijin";
    public const string Mob10220 = "M.T. Nangoku";
    public const string Mob10221 = "Blast Happy";
    public const string Mob10223 = "Kore Tte Kiseki";
    public const string Mob10224 = "Large Side";
    public const string Mob10225 = "Koi No Hana";
    public const string Mob10226 = "Marilyn Silver";
    public const string Mob10227 = "Big Bang Wonder";
    public const string Mob10229 = "Cotton Girl";
    public const string Mob10230 = "Admikami";
    public const string Mob10231 = "Pond Recorder";
    public const string Mob10232 = "Mine Terracotta";
    public const string Mob10233 = "Oshiro No An";
    public const string Mob10234 = "Dandan Honey";
    public const string Mob10235 = "Eino Soft";
    public const string Mob10236 = "Ruri Hinagiku";
    public const string Mob10237 = "Guren Fencer";
    public const string Mob10238 = "French Style";
    public const string Mob10239 = "Tensei Ga Terasu";
    public const string Mob10240 = "All Love";
    public const string Mob10241 = "Miminari Raijin";
    public const string Mob10242 = "Star Millefeuille";
    public const string Mob10243 = "Mars Plan";
    public const string Mob10244 = "Shining Bubble";
    public const string Mob10245 = "Summer Storm";
    public const string Mob10246 = "Cotton Power";
    public const string Mob10247 = "Lightning Hearts";
    public const string Mob10248 = "Lao Media";
    public const string Mob10249 = "Master Gryphon";
    public const string Mob10250 = "Shitorashi Train";
    public const string Mob10251 = "Dia Dia";
    public const string Mob10252 = "Aura Break";
    public const string Mob20000 = "Long-haired Horsegirl";
    public const string Mob20001 = "Dark bay-haired Horsegirl";
    public const string Mob20002 = "Glasses-wearing Horsegirl";
    public const string Mob20003 = "Tenacious Horsegirl";
    public const string Mob20004 = "Dignified Horsegirl";
    public const string Mob20005 = "Confident Horsegirl";
    public const string Mob20006 = "Bold Horsegirl";
    public const string Mob20007 = "Naughty Horsegirl";
    public const string Mob20008 = "Petite Horsegirl";
    public const string Mob20009 = "Seal Brown-haired Horsegirl";
    public const string Mob20010 = "Fawn-haired Horsegirl";

    public static string GetCharaName(int charaId, string fallbackName = "")
    {
        if (RuntimeCharaNames.TryGetValue(charaId, out string runtimeValue))
            return runtimeValue;

        switch (charaId)
        {
            case 1001:
                return Chara1001;
            case 1002:
                return Chara1002;
            case 1003:
                return Chara1003;
            case 1004:
                return Chara1004;
            case 1005:
                return Chara1005;
            case 1006:
                return Chara1006;
            case 1007:
                return Chara1007;
            case 1008:
                return Chara1008;
            case 1009:
                return Chara1009;
            case 1010:
                return Chara1010;
            case 1011:
                return Chara1011;
            case 1012:
                return Chara1012;
            case 1013:
                return Chara1013;
            case 1014:
                return Chara1014;
            case 1015:
                return Chara1015;
            case 1016:
                return Chara1016;
            case 1017:
                return Chara1017;
            case 1018:
                return Chara1018;
            case 1019:
                return Chara1019;
            case 1020:
                return Chara1020;
            case 1021:
                return Chara1021;
            case 1022:
                return Chara1022;
            case 1023:
                return Chara1023;
            case 1024:
                return Chara1024;
            case 1025:
                return Chara1025;
            case 1026:
                return Chara1026;
            case 1027:
                return Chara1027;
            case 1028:
                return Chara1028;
            case 1029:
                return Chara1029;
            case 1030:
                return Chara1030;
            case 1031:
                return Chara1031;
            case 1032:
                return Chara1032;
            case 1033:
                return Chara1033;
            case 1034:
                return Chara1034;
            case 1035:
                return Chara1035;
            case 1036:
                return Chara1036;
            case 1037:
                return Chara1037;
            case 1038:
                return Chara1038;
            case 1039:
                return Chara1039;
            case 1040:
                return Chara1040;
            case 1041:
                return Chara1041;
            case 1042:
                return Chara1042;
            case 1043:
                return Chara1043;
            case 1044:
                return Chara1044;
            case 1045:
                return Chara1045;
            case 1046:
                return Chara1046;
            case 1047:
                return Chara1047;
            case 1048:
                return Chara1048;
            case 1049:
                return Chara1049;
            case 1050:
                return Chara1050;
            case 1051:
                return Chara1051;
            case 1052:
                return Chara1052;
            case 1053:
                return Chara1053;
            case 1054:
                return Chara1054;
            case 1055:
                return Chara1055;
            case 1056:
                return Chara1056;
            case 1057:
                return Chara1057;
            case 1058:
                return Chara1058;
            case 1059:
                return Chara1059;
            case 1060:
                return Chara1060;
            case 1061:
                return Chara1061;
            case 1062:
                return Chara1062;
            case 1063:
                return Chara1063;
            case 1064:
                return Chara1064;
            case 1065:
                return Chara1065;
            case 1066:
                return Chara1066;
            case 1067:
                return Chara1067;
            case 1068:
                return Chara1068;
            case 1069:
                return Chara1069;
            case 1070:
                return Chara1070;
            case 1071:
                return Chara1071;
            case 1072:
                return Chara1072;
            case 1073:
                return Chara1073;
            case 1074:
                return Chara1074;
            case 1075:
                return Chara1075;
            case 1076:
                return Chara1076;
            case 1077:
                return Chara1077;
            case 1078:
                return Chara1078;
            case 1079:
                return Chara1079;
            case 1080:
                return Chara1080;
            case 1081:
                return Chara1081;
            case 1082:
                return Chara1082;
            case 1083:
                return Chara1083;
            case 1084:
                return Chara1084;
            case 1085:
                return Chara1085;
            case 1086:
                return Chara1086;
            case 1087:
                return Chara1087;
            case 1088:
                return Chara1088;
            case 1089:
                return Chara1089;
            case 1090:
                return Chara1090;
            case 1091:
                return Chara1091;
            case 1092:
                return Chara1092;
            case 1093:
                return Chara1093;
            case 1094:
                return Chara1094;
            case 1095:
                return Chara1095;
            case 1096:
                return Chara1096;
            case 1097:
                return Chara1097;
            case 1098:
                return Chara1098;
            case 1099:
                return Chara1099;
            case 1100:
                return Chara1100;
            case 1102:
                return Chara1102;
            case 1103:
                return Chara1103;
            case 1104:
                return Chara1104;
            case 1105:
                return Chara1105;
            case 1106:
                return Chara1106;
            case 1107:
                return Chara1107;
            case 1108:
                return Chara1108;
            case 1109:
                return Chara1109;
            case 1110:
                return Chara1110;
            case 1111:
                return Chara1111;
            case 1112:
                return Chara1112;
            case 1113:
                return Chara1113;
            case 1114:
                return Chara1114;
            case 1115:
                return Chara1115;
            case 1116:
                return Chara1116;
            case 1117:
                return Chara1117;
            case 1118:
                return Chara1118;
            case 1119:
                return Chara1119;
            case 1120:
                return Chara1120;
            case 1121:
                return Chara1121;
            case 1124:
                return Chara1124;
            case 1126:
                return Chara1126;
            case 1127:
                return Chara1127;
            case 1128:
                return Chara1128;
            case 1129:
                return Chara1129;
            case 1130:
                return Chara1130;
            case 1131:
                return Chara1131;
            case 1132:
                return Chara1132;
            case 1133:
                return Chara1133;
            case 1134:
                return Chara1134;
            case 1135:
                return Chara1135;
            case 1136:
                return Chara1136;
            case 1137:
                return Chara1137;
            case 1138:
                return Chara1138;
            case 1139:
                return Chara1139;
            case 1140:
                return Chara1140;
            case 1141:
                return Chara1141;
            case 1142:
                return Chara1142;
            case 1143:
                return Chara1143;
            case 1144:
                return Chara1144;
            case 1145:
                return Chara1145;
            case 2001:
                return Chara2001;
            case 2002:
                return Chara2002;
            case 2003:
                return Chara2003;
            case 2004:
                return Chara2004;
            case 2005:
                return Chara2005;
            case 2006:
                return Chara2006;
            case 2007:
                return Chara2007;
            case 2008:
                return Chara2008;
            case 9001:
                return Chara9001;
            case 9002:
                return Chara9002;
            case 9003:
                return Chara9003;
            case 9004:
                return Chara9004;
            case 9005:
                return Chara9005;
            case 9006:
                return Chara9006;
            case 9007:
                return Chara9007;
            case 9008:
                return Chara9008;
            case 9040:
                return Chara9040;
            case 9041:
                return Chara9041;
            case 9042:
                return Chara9042;
            case 9043:
                return Chara9043;
            case 9044:
                return Chara9044;
            case 9045:
                return Chara9045;
            case 9046:
                return Chara9046;
            case 9047:
                return Chara9047;
            case 9048:
                return Chara9048;
            case 9049:
                return Chara9049;
            case 9050:
                return Chara9050;
            case 9051:
                return Chara9051;

            default:
                return fallbackName ?? string.Empty;
        }
    }

    public static string GetMobName(int mobId, string fallbackName = "")
    {
        if (RuntimeMobNames.TryGetValue(mobId, out string runtimeValue))
            return runtimeValue;

        switch (mobId)
        {
            case 8000:
                return Mob8000;
            case 8001:
                return Mob8001;
            case 8002:
                return Mob8002;
            case 8003:
                return Mob8003;
            case 8004:
                return Mob8004;
            case 8005:
                return Mob8005;
            case 8006:
                return Mob8006;
            case 8007:
                return Mob8007;
            case 8008:
                return Mob8008;
            case 8009:
                return Mob8009;
            case 8010:
                return Mob8010;
            case 8011:
                return Mob8011;
            case 8012:
                return Mob8012;
            case 8013:
                return Mob8013;
            case 8014:
                return Mob8014;
            case 8015:
                return Mob8015;
            case 8016:
                return Mob8016;
            case 8017:
                return Mob8017;
            case 8018:
                return Mob8018;
            case 8019:
                return Mob8019;
            case 8020:
                return Mob8020;
            case 8021:
                return Mob8021;
            case 8022:
                return Mob8022;
            case 8023:
                return Mob8023;
            case 8024:
                return Mob8024;
            case 8025:
                return Mob8025;
            case 8026:
                return Mob8026;
            case 8027:
                return Mob8027;
            case 8028:
                return Mob8028;
            case 8029:
                return Mob8029;
            case 8030:
                return Mob8030;
            case 8031:
                return Mob8031;
            case 8032:
                return Mob8032;
            case 8033:
                return Mob8033;
            case 8034:
                return Mob8034;
            case 8035:
                return Mob8035;
            case 8036:
                return Mob8036;
            case 8037:
                return Mob8037;
            case 8038:
                return Mob8038;
            case 8039:
                return Mob8039;
            case 8040:
                return Mob8040;
            case 8041:
                return Mob8041;
            case 8042:
                return Mob8042;
            case 8043:
                return Mob8043;
            case 8044:
                return Mob8044;
            case 8045:
                return Mob8045;
            case 8046:
                return Mob8046;
            case 8047:
                return Mob8047;
            case 8048:
                return Mob8048;
            case 8049:
                return Mob8049;
            case 8050:
                return Mob8050;
            case 8051:
                return Mob8051;
            case 8052:
                return Mob8052;
            case 8053:
                return Mob8053;
            case 8054:
                return Mob8054;
            case 8055:
                return Mob8055;
            case 8056:
                return Mob8056;
            case 8057:
                return Mob8057;
            case 8058:
                return Mob8058;
            case 8059:
                return Mob8059;
            case 8060:
                return Mob8060;
            case 8061:
                return Mob8061;
            case 8062:
                return Mob8062;
            case 8063:
                return Mob8063;
            case 8064:
                return Mob8064;
            case 8065:
                return Mob8065;
            case 8066:
                return Mob8066;
            case 8067:
                return Mob8067;
            case 8068:
                return Mob8068;
            case 8069:
                return Mob8069;
            case 8070:
                return Mob8070;
            case 8071:
                return Mob8071;
            case 8072:
                return Mob8072;
            case 8073:
                return Mob8073;
            case 8074:
                return Mob8074;
            case 8075:
                return Mob8075;
            case 8076:
                return Mob8076;
            case 8077:
                return Mob8077;
            case 8078:
                return Mob8078;
            case 8079:
                return Mob8079;
            case 8080:
                return Mob8080;
            case 8081:
                return Mob8081;
            case 8082:
                return Mob8082;
            case 8083:
                return Mob8083;
            case 8084:
                return Mob8084;
            case 8085:
                return Mob8085;
            case 8086:
                return Mob8086;
            case 8087:
                return Mob8087;
            case 8088:
                return Mob8088;
            case 8089:
                return Mob8089;
            case 8090:
                return Mob8090;
            case 8091:
                return Mob8091;
            case 8092:
                return Mob8092;
            case 8093:
                return Mob8093;
            case 8094:
                return Mob8094;
            case 8095:
                return Mob8095;
            case 8096:
                return Mob8096;
            case 8097:
                return Mob8097;
            case 8098:
                return Mob8098;
            case 8099:
                return Mob8099;
            case 8100:
                return Mob8100;
            case 8101:
                return Mob8101;
            case 8102:
                return Mob8102;
            case 8103:
                return Mob8103;
            case 8104:
                return Mob8104;
            case 8105:
                return Mob8105;
            case 8106:
                return Mob8106;
            case 8107:
                return Mob8107;
            case 8108:
                return Mob8108;
            case 8109:
                return Mob8109;
            case 8110:
                return Mob8110;
            case 8111:
                return Mob8111;
            case 8112:
                return Mob8112;
            case 8113:
                return Mob8113;
            case 8114:
                return Mob8114;
            case 8115:
                return Mob8115;
            case 8116:
                return Mob8116;
            case 8117:
                return Mob8117;
            case 8118:
                return Mob8118;
            case 8119:
                return Mob8119;
            case 8120:
                return Mob8120;
            case 8121:
                return Mob8121;
            case 8122:
                return Mob8122;
            case 8123:
                return Mob8123;
            case 8124:
                return Mob8124;
            case 8125:
                return Mob8125;
            case 8126:
                return Mob8126;
            case 8127:
                return Mob8127;
            case 8128:
                return Mob8128;
            case 8129:
                return Mob8129;
            case 8130:
                return Mob8130;
            case 8131:
                return Mob8131;
            case 8132:
                return Mob8132;
            case 8133:
                return Mob8133;
            case 8134:
                return Mob8134;
            case 8135:
                return Mob8135;
            case 8136:
                return Mob8136;
            case 8137:
                return Mob8137;
            case 8138:
                return Mob8138;
            case 8139:
                return Mob8139;
            case 8140:
                return Mob8140;
            case 8141:
                return Mob8141;
            case 8142:
                return Mob8142;
            case 8143:
                return Mob8143;
            case 8144:
                return Mob8144;
            case 8145:
                return Mob8145;
            case 8146:
                return Mob8146;
            case 8147:
                return Mob8147;
            case 8148:
                return Mob8148;
            case 8149:
                return Mob8149;
            case 8150:
                return Mob8150;
            case 8151:
                return Mob8151;
            case 8152:
                return Mob8152;
            case 8153:
                return Mob8153;
            case 8154:
                return Mob8154;
            case 8155:
                return Mob8155;
            case 8156:
                return Mob8156;
            case 8157:
                return Mob8157;
            case 8158:
                return Mob8158;
            case 8159:
                return Mob8159;
            case 8160:
                return Mob8160;
            case 8161:
                return Mob8161;
            case 8162:
                return Mob8162;
            case 8163:
                return Mob8163;
            case 8164:
                return Mob8164;
            case 8165:
                return Mob8165;
            case 8166:
                return Mob8166;
            case 8167:
                return Mob8167;
            case 8168:
                return Mob8168;
            case 8169:
                return Mob8169;
            case 8170:
                return Mob8170;
            case 8171:
                return Mob8171;
            case 8172:
                return Mob8172;
            case 8173:
                return Mob8173;
            case 8174:
                return Mob8174;
            case 8175:
                return Mob8175;
            case 8176:
                return Mob8176;
            case 8177:
                return Mob8177;
            case 8178:
                return Mob8178;
            case 8179:
                return Mob8179;
            case 8180:
                return Mob8180;
            case 8181:
                return Mob8181;
            case 8182:
                return Mob8182;
            case 8183:
                return Mob8183;
            case 8184:
                return Mob8184;
            case 8185:
                return Mob8185;
            case 8186:
                return Mob8186;
            case 8187:
                return Mob8187;
            case 8188:
                return Mob8188;
            case 8189:
                return Mob8189;
            case 8190:
                return Mob8190;
            case 8191:
                return Mob8191;
            case 8192:
                return Mob8192;
            case 8193:
                return Mob8193;
            case 8194:
                return Mob8194;
            case 8195:
                return Mob8195;
            case 8196:
                return Mob8196;
            case 8197:
                return Mob8197;
            case 8198:
                return Mob8198;
            case 8199:
                return Mob8199;
            case 8200:
                return Mob8200;
            case 8201:
                return Mob8201;
            case 8202:
                return Mob8202;
            case 8203:
                return Mob8203;
            case 8204:
                return Mob8204;
            case 8205:
                return Mob8205;
            case 8206:
                return Mob8206;
            case 8207:
                return Mob8207;
            case 8208:
                return Mob8208;
            case 8209:
                return Mob8209;
            case 8210:
                return Mob8210;
            case 8211:
                return Mob8211;
            case 8212:
                return Mob8212;
            case 8213:
                return Mob8213;
            case 8214:
                return Mob8214;
            case 8215:
                return Mob8215;
            case 8216:
                return Mob8216;
            case 8217:
                return Mob8217;
            case 8218:
                return Mob8218;
            case 8219:
                return Mob8219;
            case 8220:
                return Mob8220;
            case 8221:
                return Mob8221;
            case 8222:
                return Mob8222;
            case 8223:
                return Mob8223;
            case 8224:
                return Mob8224;
            case 8225:
                return Mob8225;
            case 8226:
                return Mob8226;
            case 8227:
                return Mob8227;
            case 8228:
                return Mob8228;
            case 8229:
                return Mob8229;
            case 8230:
                return Mob8230;
            case 8231:
                return Mob8231;
            case 8232:
                return Mob8232;
            case 8233:
                return Mob8233;
            case 8234:
                return Mob8234;
            case 8235:
                return Mob8235;
            case 8236:
                return Mob8236;
            case 8237:
                return Mob8237;
            case 8238:
                return Mob8238;
            case 8239:
                return Mob8239;
            case 8240:
                return Mob8240;
            case 8241:
                return Mob8241;
            case 8242:
                return Mob8242;
            case 8243:
                return Mob8243;
            case 8244:
                return Mob8244;
            case 8245:
                return Mob8245;
            case 8246:
                return Mob8246;
            case 8247:
                return Mob8247;
            case 8248:
                return Mob8248;
            case 8249:
                return Mob8249;
            case 8250:
                return Mob8250;
            case 8251:
                return Mob8251;
            case 8252:
                return Mob8252;
            case 8253:
                return Mob8253;
            case 8254:
                return Mob8254;
            case 8255:
                return Mob8255;
            case 8256:
                return Mob8256;
            case 8257:
                return Mob8257;
            case 8258:
                return Mob8258;
            case 8259:
                return Mob8259;
            case 8260:
                return Mob8260;
            case 8261:
                return Mob8261;
            case 8262:
                return Mob8262;
            case 8263:
                return Mob8263;
            case 8264:
                return Mob8264;
            case 8265:
                return Mob8265;
            case 8266:
                return Mob8266;
            case 8267:
                return Mob8267;
            case 8268:
                return Mob8268;
            case 8269:
                return Mob8269;
            case 8270:
                return Mob8270;
            case 8271:
                return Mob8271;
            case 8272:
                return Mob8272;
            case 8273:
                return Mob8273;
            case 8274:
                return Mob8274;
            case 8275:
                return Mob8275;
            case 8276:
                return Mob8276;
            case 8277:
                return Mob8277;
            case 8278:
                return Mob8278;
            case 8279:
                return Mob8279;
            case 8280:
                return Mob8280;
            case 8281:
                return Mob8281;
            case 8282:
                return Mob8282;
            case 8283:
                return Mob8283;
            case 8284:
                return Mob8284;
            case 8285:
                return Mob8285;
            case 8286:
                return Mob8286;
            case 8287:
                return Mob8287;
            case 8288:
                return Mob8288;
            case 8289:
                return Mob8289;
            case 8290:
                return Mob8290;
            case 8291:
                return Mob8291;
            case 8292:
                return Mob8292;
            case 8293:
                return Mob8293;
            case 8294:
                return Mob8294;
            case 8295:
                return Mob8295;
            case 8296:
                return Mob8296;
            case 8297:
                return Mob8297;
            case 8298:
                return Mob8298;
            case 8299:
                return Mob8299;
            case 8300:
                return Mob8300;
            case 8301:
                return Mob8301;
            case 8302:
                return Mob8302;
            case 8303:
                return Mob8303;
            case 8304:
                return Mob8304;
            case 8305:
                return Mob8305;
            case 8306:
                return Mob8306;
            case 8307:
                return Mob8307;
            case 8308:
                return Mob8308;
            case 8309:
                return Mob8309;
            case 8310:
                return Mob8310;
            case 8311:
                return Mob8311;
            case 8312:
                return Mob8312;
            case 8313:
                return Mob8313;
            case 8314:
                return Mob8314;
            case 8315:
                return Mob8315;
            case 8316:
                return Mob8316;
            case 8317:
                return Mob8317;
            case 8318:
                return Mob8318;
            case 8319:
                return Mob8319;
            case 8320:
                return Mob8320;
            case 8321:
                return Mob8321;
            case 8322:
                return Mob8322;
            case 8323:
                return Mob8323;
            case 8324:
                return Mob8324;
            case 8325:
                return Mob8325;
            case 8326:
                return Mob8326;
            case 8327:
                return Mob8327;
            case 8328:
                return Mob8328;
            case 8329:
                return Mob8329;
            case 8330:
                return Mob8330;
            case 8331:
                return Mob8331;
            case 8332:
                return Mob8332;
            case 8333:
                return Mob8333;
            case 8334:
                return Mob8334;
            case 8335:
                return Mob8335;
            case 8336:
                return Mob8336;
            case 8337:
                return Mob8337;
            case 8338:
                return Mob8338;
            case 8339:
                return Mob8339;
            case 8340:
                return Mob8340;
            case 8341:
                return Mob8341;
            case 8342:
                return Mob8342;
            case 8343:
                return Mob8343;
            case 8344:
                return Mob8344;
            case 8345:
                return Mob8345;
            case 8346:
                return Mob8346;
            case 8347:
                return Mob8347;
            case 8348:
                return Mob8348;
            case 8349:
                return Mob8349;
            case 8350:
                return Mob8350;
            case 8351:
                return Mob8351;
            case 8352:
                return Mob8352;
            case 8353:
                return Mob8353;
            case 8354:
                return Mob8354;
            case 8355:
                return Mob8355;
            case 8356:
                return Mob8356;
            case 8357:
                return Mob8357;
            case 8358:
                return Mob8358;
            case 8359:
                return Mob8359;
            case 8360:
                return Mob8360;
            case 8361:
                return Mob8361;
            case 8362:
                return Mob8362;
            case 8363:
                return Mob8363;
            case 8364:
                return Mob8364;
            case 8365:
                return Mob8365;
            case 8366:
                return Mob8366;
            case 8367:
                return Mob8367;
            case 8368:
                return Mob8368;
            case 8369:
                return Mob8369;
            case 8370:
                return Mob8370;
            case 8371:
                return Mob8371;
            case 8372:
                return Mob8372;
            case 8373:
                return Mob8373;
            case 8374:
                return Mob8374;
            case 8375:
                return Mob8375;
            case 8376:
                return Mob8376;
            case 8377:
                return Mob8377;
            case 8378:
                return Mob8378;
            case 8379:
                return Mob8379;
            case 8380:
                return Mob8380;
            case 8381:
                return Mob8381;
            case 8382:
                return Mob8382;
            case 8383:
                return Mob8383;
            case 8384:
                return Mob8384;
            case 8385:
                return Mob8385;
            case 8386:
                return Mob8386;
            case 8387:
                return Mob8387;
            case 8388:
                return Mob8388;
            case 8389:
                return Mob8389;
            case 8390:
                return Mob8390;
            case 8391:
                return Mob8391;
            case 8392:
                return Mob8392;
            case 8393:
                return Mob8393;
            case 8394:
                return Mob8394;
            case 8395:
                return Mob8395;
            case 8396:
                return Mob8396;
            case 8397:
                return Mob8397;
            case 8398:
                return Mob8398;
            case 8399:
                return Mob8399;
            case 8400:
                return Mob8400;
            case 8401:
                return Mob8401;
            case 8402:
                return Mob8402;
            case 8403:
                return Mob8403;
            case 8404:
                return Mob8404;
            case 8405:
                return Mob8405;
            case 8406:
                return Mob8406;
            case 8407:
                return Mob8407;
            case 8408:
                return Mob8408;
            case 8409:
                return Mob8409;
            case 8410:
                return Mob8410;
            case 8411:
                return Mob8411;
            case 8412:
                return Mob8412;
            case 8413:
                return Mob8413;
            case 8414:
                return Mob8414;
            case 8415:
                return Mob8415;
            case 8416:
                return Mob8416;
            case 8417:
                return Mob8417;
            case 8418:
                return Mob8418;
            case 8419:
                return Mob8419;
            case 8420:
                return Mob8420;
            case 8421:
                return Mob8421;
            case 8422:
                return Mob8422;
            case 8423:
                return Mob8423;
            case 8424:
                return Mob8424;
            case 8425:
                return Mob8425;
            case 8426:
                return Mob8426;
            case 8427:
                return Mob8427;
            case 8428:
                return Mob8428;
            case 8429:
                return Mob8429;
            case 8430:
                return Mob8430;
            case 8431:
                return Mob8431;
            case 8432:
                return Mob8432;
            case 8433:
                return Mob8433;
            case 8434:
                return Mob8434;
            case 8435:
                return Mob8435;
            case 8436:
                return Mob8436;
            case 8437:
                return Mob8437;
            case 8438:
                return Mob8438;
            case 8439:
                return Mob8439;
            case 8440:
                return Mob8440;
            case 8441:
                return Mob8441;
            case 8442:
                return Mob8442;
            case 8443:
                return Mob8443;
            case 8444:
                return Mob8444;
            case 8445:
                return Mob8445;
            case 8446:
                return Mob8446;
            case 8447:
                return Mob8447;
            case 8448:
                return Mob8448;
            case 8449:
                return Mob8449;
            case 8450:
                return Mob8450;
            case 8451:
                return Mob8451;
            case 8452:
                return Mob8452;
            case 8453:
                return Mob8453;
            case 8454:
                return Mob8454;
            case 8455:
                return Mob8455;
            case 8456:
                return Mob8456;
            case 8457:
                return Mob8457;
            case 8458:
                return Mob8458;
            case 8459:
                return Mob8459;
            case 8460:
                return Mob8460;
            case 8461:
                return Mob8461;
            case 8462:
                return Mob8462;
            case 8463:
                return Mob8463;
            case 8464:
                return Mob8464;
            case 8465:
                return Mob8465;
            case 8466:
                return Mob8466;
            case 8467:
                return Mob8467;
            case 8468:
                return Mob8468;
            case 8469:
                return Mob8469;
            case 8470:
                return Mob8470;
            case 8471:
                return Mob8471;
            case 8472:
                return Mob8472;
            case 8473:
                return Mob8473;
            case 8474:
                return Mob8474;
            case 8475:
                return Mob8475;
            case 8476:
                return Mob8476;
            case 8477:
                return Mob8477;
            case 8478:
                return Mob8478;
            case 8479:
                return Mob8479;
            case 8480:
                return Mob8480;
            case 8481:
                return Mob8481;
            case 8482:
                return Mob8482;
            case 8483:
                return Mob8483;
            case 8484:
                return Mob8484;
            case 8485:
                return Mob8485;
            case 8486:
                return Mob8486;
            case 8487:
                return Mob8487;
            case 8488:
                return Mob8488;
            case 8489:
                return Mob8489;
            case 8490:
                return Mob8490;
            case 8491:
                return Mob8491;
            case 8492:
                return Mob8492;
            case 8493:
                return Mob8493;
            case 8494:
                return Mob8494;
            case 8495:
                return Mob8495;
            case 8496:
                return Mob8496;
            case 8497:
                return Mob8497;
            case 8498:
                return Mob8498;
            case 8499:
                return Mob8499;
            case 8500:
                return Mob8500;
            case 8501:
                return Mob8501;
            case 8502:
                return Mob8502;
            case 8503:
                return Mob8503;
            case 8504:
                return Mob8504;
            case 8505:
                return Mob8505;
            case 8506:
                return Mob8506;
            case 8507:
                return Mob8507;
            case 8508:
                return Mob8508;
            case 8509:
                return Mob8509;
            case 8510:
                return Mob8510;
            case 8511:
                return Mob8511;
            case 8512:
                return Mob8512;
            case 8513:
                return Mob8513;
            case 8514:
                return Mob8514;
            case 8515:
                return Mob8515;
            case 8516:
                return Mob8516;
            case 8517:
                return Mob8517;
            case 8518:
                return Mob8518;
            case 8519:
                return Mob8519;
            case 8520:
                return Mob8520;
            case 8521:
                return Mob8521;
            case 8522:
                return Mob8522;
            case 8523:
                return Mob8523;
            case 8524:
                return Mob8524;
            case 8525:
                return Mob8525;
            case 8526:
                return Mob8526;
            case 8527:
                return Mob8527;
            case 8528:
                return Mob8528;
            case 8529:
                return Mob8529;
            case 8530:
                return Mob8530;
            case 8531:
                return Mob8531;
            case 8532:
                return Mob8532;
            case 8533:
                return Mob8533;
            case 8534:
                return Mob8534;
            case 8535:
                return Mob8535;
            case 8536:
                return Mob8536;
            case 8537:
                return Mob8537;
            case 8538:
                return Mob8538;
            case 8539:
                return Mob8539;
            case 8540:
                return Mob8540;
            case 8541:
                return Mob8541;
            case 8542:
                return Mob8542;
            case 8543:
                return Mob8543;
            case 8544:
                return Mob8544;
            case 8545:
                return Mob8545;
            case 8546:
                return Mob8546;
            case 8547:
                return Mob8547;
            case 8548:
                return Mob8548;
            case 8549:
                return Mob8549;
            case 8550:
                return Mob8550;
            case 8551:
                return Mob8551;
            case 8552:
                return Mob8552;
            case 8553:
                return Mob8553;
            case 8554:
                return Mob8554;
            case 8555:
                return Mob8555;
            case 8556:
                return Mob8556;
            case 8557:
                return Mob8557;
            case 8558:
                return Mob8558;
            case 8559:
                return Mob8559;
            case 8560:
                return Mob8560;
            case 8561:
                return Mob8561;
            case 8562:
                return Mob8562;
            case 8563:
                return Mob8563;
            case 8564:
                return Mob8564;
            case 8565:
                return Mob8565;
            case 8566:
                return Mob8566;
            case 8567:
                return Mob8567;
            case 8568:
                return Mob8568;
            case 8569:
                return Mob8569;
            case 8570:
                return Mob8570;
            case 8571:
                return Mob8571;
            case 8572:
                return Mob8572;
            case 8573:
                return Mob8573;
            case 8574:
                return Mob8574;
            case 8575:
                return Mob8575;
            case 8576:
                return Mob8576;
            case 8577:
                return Mob8577;
            case 8578:
                return Mob8578;
            case 8579:
                return Mob8579;
            case 8580:
                return Mob8580;
            case 8581:
                return Mob8581;
            case 8582:
                return Mob8582;
            case 8583:
                return Mob8583;
            case 8584:
                return Mob8584;
            case 8585:
                return Mob8585;
            case 8586:
                return Mob8586;
            case 8587:
                return Mob8587;
            case 8588:
                return Mob8588;
            case 8589:
                return Mob8589;
            case 8590:
                return Mob8590;
            case 8591:
                return Mob8591;
            case 8592:
                return Mob8592;
            case 8593:
                return Mob8593;
            case 8594:
                return Mob8594;
            case 8595:
                return Mob8595;
            case 8596:
                return Mob8596;
            case 8597:
                return Mob8597;
            case 8598:
                return Mob8598;
            case 8599:
                return Mob8599;
            case 8600:
                return Mob8600;
            case 8601:
                return Mob8601;
            case 8602:
                return Mob8602;
            case 8603:
                return Mob8603;
            case 8604:
                return Mob8604;
            case 8605:
                return Mob8605;
            case 8606:
                return Mob8606;
            case 8607:
                return Mob8607;
            case 8608:
                return Mob8608;
            case 8609:
                return Mob8609;
            case 8610:
                return Mob8610;
            case 8611:
                return Mob8611;
            case 8612:
                return Mob8612;
            case 8613:
                return Mob8613;
            case 8614:
                return Mob8614;
            case 8615:
                return Mob8615;
            case 8616:
                return Mob8616;
            case 8617:
                return Mob8617;
            case 8618:
                return Mob8618;
            case 9001:
                return Mob9001;
            case 9002:
                return Mob9002;
            case 9003:
                return Mob9003;
            case 9004:
                return Mob9004;
            case 9005:
                return Mob9005;
            case 9006:
                return Mob9006;
            case 9007:
                return Mob9007;
            case 9008:
                return Mob9008;
            case 9009:
                return Mob9009;
            case 9010:
                return Mob9010;
            case 9011:
                return Mob9011;
            case 9012:
                return Mob9012;
            case 9013:
                return Mob9013;
            case 9014:
                return Mob9014;
            case 9015:
                return Mob9015;
            case 9016:
                return Mob9016;
            case 9017:
                return Mob9017;
            case 9018:
                return Mob9018;
            case 9019:
                return Mob9019;
            case 9020:
                return Mob9020;
            case 9021:
                return Mob9021;
            case 9022:
                return Mob9022;
            case 9023:
                return Mob9023;
            case 9024:
                return Mob9024;
            case 9025:
                return Mob9025;
            case 9026:
                return Mob9026;
            case 9027:
                return Mob9027;
            case 9028:
                return Mob9028;
            case 9029:
                return Mob9029;
            case 9030:
                return Mob9030;
            case 9031:
                return Mob9031;
            case 9032:
                return Mob9032;
            case 9033:
                return Mob9033;
            case 9034:
                return Mob9034;
            case 9035:
                return Mob9035;
            case 9036:
                return Mob9036;
            case 9037:
                return Mob9037;
            case 9038:
                return Mob9038;
            case 9039:
                return Mob9039;
            case 9040:
                return Mob9040;
            case 9041:
                return Mob9041;
            case 9042:
                return Mob9042;
            case 9043:
                return Mob9043;
            case 9044:
                return Mob9044;
            case 9045:
                return Mob9045;
            case 9046:
                return Mob9046;
            case 9047:
                return Mob9047;
            case 9048:
                return Mob9048;
            case 9049:
                return Mob9049;
            case 9050:
                return Mob9050;
            case 9051:
                return Mob9051;
            case 9052:
                return Mob9052;
            case 9053:
                return Mob9053;
            case 9054:
                return Mob9054;
            case 9055:
                return Mob9055;
            case 9056:
                return Mob9056;
            case 9057:
                return Mob9057;
            case 9058:
                return Mob9058;
            case 9059:
                return Mob9059;
            case 9060:
                return Mob9060;
            case 9061:
                return Mob9061;
            case 9100:
                return Mob9100;
            case 9101:
                return Mob9101;
            case 9102:
                return Mob9102;
            case 9103:
                return Mob9103;
            case 9104:
                return Mob9104;
            case 9105:
                return Mob9105;
            case 9106:
                return Mob9106;
            case 9107:
                return Mob9107;
            case 9108:
                return Mob9108;
            case 9109:
                return Mob9109;
            case 9110:
                return Mob9110;
            case 9111:
                return Mob9111;
            case 9112:
                return Mob9112;
            case 9113:
                return Mob9113;
            case 9114:
                return Mob9114;
            case 9115:
                return Mob9115;
            case 9116:
                return Mob9116;
            case 9117:
                return Mob9117;
            case 9118:
                return Mob9118;
            case 9119:
                return Mob9119;
            case 9120:
                return Mob9120;
            case 9121:
                return Mob9121;
            case 9122:
                return Mob9122;
            case 9123:
                return Mob9123;
            case 9124:
                return Mob9124;
            case 9125:
                return Mob9125;
            case 9126:
                return Mob9126;
            case 9127:
                return Mob9127;
            case 9128:
                return Mob9128;
            case 9129:
                return Mob9129;
            case 9130:
                return Mob9130;
            case 9131:
                return Mob9131;
            case 9132:
                return Mob9132;
            case 9133:
                return Mob9133;
            case 9134:
                return Mob9134;
            case 9135:
                return Mob9135;
            case 9136:
                return Mob9136;
            case 9137:
                return Mob9137;
            case 9138:
                return Mob9138;
            case 9139:
                return Mob9139;
            case 9140:
                return Mob9140;
            case 9141:
                return Mob9141;
            case 9142:
                return Mob9142;
            case 9143:
                return Mob9143;
            case 9144:
                return Mob9144;
            case 9145:
                return Mob9145;
            case 9146:
                return Mob9146;
            case 9147:
                return Mob9147;
            case 9148:
                return Mob9148;
            case 9149:
                return Mob9149;
            case 9150:
                return Mob9150;
            case 9151:
                return Mob9151;
            case 9152:
                return Mob9152;
            case 9153:
                return Mob9153;
            case 9154:
                return Mob9154;
            case 9155:
                return Mob9155;
            case 9156:
                return Mob9156;
            case 9157:
                return Mob9157;
            case 9158:
                return Mob9158;
            case 9159:
                return Mob9159;
            case 9160:
                return Mob9160;
            case 9161:
                return Mob9161;
            case 9162:
                return Mob9162;
            case 9163:
                return Mob9163;
            case 9164:
                return Mob9164;
            case 9165:
                return Mob9165;
            case 9166:
                return Mob9166;
            case 9167:
                return Mob9167;
            case 9168:
                return Mob9168;
            case 9169:
                return Mob9169;
            case 9170:
                return Mob9170;
            case 9171:
                return Mob9171;
            case 9172:
                return Mob9172;
            case 9173:
                return Mob9173;
            case 9174:
                return Mob9174;
            case 9175:
                return Mob9175;
            case 9176:
                return Mob9176;
            case 9177:
                return Mob9177;
            case 9178:
                return Mob9178;
            case 9179:
                return Mob9179;
            case 9180:
                return Mob9180;
            case 9181:
                return Mob9181;
            case 9182:
                return Mob9182;
            case 9183:
                return Mob9183;
            case 9184:
                return Mob9184;
            case 9185:
                return Mob9185;
            case 9186:
                return Mob9186;
            case 9187:
                return Mob9187;
            case 9188:
                return Mob9188;
            case 9189:
                return Mob9189;
            case 9190:
                return Mob9190;
            case 10001:
                return Mob10001;
            case 10002:
                return Mob10002;
            case 10003:
                return Mob10003;
            case 10004:
                return Mob10004;
            case 10005:
                return Mob10005;
            case 10006:
                return Mob10006;
            case 10007:
                return Mob10007;
            case 10008:
                return Mob10008;
            case 10009:
                return Mob10009;
            case 10010:
                return Mob10010;
            case 10011:
                return Mob10011;
            case 10012:
                return Mob10012;
            case 10013:
                return Mob10013;
            case 10014:
                return Mob10014;
            case 10015:
                return Mob10015;
            case 10016:
                return Mob10016;
            case 10017:
                return Mob10017;
            case 10018:
                return Mob10018;
            case 10019:
                return Mob10019;
            case 10020:
                return Mob10020;
            case 10021:
                return Mob10021;
            case 10022:
                return Mob10022;
            case 10023:
                return Mob10023;
            case 10024:
                return Mob10024;
            case 10025:
                return Mob10025;
            case 10026:
                return Mob10026;
            case 10027:
                return Mob10027;
            case 10028:
                return Mob10028;
            case 10029:
                return Mob10029;
            case 10030:
                return Mob10030;
            case 10031:
                return Mob10031;
            case 10032:
                return Mob10032;
            case 10033:
                return Mob10033;
            case 10034:
                return Mob10034;
            case 10035:
                return Mob10035;
            case 10036:
                return Mob10036;
            case 10037:
                return Mob10037;
            case 10038:
                return Mob10038;
            case 10039:
                return Mob10039;
            case 10052:
                return Mob10052;
            case 10053:
                return Mob10053;
            case 10054:
                return Mob10054;
            case 10055:
                return Mob10055;
            case 10056:
                return Mob10056;
            case 10057:
                return Mob10057;
            case 10058:
                return Mob10058;
            case 10059:
                return Mob10059;
            case 10060:
                return Mob10060;
            case 10061:
                return Mob10061;
            case 10062:
                return Mob10062;
            case 10063:
                return Mob10063;
            case 10064:
                return Mob10064;
            case 10065:
                return Mob10065;
            case 10066:
                return Mob10066;
            case 10067:
                return Mob10067;
            case 10068:
                return Mob10068;
            case 10069:
                return Mob10069;
            case 10070:
                return Mob10070;
            case 10071:
                return Mob10071;
            case 10072:
                return Mob10072;
            case 10073:
                return Mob10073;
            case 10074:
                return Mob10074;
            case 10075:
                return Mob10075;
            case 10076:
                return Mob10076;
            case 10077:
                return Mob10077;
            case 10078:
                return Mob10078;
            case 10079:
                return Mob10079;
            case 10080:
                return Mob10080;
            case 10081:
                return Mob10081;
            case 10082:
                return Mob10082;
            case 10083:
                return Mob10083;
            case 10084:
                return Mob10084;
            case 10085:
                return Mob10085;
            case 10086:
                return Mob10086;
            case 10087:
                return Mob10087;
            case 10088:
                return Mob10088;
            case 10089:
                return Mob10089;
            case 10090:
                return Mob10090;
            case 10091:
                return Mob10091;
            case 10092:
                return Mob10092;
            case 10093:
                return Mob10093;
            case 10094:
                return Mob10094;
            case 10095:
                return Mob10095;
            case 10096:
                return Mob10096;
            case 10097:
                return Mob10097;
            case 10098:
                return Mob10098;
            case 10099:
                return Mob10099;
            case 10100:
                return Mob10100;
            case 10101:
                return Mob10101;
            case 10102:
                return Mob10102;
            case 10103:
                return Mob10103;
            case 10104:
                return Mob10104;
            case 10105:
                return Mob10105;
            case 10106:
                return Mob10106;
            case 10107:
                return Mob10107;
            case 10108:
                return Mob10108;
            case 10109:
                return Mob10109;
            case 10110:
                return Mob10110;
            case 10111:
                return Mob10111;
            case 10112:
                return Mob10112;
            case 10113:
                return Mob10113;
            case 10114:
                return Mob10114;
            case 10115:
                return Mob10115;
            case 10116:
                return Mob10116;
            case 10117:
                return Mob10117;
            case 10118:
                return Mob10118;
            case 10119:
                return Mob10119;
            case 10120:
                return Mob10120;
            case 10121:
                return Mob10121;
            case 10122:
                return Mob10122;
            case 10123:
                return Mob10123;
            case 10124:
                return Mob10124;
            case 10125:
                return Mob10125;
            case 10126:
                return Mob10126;
            case 10127:
                return Mob10127;
            case 10128:
                return Mob10128;
            case 10129:
                return Mob10129;
            case 10130:
                return Mob10130;
            case 10131:
                return Mob10131;
            case 10132:
                return Mob10132;
            case 10133:
                return Mob10133;
            case 10134:
                return Mob10134;
            case 10135:
                return Mob10135;
            case 10136:
                return Mob10136;
            case 10137:
                return Mob10137;
            case 10138:
                return Mob10138;
            case 10139:
                return Mob10139;
            case 10140:
                return Mob10140;
            case 10141:
                return Mob10141;
            case 10142:
                return Mob10142;
            case 10143:
                return Mob10143;
            case 10144:
                return Mob10144;
            case 10145:
                return Mob10145;
            case 10146:
                return Mob10146;
            case 10147:
                return Mob10147;
            case 10148:
                return Mob10148;
            case 10149:
                return Mob10149;
            case 10150:
                return Mob10150;
            case 10151:
                return Mob10151;
            case 10152:
                return Mob10152;
            case 10153:
                return Mob10153;
            case 10154:
                return Mob10154;
            case 10155:
                return Mob10155;
            case 10156:
                return Mob10156;
            case 10157:
                return Mob10157;
            case 10158:
                return Mob10158;
            case 10159:
                return Mob10159;
            case 10160:
                return Mob10160;
            case 10162:
                return Mob10162;
            case 10163:
                return Mob10163;
            case 10164:
                return Mob10164;
            case 10165:
                return Mob10165;
            case 10166:
                return Mob10166;
            case 10167:
                return Mob10167;
            case 10168:
                return Mob10168;
            case 10169:
                return Mob10169;
            case 10170:
                return Mob10170;
            case 10171:
                return Mob10171;
            case 10172:
                return Mob10172;
            case 10173:
                return Mob10173;
            case 10174:
                return Mob10174;
            case 10175:
                return Mob10175;
            case 10176:
                return Mob10176;
            case 10177:
                return Mob10177;
            case 10178:
                return Mob10178;
            case 10179:
                return Mob10179;
            case 10180:
                return Mob10180;
            case 10181:
                return Mob10181;
            case 10182:
                return Mob10182;
            case 10183:
                return Mob10183;
            case 10184:
                return Mob10184;
            case 10185:
                return Mob10185;
            case 10186:
                return Mob10186;
            case 10187:
                return Mob10187;
            case 10188:
                return Mob10188;
            case 10189:
                return Mob10189;
            case 10190:
                return Mob10190;
            case 10191:
                return Mob10191;
            case 10192:
                return Mob10192;
            case 10193:
                return Mob10193;
            case 10194:
                return Mob10194;
            case 10195:
                return Mob10195;
            case 10196:
                return Mob10196;
            case 10197:
                return Mob10197;
            case 10198:
                return Mob10198;
            case 10199:
                return Mob10199;
            case 10200:
                return Mob10200;
            case 10201:
                return Mob10201;
            case 10202:
                return Mob10202;
            case 10203:
                return Mob10203;
            case 10204:
                return Mob10204;
            case 10205:
                return Mob10205;
            case 10206:
                return Mob10206;
            case 10207:
                return Mob10207;
            case 10208:
                return Mob10208;
            case 10209:
                return Mob10209;
            case 10210:
                return Mob10210;
            case 10211:
                return Mob10211;
            case 10212:
                return Mob10212;
            case 10213:
                return Mob10213;
            case 10214:
                return Mob10214;
            case 10215:
                return Mob10215;
            case 10216:
                return Mob10216;
            case 10218:
                return Mob10218;
            case 10219:
                return Mob10219;
            case 10220:
                return Mob10220;
            case 10221:
                return Mob10221;
            case 10223:
                return Mob10223;
            case 10224:
                return Mob10224;
            case 10225:
                return Mob10225;
            case 10226:
                return Mob10226;
            case 10227:
                return Mob10227;
            case 10229:
                return Mob10229;
            case 10230:
                return Mob10230;
            case 10231:
                return Mob10231;
            case 10232:
                return Mob10232;
            case 10233:
                return Mob10233;
            case 10234:
                return Mob10234;
            case 10235:
                return Mob10235;
            case 10236:
                return Mob10236;
            case 10237:
                return Mob10237;
            case 10238:
                return Mob10238;
            case 10239:
                return Mob10239;
            case 10240:
                return Mob10240;
            case 10241:
                return Mob10241;
            case 10242:
                return Mob10242;
            case 10243:
                return Mob10243;
            case 10244:
                return Mob10244;
            case 10245:
                return Mob10245;
            case 10246:
                return Mob10246;
            case 10247:
                return Mob10247;
            case 10248:
                return Mob10248;
            case 10249:
                return Mob10249;
            case 10250:
                return Mob10250;
            case 10251:
                return Mob10251;
            case 10252:
                return Mob10252;
            case 20000:
                return Mob20000;
            case 20001:
                return Mob20001;
            case 20002:
                return Mob20002;
            case 20003:
                return Mob20003;
            case 20004:
                return Mob20004;
            case 20005:
                return Mob20005;
            case 20006:
                return Mob20006;
            case 20007:
                return Mob20007;
            case 20008:
                return Mob20008;
            case 20009:
                return Mob20009;
            case 20010:
                return Mob20010;

            default:
                return fallbackName ?? string.Empty;
        }
    }

    public static void SetCharaName(int charaId, string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            RuntimeCharaNames.Remove(charaId);
            return;
        }

        RuntimeCharaNames[charaId] = value;
    }

    public static void SetMobName(int mobId, string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            RuntimeMobNames.Remove(mobId);
            return;
        }

        RuntimeMobNames[mobId] = value;
    }
}