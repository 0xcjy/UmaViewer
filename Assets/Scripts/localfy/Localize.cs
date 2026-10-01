using System.Collections.Generic;

namespace Gallop
{
    ///内置中文文本
    ///日文角色名仍然从 master.mdb 读取；
    ///英文角色名由 LocalizeEn.cs的本地表提供或者联网下载
    ///中文角色名直接写在本脚本中

    public static class Localize
    {
        public const string None = "";

        private static Region _currentRegion = Region.JP;

        public static Region CurrentRegion
        {
            get => _currentRegion;
            set => _currentRegion = value;
        }

        public enum Region
        {
            JP = 0,
            CN = 1
        }
        /// 根据当前区域取得普通角色名称
        /// 找不到翻译时使用fallbackName
        public static string GetCharaName(int charaId, string fallbackName = "")
        {
            switch (_currentRegion)
            {
                case Region.CN:
                    return CN.GetCharaName(charaId, fallbackName);

                case Region.JP:
                default:
                    return fallbackName ?? string.Empty;
            }
        }

        /// 根据当前选择region取得Mob名称
        /// 找不到翻译时返回fallbackName

        public static string GetMobName(int mobId, string fallbackName = "")
        {
            switch (_currentRegion)
            {
                case Region.CN:
                    return CN.GetMobName(mobId, fallbackName);

                case Region.JP:
                default:
                    return fallbackName ?? string.Empty;
            }
        }

        /// 运行时追加或覆盖中文普通角色名
        public static void SetCharaName(
            Region region,
            int charaId,
            string value)
        {
            if (region == Region.CN)
                CN.SetCharaName(charaId, value);
        }

        /// 运行时追加或覆盖中文Mob名字
        public static void SetMobName(
            Region region,
            int mobId,
            string value)
        {
            if (region == Region.CN)
                CN.SetMobName(mobId, value);
        }

        public static class JP
        {
            public static string GetCharaName(
                int charaId,
                string fallbackName = "")
            {
                return fallbackName ?? string.Empty;
            }

            public static string GetMobName(
                int mobId,
                string fallbackName = "")
            {
                return fallbackName ?? string.Empty;
            }
        }

        public static class CN
        {
            public const string Chara1001 = "特别周"; // スペシャルウィーク
            public const string Chara1002 = "无声铃鹿"; // サイレンススズカ
            public const string Chara1003 = "东海帝皇"; // トウカイテイオー
            public const string Chara1004 = "丸善斯基"; // マルゼンスキー
            public const string Chara1005 = "富士奇石"; // フジキセキ
            public const string Chara1006 = "小栗帽"; // オグリキャップ
            public const string Chara1007 = "黄金船"; // ゴールドシップ
            public const string Chara1008 = "伏特加"; // ウオッカ
            public const string Chara1009 = "大和赤骥"; // ダイワスカーレット
            public const string Chara1010 = "大树快车"; // タイキシャトル
            public const string Chara1011 = "草上飞"; // グラスワンダー
            public const string Chara1012 = "菱亚马逊"; // ヒシアマゾン
            public const string Chara1013 = "目白麦昆"; // メジロマックイーン
            public const string Chara1014 = "神鹰"; // エルコンドルパサー
            public const string Chara1015 = "好歌剧"; // テイエムオペラオー
            public const string Chara1016 = "成田白仁"; // ナリタブライアン
            public const string Chara1017 = "鲁道夫象征"; // シンボリルドルフ
            public const string Chara1018 = "气槽"; // エアグルーヴ
            public const string Chara1019 = "爱丽数码"; // アグネスデジタル
            public const string Chara1020 = "星云天空"; // セイウンスカイ
            public const string Chara1021 = "玉藻十字"; // タマモクロス
            public const string Chara1022 = "美妙姿势"; // ファインモーション
            public const string Chara1023 = "琵琶晨光"; // ビワハヤヒデ
            public const string Chara1024 = "摩耶重炮"; // マヤノトップガン
            public const string Chara1025 = "曼城茶座"; // マンハッタンカフェ
            public const string Chara1026 = "美浦波旁"; // ミホノブルボン
            public const string Chara1027 = "目白赖恩"; // メジロライアン
            public const string Chara1028 = "菱曙"; // ヒシアケボノ
            public const string Chara1029 = "雪之美人"; // ユキノビジン
            public const string Chara1030 = "米浴"; // ライスシャワー
            public const string Chara1031 = "艾尼斯风神"; // アイネスフウジン
            public const string Chara1032 = "爱丽速子"; // アグネスタキオン
            public const string Chara1033 = "爱慕织姬"; // アドマイヤベガ
            public const string Chara1034 = "稻荷一"; // イナリワン
            public const string Chara1035 = "胜利奖券"; // ウイニングチケット
            public const string Chara1036 = "空中神宫"; // エアシャカール
            public const string Chara1037 = "荣进闪耀"; // エイシンフラッシュ
            public const string Chara1038 = "真机伶"; // カレンチャン
            public const string Chara1039 = "川上公主"; // カワカミプリンセス
            public const string Chara1040 = "黄金城市"; // ゴールドシチー
            public const string Chara1041 = "樱花进王"; // サクラバクシンオー
            public const string Chara1042 = "采珠"; // シーキングザパール
            public const string Chara1043 = "新光风"; // シンコウウインディ
            public const string Chara1044 = "东商变革"; // スイープトウショウ
            public const string Chara1045 = "超级溪流"; // スーパークリーク
            public const string Chara1046 = "醒目飞鹰"; // スマートファルコン
            public const string Chara1047 = "荒漠英雄"; // ゼンノロブロイ
            public const string Chara1048 = "东瀛佐敦"; // トーセンジョーダン
            public const string Chara1049 = "中山庆典"; // ナカヤマフェスタ
            public const string Chara1050 = "成田大进"; // ナリタタイシン
            public const string Chara1051 = "西野花"; // ニシノフラワー
            public const string Chara1052 = "春乌菈菈"; // ハルウララ
            public const string Chara1053 = "青竹回忆"; // バンブーメモリー
            public const string Chara1054 = "微光飞驹"; // ビコーペガサス
            public const string Chara1055 = "美丽周日"; // マーベラスサンデー
            public const string Chara1056 = "待兼福来"; // マチカネフクキタル
            public const string Chara1057 = "千明代表"; // ミスターシービー
            public const string Chara1058 = "名将怒涛"; // メイショウドトウ
            public const string Chara1059 = "目白多伯"; // メジロドーベル
            public const string Chara1060 = "优秀素质"; // ナイスネイチャ
            public const string Chara1061 = "帝王光辉"; // キングヘイロー
            public const string Chara1062 = "待兼诗歌剧"; // マチカネタンホイザ
            public const string Chara1063 = "生野狄杜斯"; // イクノディクタス
            public const string Chara1064 = "目白善信"; // メジロパーマー
            public const string Chara1065 = "大拓太阳神"; // ダイタクヘリオス
            public const string Chara1066 = "双涡轮"; // ツインターボ
            public const string Chara1067 = "里见光钻"; // サトノダイヤモンド
            public const string Chara1068 = "北部玄驹"; // キタサンブラック
            public const string Chara1069 = "樱花千代王"; // サクラチヨノオー
            public const string Chara1070 = "天狼星象征"; // シリウスシンボリ
            public const string Chara1071 = "目白阿尔丹"; // メジロアルダン
            public const string Chara1072 = "八重无敌"; // ヤエノムテキ
            public const string Chara1073 = "鹤丸刚志"; // ツルマルツヨシ
            public const string Chara1074 = "目白光明"; // メジロブライト
            public const string Chara1075 = "谋勇兼备"; // デアリングタクト
            public const string Chara1076 = "樱花桂冠"; // サクラローレル
            public const string Chara1077 = "成田路"; // ナリタトップロード
            public const string Chara1078 = "山人西风"; // ヤマニンゼファー
            public const string Chara1079 = "狂热激昂"; // フリオーソ
            public const string Chara1080 = "创升"; // トランセンド
            public const string Chara1081 = "希望之城"; // エスポワールシチー
            public const string Chara1082 = "北方飞翔"; // ノースフライト
            public const string Chara1083 = "吉兆"; // シンボリクリスエス
            public const string Chara1084 = "谷野美酒"; // タニノギムレット
            public const string Chara1085 = "第一红宝"; // ダイイチルビー
            public const string Chara1086 = "目白拉莫奴"; // メジロラモーヌ
            public const string Chara1087 = "真弓快车"; // アストンマーチャン
            public const string Chara1088 = "里见皇冠"; // サトノクラウン
            public const string Chara1089 = "高尚骏逸"; // シュヴァルグラン
            public const string Chara1090 = "极峰"; // ヴィルシーナ
            public const string Chara1091 = "强击"; // ヴィブロス
            public const string Chara1092 = "烈焰快驹"; // ダンツフレーム
            public const string Chara1093 = "凯斯奇迹"; // ケイエスミラクル
            public const string Chara1094 = "森林宝穴"; // ジャングルポケット
            public const string Chara1095 = "信念"; // ビリーヴ
            public const string Chara1096 = "莫名其妙"; // ノーリーズン
            public const string Chara1097 = "爱如往昔"; // スティルインラブ
            public const string Chara1098 = "小林历奇"; // コパノリッキー
            public const string Chara1099 = "北港火山"; // ホッコータルマエ
            public const string Chara1100 = "奇锐骏"; // ワンダーアキュート
            public const string Chara1102 = "万籁争鸣"; // サウンズオブアース
            public const string Chara1103 = "莱斯莱斯"; // ロイスアンドロイス
            public const string Chara1104 = "葛城王牌"; // カツラギエース
            public const string Chara1105 = "新宇宙"; // ネオユニヴァース
            public const string Chara1106 = "菱钻奇宝"; // ヒシミラクル
            public const string Chara1107 = "跳舞城"; // タップダンスシチー
            public const string Chara1108 = "大鸣大放"; // ドゥラメンテ
            public const string Chara1109 = "莱茵力量"; // ラインクラフト
            public const string Chara1110 = "西沙里奥"; // シーザリオ
            public const string Chara1111 = "空中弥赛亚"; // エアメサイア
            public const string Chara1112 = "勇敢之心"; // デアリングハート
            public const string Chara1113 = "火神"; // フサイチパンドラ
            public const string Chara1114 = "迷人景致"; // ブエナビスタ
            public const string Chara1115 = "黄金巨匠"; // オルフェーヴル
            public const string Chara1116 = "贵妇人"; // ジェンティルドンナ
            public const string Chara1117 = "凯旋芭蕾"; // ウインバリアシオン
            public const string Chara1118 = "爱慕律动"; // アドマイヤグルーヴ
            public const string Chara1119 = "梦之旅"; // ドリームジャーニー
            public const string Chara1120 = "金镇之光"; // カルストンライトオ
            public const string Chara1121 = "多旺达"; // デュランダル
            public const string Chara1124 = "吹波糖"; // バブルガムフェロー
            public const string Chara1126 = "樱花千岁王"; // サクラチトセオー
            public const string Chara1127 = "超常骏骥"; // フェノーメノ
            public const string Chara1128 = "防爆装束"; // ブラストワンピース
            public const string Chara1129 = "杏目"; // アーモンドアイ
            public const string Chara1130 = "旺紫丁"; // ラッキーライラック
            public const string Chara1131 = "放声欢呼"; // グランアレグリア
            public const string Chara1132 = "唯独爱你"; // ラヴズオンリーユー
            public const string Chara1133 = "创世驹"; // クロノジェネシス
            public const string Chara1134 = "机伶金花"; // カレンブーケドール
            public const string Chara1135 = "黄金旅程"; // ステイゴールド
            public const string Chara1136 = "红色梦想"; // レッドディザイア
            public const string Chara1137 = "神业"; // キセキ
            public const string Chara1138 = "青春永驻"; // フォーエバーヤング
            public const string Chara1139 = "赌城大道"; // カジノドライヴ
            public const string Chara1140 = "洛林军歌"; // マルシュロレーヌ
            public const string Chara1141 = "神威启示"; // エピファネイア
            public const string Chara1142 = "标志名驹"; // ロゴタイプ
            public const string Chara1143 = "比萨胜驹"; // ヴィクトワールピサ
            public const string Chara1144 = "玫瑰帝国"; // ローズキングダム
            public const string Chara1145 = "统治地位"; // ルーラーシップ
            public const string Chara2005 = "卓芙";
            public const string Chara9001 = "俊川手纲(绿巨人)";
            public const string Chara9002 = "秋川理事长";


            private static readonly Dictionary<int, string>
                RuntimeCharaNames = new Dictionary<int, string>();

            private static readonly Dictionary<int, string>
                RuntimeMobNames = new Dictionary<int, string>();

            public static string GetCharaName(int charaId, string fallbackName = "")
            {
                if (RuntimeCharaNames.TryGetValue(charaId, out string runtimeValue))
                {
                    return runtimeValue;
                }

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

                    case 2005:
                        return Chara2005;

                    case 9001:
                        return Chara9001;
                        
                    case 9002:
                        return Chara9002;

                    default:
                        return fallbackName ?? string.Empty;
                }
            }

            public static string GetMobName(
                int mobId,
                string fallbackName = "")
            {
                if (RuntimeMobNames.TryGetValue(
                        mobId,
                        out string runtimeValue))
                {
                    return runtimeValue;
                }

                /*
                 * Mob 中文名以后直接在这里添加：
                 *
                 * switch (mobId)
                 * {
                 *     case 1:
                 *         return "中文名";
                 * }
                 */

                return fallbackName ?? string.Empty;
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
    }
}
