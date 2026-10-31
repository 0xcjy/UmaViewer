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

    // Simplified Chinese: dropped next to the player as translations/zh-CN.json
    // (same hachimi text_data_dict.json layout). Set TranslationUrlCn to serve it
    // from the API instead.
    public const string TranslationFileNameCn = "zh-CN.json";
    public const string TranslationUrlCn = "https://fuwadl.yingqwq.cn/zh-cn/root/text_data_dict.json";

    // Fallback mirrors of the same hachimi zh-CN payload (identical content, different hosts).
    public static readonly string[] TranslationUrlCnMirrors =
    {
        "https://fuwadl.yingqwq.cn/zh-cn/root/text_data_dict.json",
        "https://files.leadrdrk.com/hachimi/tl-zh-cn/localized_data/text_data_dict.json"
    };
    public const string CharaTranslationTableKey = "6";
    public const string MobTranslationTableKey = "59";

    /// <summary>
    /// Looks for a Chinese translation file shipped next to the player:
    /// {exe}/translations/zh-CN.json (hachimi text_data_dict.json layout).
    /// </summary>
    public static string FindLocalTranslationFile()
    {
        string root = System.IO.Directory.GetParent(UnityEngine.Application.dataPath)?.FullName;

        if (string.IsNullOrEmpty(root)) return null;

        string[] candidates =
        {
            System.IO.Path.Combine(root, "translations", TranslationFileNameCn),
            System.IO.Path.Combine(root, "translations", "zh-CN", "text_data_dict.json")
        };

        foreach (string candidate in candidates)
        {
            if (System.IO.File.Exists(candidate)) return candidate;
        }

        return null;
    }

    private static readonly Dictionary<int, string> RuntimeCharaNames =
        new Dictionary<int, string>();

    private static readonly Dictionary<int, string> RuntimeMobNames =
        new Dictionary<int, string>();

    // Built-in character names.
    public const string Chara1001 = "特别周";
    public const string Chara1002 = "无声铃鹿";
    public const string Chara1003 = "东海帝王";
    public const string Chara1004 = "丸善斯基";
    public const string Chara1005 = "富士奇石";
    public const string Chara1006 = "小栗帽";
    public const string Chara1007 = "黄金船";
    public const string Chara1008 = "伏特加";
    public const string Chara1009 = "大和赤骥";
    public const string Chara1010 = "大树快车";
    public const string Chara1011 = "草上飞";
    public const string Chara1012 = "菱亚马逊";
    public const string Chara1013 = "目白麦昆";
    public const string Chara1014 = "神鹰";
    public const string Chara1015 = "好歌剧";
    public const string Chara1016 = "成田白仁";
    public const string Chara1017 = "鲁道夫象征";
    public const string Chara1018 = "气槽";
    public const string Chara1019 = "爱丽数码";
    public const string Chara1020 = "青云天空";
    public const string Chara1021 = "玉藻十字";
    public const string Chara1022 = "美妙姿势";
    public const string Chara1023 = "琵琶晨光";
    public const string Chara1024 = "摩耶重炮";
    public const string Chara1025 = "曼城茶座";
    public const string Chara1026 = "美浦波旁";
    public const string Chara1027 = "目白赖恩";
    public const string Chara1028 = "菱曙";
    public const string Chara1029 = "雪之美人";
    public const string Chara1030 = "米浴";
    public const string Chara1031 = "艾尼斯风神";
    public const string Chara1032 = "爱丽速子";
    public const string Chara1033 = "爱慕织姬";
    public const string Chara1034 = "稻荷一";
    public const string Chara1035 = "胜利奖券";
    public const string Chara1036 = "空中神宫";
    public const string Chara1037 = "荣进闪耀";
    public const string Chara1038 = "真机伶";
    public const string Chara1039 = "川上公主";
    public const string Chara1040 = "黄金城市";
    public const string Chara1041 = "樱花进王";
    public const string Chara1042 = "采珠";
    public const string Chara1043 = "新光风";
    public const string Chara1044 = "东商变革";
    public const string Chara1045 = "超级溪流";
    public const string Chara1046 = "醒目飞鹰";
    public const string Chara1047 = "荒漠英雄";
    public const string Chara1048 = "东瀛佐敦";
    public const string Chara1049 = "中山庆典";
    public const string Chara1050 = "成田大进";
    public const string Chara1051 = "西野花";
    public const string Chara1052 = "春丽";
    public const string Chara1053 = "青竹回忆";
    public const string Chara1054 = "微光飞驹";
    public const string Chara1055 = "美丽周日";
    public const string Chara1056 = "待兼福来";
    public const string Chara1057 = "千明代表";
    public const string Chara1058 = "名将怒涛";
    public const string Chara1059 = "目白多伯";
    public const string Chara1060 = "优秀素质";
    public const string Chara1061 = "圣王光环";
    public const string Chara1062 = "待兼诗歌剧";
    public const string Chara1063 = "生野狄杜斯";
    public const string Chara1064 = "目白善信";
    public const string Chara1065 = "大拓太阳神";
    public const string Chara1066 = "双涡轮";
    public const string Chara1067 = "里见光钻";
    public const string Chara1068 = "北部玄驹";
    public const string Chara1069 = "樱花千代王";
    public const string Chara1070 = "天狼星象征";
    public const string Chara1071 = "目白阿尔丹";
    public const string Chara1072 = "八重无敌";
    public const string Chara1073 = "鹤丸刚志";
    public const string Chara1074 = "目白光明";
    public const string Chara1075 = "谋勇兼备";
    public const string Chara1076 = "樱花桂冠";
    public const string Chara1077 = "成田路";
    public const string Chara1078 = "也文摄辉";
    public const string Chara1079 = "狂怒乐章";
    public const string Chara1080 = "创升";
    public const string Chara1081 = "希望之城";
    public const string Chara1082 = "北方飞翔";
    public const string Chara1083 = "吉兆";
    public const string Chara1084 = "谷野美酒";
    public const string Chara1085 = "第一红宝石";
    public const string Chara1086 = "目白高峰";
    public const string Chara1087 = "真弓快车";
    public const string Chara1088 = "里见皇冠";
    public const string Chara1089 = "高尚骏逸";
    public const string Chara1090 = "极峰";
    public const string Chara1091 = "强击";
    public const string Chara1092 = "烈焰快驹";
    public const string Chara1093 = "凯斯奇迹";
    public const string Chara1094 = "森林宝穴";
    public const string Chara1095 = "信念";
    public const string Chara1096 = "莫名其妙";
    public const string Chara1097 = "爱如往昔";
    public const string Chara1098 = "小林历奇";
    public const string Chara1099 = "北港火山";
    public const string Chara1100 = "奇锐骏";
    public const string Chara1102 = "万籁争鸣";
    public const string Chara1103 = "莱斯莱斯";
    public const string Chara1104 = "葛城王牌";
    public const string Chara1105 = "新宇宙";
    public const string Chara1106 = "菱钻奇宝";
    public const string Chara1107 = "跳舞城";
    public const string Chara1108 = "大鸣大放";
    public const string Chara1109 = "莱茵力量";
    public const string Chara1110 = "西沙里奥";
    public const string Chara1111 = "空中救主";
    public const string Chara1112 = "勇敢之心";
    public const string Chara1113 = "房一潘多拉";
    public const string Chara1114 = "迷人景致";
    public const string Chara1115 = "黄金巨匠";
    public const string Chara1116 = "贵妇人";
    public const string Chara1117 = "凯旋芭蕾";
    public const string Chara1118 = "爱慕律动";
    public const string Chara1119 = "梦之旅";
    public const string Chara1120 = "金镇之光";
    public const string Chara1121 = "多旺达";
    public const string Chara1124 = "吹波糖";
    public const string Chara1126 = "樱花千岁王";
    public const string Chara1127 = "超常骏骥";
    public const string Chara1128 = "防爆装束";
    public const string Chara1129 = "杏目";
    public const string Chara1130 = "旺紫丁";
    public const string Chara1131 = "放声欢呼";
    public const string Chara1132 = "唯独爱你";
    public const string Chara1133 = "创世驹";
    public const string Chara1134 = "机伶金花";
    public const string Chara1135 = "黄金旅程";
    public const string Chara1136 = "红色梦想";
    public const string Chara1137 = "神业";
    public const string Chara1138 = "青春永驻";
    public const string Chara1139 = "夺金旅途";
    public const string Chara1140 = "洛林军歌";
    public const string Chara1141 = "神威启示";
    public const string Chara1142 = "标志名驹";
    public const string Chara1143 = "比萨胜驹";
    public const string Chara1144 = "玫瑰帝国";
    public const string Chara1145 = "统治地位";
    public const string Chara2001 = "快乐米可";
    public const string Chara2002 = "苦涩糖衣";
    public const string Chara2003 = "小小蚕茧";
    public const string Chara2004 = "望族";
    public const string Chara2005 = "金星复活节";
    public const string Chara2006 = "里格安东娜";
    public const string Chara2007 = "索侬艾尔菲";
    public const string Chara2008 = "ST-2";
    public const string Chara9001 = "骏川手纲";
    public const string Chara9002 = "秋川理事长";
    public const string Chara9003 = "乙名史记者";
    public const string Chara9004 = "桐生院葵";
    public const string Chara9005 = "安心泽刺刺美";
    public const string Chara9006 = "㭴本理子";
    public const string Chara9007 = "美丽安心泽";
    public const string Chara9008 = "轻柔致意";
    public const string Chara9040 = "达利阿拉伯";
    public const string Chara9041 = "高多芬阿拉伯";
    public const string Chara9042 = "拜耶尔土耳其";
    public const string Chara9043 = "佐岳五月";
    public const string Chara9044 = "都留岐凉花";
    public const string Chara9045 = "砂糖光";
    public const string Chara9046 = "圣赖特";
    public const string Chara9047 = "速度象征";
    public const string Chara9048 = "海塞克";
    public const string Chara9049 = "塔可布莱恩";
    public const string Chara9050 = "保科健子";
    public const string Chara9051 = "汤华盛放";

    // mob名字
    public const string Mob8000 = "珠宝软玉";
    public const string Mob8001 = "桥式压缩";
    public const string Mob8002 = "迷你牡丹";
    public const string Mob8003 = "弗里敦邦";
    public const string Mob8004 = "尊贵美德";
    public const string Mob8005 = "水色飞瀑";
    public const string Mob8006 = "阴险洞察";
    public const string Mob8007 = "亲爱礼物";
    public const string Mob8008 = "迷你百合";
    public const string Mob8009 = "偶然机遇";
    public const string Mob8010 = "微风航天飞机";
    public const string Mob8011 = "匆匆忙忙";
    public const string Mob8012 = "仪式短剑";
    public const string Mob8013 = "慌慌张张";
    public const string Mob8014 = "迷你兰花";
    public const string Mob8015 = "缎带赋格曲";
    public const string Mob8016 = "萨克斯风节奏";
    public const string Mob8017 = "阅览杂志";
    public const string Mob8018 = "优势冲力";
    public const string Mob8019 = "棕色蒙布朗";
    public const string Mob8020 = "活力发电机";
    public const string Mob8021 = "香颂女孩";
    public const string Mob8022 = "迷你玫瑰";
    public const string Mob8023 = "成对梦境";
    public const string Mob8024 = "莎翁小说";
    public const string Mob8025 = "最美邂逅";
    public const string Mob8026 = "巨型享乐";
    public const string Mob8027 = "敏捷才能";
    public const string Mob8028 = "力量充能器";
    public const string Mob8029 = "萨拉沙泰歌剧";
    public const string Mob8030 = "大陵五";
    public const string Mob8031 = "缎带夜曲";
    public const string Mob8032 = "竖琴节奏";
    public const string Mob8033 = "春季美好";
    public const string Mob8034 = "单簧管节奏";
    public const string Mob8035 = "双重皮盾";
    public const string Mob8036 = "最高机密";
    public const string Mob8037 = "螺旋桨以萨";
    public const string Mob8038 = "残根新芽";
    public const string Mob8039 = "珠宝水晶";
    public const string Mob8040 = "变速超限";
    public const string Mob8041 = "糖果书籍";
    public const string Mob8042 = "花边柑橘";
    public const string Mob8043 = "喝彩双重";
    public const string Mob8044 = "传教士";
    public const string Mob8045 = "双重圆盾";
    public const string Mob8046 = "水色峡湾";
    public const string Mob8047 = "冰上跳跃者";
    public const string Mob8048 = "咖啡圣代";
    public const string Mob8049 = "喝彩两倍";
    public const string Mob8050 = "战士桨刃";
    public const string Mob8051 = "反射镜";
    public const string Mob8052 = "贝拉草原";
    public const string Mob8053 = "硬式摇滚乐手";
    public const string Mob8054 = "带电体";
    public const string Mob8055 = "坚石淑女";
    public const string Mob8056 = "阴森骑士";
    public const string Mob8057 = "天命石板";
    public const string Mob8058 = "上升树木";
    public const string Mob8059 = "卡恩文南";
    public const string Mob8060 = "深红天空";
    public const string Mob8061 = "中提琴节奏";
    public const string Mob8062 = "缎带牧歌";
    public const string Mob8063 = "阅览奇幻";
    public const string Mob8064 = "调和优雅";
    public const string Mob8065 = "少女魅力";
    public const string Mob8066 = "成长茁壮";
    public const string Mob8067 = "大于一切";
    public const string Mob8068 = "再次美丽";
    public const string Mob8069 = "鲁莽开火";
    public const string Mob8070 = "七角召唤师";
    public const string Mob8071 = "再次安可";
    public const string Mob8072 = "波尔卡舞步";
    public const string Mob8073 = "影子跟踪者";
    public const string Mob8074 = "调度员";
    public const string Mob8075 = "漩涡扭曲";
    public const string Mob8076 = "闪亮中庭";
    public const string Mob8077 = "特别圣代";
    public const string Mob8078 = "超越光速";
    public const string Mob8079 = "皇家海军";
    public const string Mob8080 = "六角峡谷";
    public const string Mob8081 = "莓果大师";
    public const string Mob8082 = "花园路径";
    public const string Mob8083 = "连岛沙洲";
    public const string Mob8084 = "比维拉琴节奏";
    public const string Mob8085 = "人生玫瑰";
    public const string Mob8086 = "雄伟之屋";
    public const string Mob8087 = "双向对讲机";
    public const string Mob8088 = "灿烂荣耀";
    public const string Mob8089 = "迷你波斯菊";
    public const string Mob8090 = "传奇进行式";
    public const string Mob8091 = "激励舞者";
    public const string Mob8092 = "追随日头";
    public const string Mob8093 = "威斯特手艺";
    public const string Mob8094 = "缎带轻歌剧";
    public const string Mob8095 = "水色海洋";
    public const string Mob8096 = "家教支援";
    public const string Mob8097 = "无语黑客";
    public const string Mob8098 = "劳动忠诚";
    public const string Mob8099 = "过度流逝";
    public const string Mob8100 = "狂热员工";
    public const string Mob8101 = "迷你凤仙花";
    public const string Mob8102 = "默契之交";
    public const string Mob8103 = "呼喊吾名";
    public const string Mob8104 = "在地溪流";
    public const string Mob8105 = "火热炸药";
    public const string Mob8106 = "皇家公仆";
    public const string Mob8107 = "双重弓兵盾";
    public const string Mob8108 = "礼貌致敬";
    public const string Mob8109 = "潮汐川流";
    public const string Mob8110 = "珠宝黄玉";
    public const string Mob8111 = "真心情书";
    public const string Mob8112 = "珠宝尖晶";
    public const string Mob8113 = "窸窸窣窣";
    public const string Mob8114 = "甜蜜圣代";
    public const string Mob8115 = "皇家冠冕";
    public const string Mob8116 = "逆势强风";
    public const string Mob8117 = "本体论";
    public const string Mob8118 = "水色舄湖";
    public const string Mob8119 = "混乱状态";
    public const string Mob8120 = "近在咫尺";
    public const string Mob8121 = "缎带摇篮曲";
    public const string Mob8122 = "阅览相簿";
    public const string Mob8123 = "不列颠一流";
    public const string Mob8124 = "璀璨闪耀";
    public const string Mob8125 = "柔情舞步";
    public const string Mob8126 = "慕夏淑女";
    public const string Mob8127 = "缎带随想曲";
    public const string Mob8128 = "双重斯瓦林盾";
    public const string Mob8129 = "自由女神";
    public const string Mob8130 = "割草者";
    public const string Mob8131 = "珠宝石青";
    public const string Mob8132 = "缎带宫廷恋歌";
    public const string Mob8133 = "金刚杵";
    public const string Mob8134 = "缎带挽歌";
    public const string Mob8135 = "引导之光";
    public const string Mob8136 = "小型方盒";
    public const string Mob8137 = "迅捷加速";
    public const string Mob8138 = "下拉连结";
    public const string Mob8139 = "阅览批评";
    public const string Mob8140 = "一生一世";
    public const string Mob8141 = "花边蜜桃";
    public const string Mob8142 = "芭蕾舞步";
    public const string Mob8143 = "知名女伶";
    public const string Mob8144 = "六块肌";
    public const string Mob8145 = "玫瑰捧花抛掷";
    public const string Mob8146 = "诚实话语";
    public const string Mob8147 = "经济生物";
    public const string Mob8148 = "棍棒一击";
    public const string Mob8149 = "天堂席次";
    public const string Mob8150 = "最初曙光";
    public const string Mob8151 = "羊角号节奏";
    public const string Mob8152 = "时机将至";
    public const string Mob8153 = "最佳之日";
    public const string Mob8154 = "特尔潘德";
    public const string Mob8155 = "协调四重奏";
    public const string Mob8156 = "阻塞瓶颈";
    public const string Mob8157 = "微小勇气";
    public const string Mob8158 = "假日踏青";
    public const string Mob8159 = "第七女王";
    public const string Mob8160 = "致命成分";
    public const string Mob8161 = "波吕许谟尼亚";
    public const string Mob8162 = "创下纪录";
    public const string Mob8163 = "缎带终曲";
    public const string Mob8164 = "宫殿点缀";
    public const string Mob8165 = "缎带哼唱";
    public const string Mob8166 = "缎带曼波";
    public const string Mob8167 = "阅览诗歌";
    public const string Mob8168 = "随身小包";
    public const string Mob8169 = "苹果酒";
    public const string Mob8170 = "城市西岸";
    public const string Mob8171 = "焦糖圣代";
    public const string Mob8172 = "五叶幸运草";
    public const string Mob8173 = "乌黑魔书";
    public const string Mob8174 = "阿维尔达";
    public const string Mob8175 = "国土佣仆";
    public const string Mob8176 = "竞争对手";
    public const string Mob8177 = "吾之波动";
    public const string Mob8178 = "强弓坚盾";
    public const string Mob8179 = "日落新郎";
    public const string Mob8180 = "双重小圆盾";
    public const string Mob8181 = "并行边车";
    public const string Mob8182 = "骚莎舞步";
    public const string Mob8183 = "夏兰珈神弓";
    public const string Mob8184 = "副产品";
    public const string Mob8185 = "意式小餐馆";
    public const string Mob8186 = "珠宝橄榄石";
    public const string Mob8187 = "女高音节奏";
    public const string Mob8188 = "迅速逼近";
    public const string Mob8189 = "活泼花梨";
    public const string Mob8190 = "智慧经理";
    public const string Mob8191 = "双重环绕";
    public const string Mob8192 = "纳西瑟斯";
    public const string Mob8193 = "韵律飞跃";
    public const string Mob8194 = "苦涩圣代";
    public const string Mob8195 = "塔鲁可";
    public const string Mob8196 = "伦巴舞步";
    public const string Mob8197 = "洁白魔书";
    public const string Mob8198 = "黝黑魔书";
    public const string Mob8199 = "芦笛节奏";
    public const string Mob8200 = "花边菠萝";
    public const string Mob8201 = "邱比特射击";
    public const string Mob8202 = "缎带诗歌";
    public const string Mob8203 = "微风无人机";
    public const string Mob8204 = "迷你铁线莲";
    public const string Mob8205 = "忉利天";
    public const string Mob8206 = "突发袭击";
    public const string Mob8207 = "吉祥印记";
    public const string Mob8208 = "早日发芽";
    public const string Mob8209 = "修长腰身";
    public const string Mob8210 = "缎带赞歌";
    public const string Mob8211 = "迷你水仙花";
    public const string Mob8212 = "托托努伊";
    public const string Mob8213 = "太阳鱼射线";
    public const string Mob8214 = "热情舞蹈";
    public const string Mob8215 = "闭门羹";
    public const string Mob8216 = "果断裁决";
    public const string Mob8217 = "街机冠军";
    public const string Mob8218 = "铜钹节奏";
    public const string Mob8219 = "表演者演出";
    public const string Mob8220 = "捕食者";
    public const string Mob8221 = "狼吞虎咽";
    public const string Mob8222 = "缎带加沃特";
    public const string Mob8223 = "飞行火鸡";
    public const string Mob8224 = "丽芙丽芙";
    public const string Mob8225 = "软软嫩嫩";
    public const string Mob8226 = "短号节奏";
    public const string Mob8227 = "潘帕格兰德";
    public const string Mob8228 = "舌尖现象";
    public const string Mob8229 = "占星术四书";
    public const string Mob8230 = "粉红发圈";
    public const string Mob8231 = "微风滑翔机";
    public const string Mob8232 = "西洋四";
    public const string Mob8233 = "神力";
    public const string Mob8234 = "键盘节奏";
    public const string Mob8235 = "喝彩二重";
    public const string Mob8236 = "妙妙";
    public const string Mob8237 = "白雪冰霜";
    public const string Mob8238 = "狩猎弯刀";
    public const string Mob8239 = "经典喜剧";
    public const string Mob8240 = "大地沙霾风";
    public const string Mob8241 = "穆如干";
    public const string Mob8242 = "小镇闲晃";
    public const string Mob8243 = "双重圣母盾";
    public const string Mob8244 = "命中注定";
    public const string Mob8245 = "雅痞幸运";
    public const string Mob8246 = "慌慌张张";
    public const string Mob8247 = "决斗之星 ";
    public const string Mob8248 = "水色绿洲";
    public const string Mob8249 = "快乐多多";
    public const string Mob8250 = "缎带牧歌";
    public const string Mob8251 = "玫瑰花瓶";
    public const string Mob8252 = "夏日营火";
    public const string Mob8253 = "飞鸟绝壁";
    public const string Mob8254 = "米克诺斯粉笔";
    public const string Mob8255 = "盛大飨宴";
    public const string Mob8256 = "莱姆发圈";
    public const string Mob8257 = "黑色檀木";
    public const string Mob8258 = "卡利拉斯";
    public const string Mob8259 = "微风轻航机";
    public const string Mob8260 = "度假村偶像";
    public const string Mob8261 = "克里修马德";
    public const string Mob8262 = "消遣欢愉";
    public const string Mob8263 = "瓦卢瓦红栗";
    public const string Mob8264 = "缎带轮旋曲";
    public const string Mob8265 = "短眠者";
    public const string Mob8266 = "跳动脉搏";
    public const string Mob8267 = "白银山茶花";
    public const string Mob8268 = "细微民俗学";
    public const string Mob8269 = "肉桂牛奶";
    public const string Mob8270 = "迪拉姆硬币";
    public const string Mob8271 = "花边香蕉";
    public const string Mob8272 = "阅览历史";
    public const string Mob8273 = "女杰降临";
    public const string Mob8274 = "优越绽放";
    public const string Mob8275 = "花边莱姆";
    public const string Mob8276 = "感受命运";
    public const string Mob8277 = "长距离跑者";
    public const string Mob8278 = "马札尔轮旋曲";
    public const string Mob8279 = "德式蛋糕";
    public const string Mob8280 = "水色喷泉";
    public const string Mob8281 = "出色演出";
    public const string Mob8282 = "感恩生命";
    public const string Mob8283 = "我乃女王";
    public const string Mob8284 = "微风热气球";
    public const string Mob8285 = "非我莫属";
    public const string Mob8286 = "灰色发圈";
    public const string Mob8287 = "谷登堡";
    public const string Mob8288 = "安古塔";
    public const string Mob8289 = "爱坛莉";
    public const string Mob8290 = "珠宝锆英";
    public const string Mob8291 = "私有财产";
    public const string Mob8292 = "乳白发圈";
    public const string Mob8293 = "秋季山峦";
    public const string Mob8294 = "坚定意志";
    public const string Mob8295 = "甜点宝贝";
    public const string Mob8296 = "恒常不变";
    public const string Mob8297 = "妖精回声";
    public const string Mob8298 = "桑卡利俄斯";
    public const string Mob8299 = "小号节奏";
    public const string Mob8300 = "琥珀发圈";
    public const string Mob8301 = "锋利诱惑";
    public const string Mob8302 = "记忆迷宫";
    public const string Mob8303 = "白银发圈";
    public const string Mob8304 = "渴望恋情";
    public const string Mob8305 = "珠宝碧玺";
    public const string Mob8306 = "海蓝珠宝";
    public const string Mob8307 = "涅普切妮丝";
    public const string Mob8308 = "神圣合唱";
    public const string Mob8309 = "巨大翅膀";
    public const string Mob8310 = "缎带挽歌";
    public const string Mob8311 = "微风直升机";
    public const string Mob8312 = "圣诞之夜";
    public const string Mob8313 = "古尔林塔尼";
    public const string Mob8314 = "西红柿酱舞步";
    public const string Mob8315 = "爱情一英吋";
    public const string Mob8316 = "净白魔书";
    public const string Mob8317 = "亮白佳丽";
    public const string Mob8318 = "傀儡华尔兹";
    public const string Mob8319 = "想象成功";
    public const string Mob8320 = "哥萨克舞步";
    public const string Mob8321 = "梦幻每日";
    public const string Mob8322 = "钢铁舞者";
    public const string Mob8323 = "新月王牌";
    public const string Mob8324 = "双重长鳞盾";
    public const string Mob8325 = "花边柳橙";
    public const string Mob8326 = "赞峇哈";
    public const string Mob8327 = "前进上流";
    public const string Mob8328 = "珠宝紫水晶";
    public const string Mob8329 = "复兴情诗";
    public const string Mob8330 = "伴随贤者";
    public const string Mob8331 = "闪耀水流";
    public const string Mob8332 = "便服暗扣";
    public const string Mob8333 = "巨大格伦戴尔";
    public const string Mob8334 = "漆黑起源";
    public const string Mob8335 = "玛西亚斯";
    public const string Mob8336 = "霹雳舞步";
    public const string Mob8337 = "珠宝红碧玺";
    public const string Mob8338 = "布丁圣代";
    public const string Mob8339 = "黄金国掌声";
    public const string Mob8340 = "伊拉帕";
    public const string Mob8341 = "贪婪空洞";
    public const string Mob8342 = "双簧管节奏";
    public const string Mob8343 = "阿巴布尼利 ";
    public const string Mob8344 = "强烈印记";
    public const string Mob8345 = "迷你香草";
    public const string Mob8346 = "双重竞技盾";
    public const string Mob8347 = "阅览悬疑";
    public const string Mob8348 = "缎带晨曲";
    public const string Mob8349 = "花边苹果";
    public const string Mob8350 = "帝国列车";
    public const string Mob8351 = "迷你百日菊";
    public const string Mob8352 = "轰轰隆隆";
    public const string Mob8353 = "等待夏琳";
    public const string Mob8354 = "橘色发圈";
    public const string Mob8355 = "象牙之塔";
    public const string Mob8356 = "朦胧夜色";
    public const string Mob8357 = "佛朗明哥舞步";
    public const string Mob8358 = "甜蜜小屋";
    public const string Mob8359 = "杰巴特";
    public const string Mob8360 = "可口圣代";
    public const string Mob8361 = "大方买单";
    public const string Mob8362 = "初始季节";
    public const string Mob8363 = "拇指花园";
    public const string Mob8364 = "花网";
    public const string Mob8365 = "花边葡萄";
    public const string Mob8366 = "恭敬虔诚";
    public const string Mob8367 = "华尔兹舞步";
    public const string Mob8368 = "识别磁卡";
    public const string Mob8369 = "长笛节奏";
    public const string Mob8370 = "流浪凯特";
    public const string Mob8371 = "黑色尖端";
    public const string Mob8372 = "古铜发圈";
    public const string Mob8373 = "心脏爆炸";
    public const string Mob8374 = "极地汲取者";
    public const string Mob8375 = "雅加达放克";
    public const string Mob8376 = "雪白魔书";
    public const string Mob8377 = "迅捷创建者";
    public const string Mob8378 = "美技进球";
    public const string Mob8379 = "电流断路器";
    public const string Mob8380 = "大麦町";
    public const string Mob8381 = "双重纹章盾";
    public const string Mob8382 = "微风风筝";
    public const string Mob8383 = "迷你婆婆纳";
    public const string Mob8384 = "双人骚莎";
    public const string Mob8385 = "爵士舞步";
    public const string Mob8386 = "独一无二";
    public const string Mob8387 = "骚动胜利";
    public const string Mob8388 = "泛太平洋";
    public const string Mob8389 = "双重希腊圆盾";
    public const string Mob8390 = "经典圣代";
    public const string Mob8391 = "萧姆管节奏";
    public const string Mob8392 = "以拉谷之战";
    public const string Mob8393 = "瓦沙克";
    public const string Mob8394 = "飞行领域";
    public const string Mob8395 = "珠宝缟玛瑙";
    public const string Mob8396 = "变倍瞄具";
    public const string Mob8397 = "白银莓果";
    public const string Mob8398 = "希格斯喷雾";
    public const string Mob8399 = "野蛮突击";
    public const string Mob8400 = "赫拉克利翁神话";
    public const string Mob8401 = "爱怜剪影";
    public const string Mob8402 = "花边甜瓜";
    public const string Mob8403 = "航海工具";
    public const string Mob8404 = "太阳射线";
    public const string Mob8405 = "海岸灯塔";
    public const string Mob8406 = "创造发送";
    public const string Mob8407 = "成千伏尔泰";
    public const string Mob8408 = "踢踏舞步";
    public const string Mob8409 = "北斗七星";
    public const string Mob8410 = "内心暗码";
    public const string Mob8411 = "缎带圣歌";
    public const string Mob8412 = "狂暴猛踢";
    public const string Mob8413 = "紫阳花月光";
    public const string Mob8414 = "原初闪亮";
    public const string Mob8415 = "咆哮女孩";
    public const string Mob8416 = "短笛节奏";
    public const string Mob8417 = "珠宝石榴";
    public const string Mob8418 = "巴投";
    public const string Mob8419 = "海仙女会合";
    public const string Mob8420 = "胡纳普 ";
    public const string Mob8421 = "勤劳皇帝";
    public const string Mob8422 = "强大力矩";
    public const string Mob8423 = "第一战术";
    public const string Mob8424 = "缎带小步舞曲";
    public const string Mob8425 = "安培组合";
    public const string Mob8426 = "迷你金盏花";
    public const string Mob8427 = "埃涅阿斯";
    public const string Mob8428 = "红绿灯";
    public const string Mob8429 = "征兆";
    public const string Mob8430 = "珠宝榍石";
    public const string Mob8431 = "湿润双眼";
    public const string Mob8432 = "华丽圣代";
    public const string Mob8433 = "为爱疯狂";
    public const string Mob8434 = "戏剧性发展";
    public const string Mob8435 = "迷你棉花";
    public const string Mob8436 = "对立者";
    public const string Mob8437 = "巴斯克手杖";
    public const string Mob8438 = "十八月分";
    public const string Mob8439 = "喝彩双倍";
    public const string Mob8440 = "毛利骨杖";
    public const string Mob8441 = "轻敲尖端";
    public const string Mob8442 = "罪恶领域";
    public const string Mob8443 = "缤纷粉彩";
    public const string Mob8444 = "月神火花";
    public const string Mob8445 = "多重选呼";
    public const string Mob8446 = "缎带二韵诗";
    public const string Mob8447 = "水果圣代";
    public const string Mob8448 = "迷你蒲公英";
    public const string Mob8449 = "缎带玛祖卡舞曲";
    public const string Mob8450 = "水色河川";
    public const string Mob8451 = "热带天空";
    public const string Mob8452 = "水色间歇泉";
    public const string Mob8453 = "狩猎长枪";
    public const string Mob8454 = "西塞拉琴节奏";
    public const string Mob8455 = "安魂鬼火";
    public const string Mob8456 = "皇家格纹";
    public const string Mob8457 = "纯白魔书";
    public const string Mob8458 = "漂漂浮浮";
    public const string Mob8459 = "女孩微笑";
    public const string Mob8460 = "顽皮淑女";
    public const string Mob8461 = "邪恶巫师";
    public const string Mob8462 = "室建陀";
    public const string Mob8463 = "首席女孩";
    public const string Mob8464 = "起飞平原";
    public const string Mob8465 = "冰冻天空";
    public const string Mob8466 = "连结之力";
    public const string Mob8467 = "咚咚咖咖";
    public const string Mob8468 = "微风飞机";
    public const string Mob8469 = "花边柠檬";
    public const string Mob8470 = "乡村闲暇";
    public const string Mob8471 = "蓝色发圈";
    public const string Mob8472 = "自我复制";
    public const string Mob8473 = "迷你熏衣草";
    public const string Mob8474 = "聚合体";
    public const string Mob8475 = "勇敢之幸";
    public const string Mob8476 = "迷你仙人掌";
    public const string Mob8477 = "珠宝方解石";
    public const string Mob8478 = "赤红之美";
    public const string Mob8479 = "沙石指令";
    public const string Mob8480 = "观光摩托车";
    public const string Mob8481 = "智慧之家";
    public const string Mob8482 = "优雅将军";
    public const string Mob8483 = "时光飞逝";
    public const string Mob8484 = "人见人爱";
    public const string Mob8485 = "绵延商队";
    public const string Mob8486 = "编码器";
    public const string Mob8487 = "缎带进行曲";
    public const string Mob8488 = "机械妄想";
    public const string Mob8489 = "霹雳连锁";
    public const string Mob8490 = "颤动双颊";
    public const string Mob8491 = "溜索者";
    public const string Mob8492 = "征状猛冲";
    public const string Mob8493 = "当前呼唤";
    public const string Mob8494 = "天然木杖";
    public const string Mob8495 = "迷你三色堇";
    public const string Mob8496 = "微风飞船";
    public const string Mob8497 = "傍晚天色";
    public const string Mob8498 = "愉悦职员";
    public const string Mob8499 = "珠宝红珠宝";
    public const string Mob8500 = "凯萨宫殿";
    public const string Mob8501 = "隧道之声";
    public const string Mob8502 = "薄荷水滴";
    public const string Mob8503 = "缎带练习曲";
    public const string Mob8504 = "第三方势力";
    public const string Mob8505 = "都铎花园";
    public const string Mob8506 = "缎带悼歌";
    public const string Mob8507 = "假面之眼";
    public const string Mob8508 = "美味圣代";
    public const string Mob8509 = "难以抗拒";
    public const string Mob8510 = "明月英雄";
    public const string Mob8511 = "敦娜";
    public const string Mob8512 = "坚定掌控";
    public const string Mob8513 = "座舱长";
    public const string Mob8514 = "康巴哈那";
    public const string Mob8515 = "双重大盾";
    public const string Mob8516 = "崭新歌曲";
    public const string Mob8517 = "阅览小说";
    public const string Mob8518 = "卡其色发圈";
    public const string Mob8519 = "宇宙刮刀";
    public const string Mob8520 = "短小射击";
    public const string Mob8521 = "花边莓果";
    public const string Mob8522 = "迷你雏菊";
    public const string Mob8523 = "一起跳跃";
    public const string Mob8524 = "编年史誓约";
    public const string Mob8525 = "红色发圈";
    public const string Mob8526 = "珠宝绿宝石";
    public const string Mob8527 = "响板节奏";
    public const string Mob8528 = "法国号节奏";
    public const string Mob8529 = "登台讲评";
    public const string Mob8530 = "贤者复苏";
    public const string Mob8531 = "双重塔里卡盾";
    public const string Mob8532 = "雀跃缎带";
    public const string Mob8533 = "折衷让步";
    public const string Mob8534 = "欢呼节奏";
    public const string Mob8535 = "雌性巨魔";
    public const string Mob8536 = "贝塔立体主义";
    public const string Mob8537 = "喝彩第二";
    public const string Mob8538 = "风潮赞美诗";
    public const string Mob8539 = "背负皮囊";
    public const string Mob8540 = "心灵凯萨";
    public const string Mob8541 = "大键琴节奏";
    public const string Mob8542 = "靛蓝发圈";
    public const string Mob8543 = "缎带抒情歌";
    public const string Mob8544 = "幸福奏鸣曲";
    public const string Mob8545 = "绿色发圈";
    public const string Mob8546 = "幼童珍宝";
    public const string Mob8547 = "急速火焰";
    public const string Mob8548 = "西班牙冰淇淋";
    public const string Mob8549 = "多米兹安娜";
    public const string Mob8550 = "克罗地亚领巾";
    public const string Mob8551 = "黄金发圈";
    public const string Mob8552 = "水力劈砍";
    public const string Mob8553 = "迷你莲花";
    public const string Mob8554 = "竞技场斗争";
    public const string Mob8555 = "星辰之夜";
    public const string Mob8556 = "海上沙鸥";
    public const string Mob8557 = "直击子弹";
    public const string Mob8558 = "珠宝蓝宝石";
    public const string Mob8559 = "海事运输";
    public const string Mob8560 = "领地所有者";
    public const string Mob8561 = "突袭闪电";
    public const string Mob8562 = "花边樱桃";
    public const string Mob8563 = "月亮爆裂";
    public const string Mob8564 = "谎言领域";
    public const string Mob8565 = "光照护盾";
    public const string Mob8566 = "珠宝孔雀石";
    public const string Mob8567 = "印地安祝福";
    public const string Mob8568 = "阅览科幻";
    public const string Mob8569 = "腓尼基商贾";
    public const string Mob8570 = "龙骑兵长枪";
    public const string Mob8571 = "晴朗天气";
    public const string Mob8572 = "世界树谷地";
    public const string Mob8573 = "朝生暮死";
    public const string Mob8574 = "暗黑魔书";
    public const string Mob8575 = "春季快乐";
    public const string Mob8576 = "洞见捕捉";
    public const string Mob8577 = "斯基泰短剑";
    public const string Mob8578 = "横笛节奏";
    public const string Mob8579 = "涡轮雷管";
    public const string Mob8580 = "联合旗帜";
    public const string Mob8581 = "爱情光束";
    public const string Mob8582 = "乐园天空";
    public const string Mob8583 = "甜蜜之心";
    public const string Mob8584 = "内在阴阳";
    public const string Mob8585 = "水色湖泊";
    public const string Mob8586 = "草裙舞学校";
    public const string Mob8587 = "布鲁克林岛";
    public const string Mob8588 = "阅览论文";
    public const string Mob8589 = "东方食客";
    public const string Mob8590 = "失落夜晚";
    public const string Mob8591 = "化学洗涤";
    public const string Mob8592 = "漆黑魔书";
    public const string Mob8593 = "慢动作";
    public const string Mob8594 = "杜鹃花幸福";
    public const string Mob8595 = "燃烧炙热";
    public const string Mob8596 = "缎带诙谐曲";
    public const string Mob8597 = "珠宝珊瑚";
    public const string Mob8598 = "缎带凯歌";
    public const string Mob8599 = "残酷女王";
    public const string Mob8600 = "索尔登热情";
    public const string Mob8601 = "尖端科技";
    public const string Mob8602 = "公爵罂粟花";
    public const string Mob8603 = "圣骑士之剑";
    public const string Mob8604 = "石竹花蕾";
    public const string Mob8605 = "爹地皮靴";
    public const string Mob8606 = "可爱派翠西亚";
    public const string Mob8607 = "震惊四座";
    public const string Mob8608 = "美德心灵";
    public const string Mob8609 = "活力四射";
    public const string Mob8610 = "鲁莽狂音";
    public const string Mob8611 = "甜美仙子";
    public const string Mob8612 = "天琴阿尔法";
    public const string Mob8613 = "椎名弗雷瑞斯";
    public const string Mob8614 = "ネモティアラ";
    public const string Mob8615 = "闪耀表演";
    public const string Mob8616 = "アウラムスタア";
    public const string Mob8617 = "ピアレスシャウト";
    public const string Mob8618 = "ツヅキレイニング";
    public const string Mob9001 = "玳瑁甲冑";
    public const string Mob9002 = "血色顽石";
    public const string Mob9003 = "赫普史超伯";
    public const string Mob9004 = "净白睡莲";
    public const string Mob9005 = "高度气压";
    public const string Mob9006 = "冰冷寒冻";
    public const string Mob9007 = "口红唇色";
    public const string Mob9008 = "漫歌倩影";
    public const string Mob9009 = "憧憬乡愁";
    public const string Mob9010 = "无口寡默";
    public const string Mob9011 = "薄紫水晶";
    public const string Mob9012 = "横纹玛瑙";
    public const string Mob9013 = "甜美杏实";
    public const string Mob9014 = "紫花菖蒲";
    public const string Mob9015 = "湿润之风";
    public const string Mob9016 = "不灭高地";
    public const string Mob9017 = "灵动耳饰";
    public const string Mob9018 = "美生色拉";
    public const string Mob9019 = "顽皮淘气";
    public const string Mob9020 = "使命任务";
    public const string Mob9021 = "辉煌瞬间";
    public const string Mob9022 = "固执坚定";
    public const string Mob9023 = "乌云遮日";
    public const string Mob9024 = "土星";
    public const string Mob9025 = "冥王星";
    public const string Mob9026 = "记号形象";
    public const string Mob9027 = "玛拉美";
    public const string Mob9028 = "艾吕雅";
    public const string Mob9029 = "就是今日";
    public const string Mob9030 = "闪电雷光";
    public const string Mob9031 = "金色丹桂";
    public const string Mob9032 = "酸浆金灯";
    public const string Mob9033 = "勒特威奇";
    public const string Mob9034 = "逍遥舞会";
    public const string Mob9035 = "鲍斯爵士";
    public const string Mob9036 = "阿雷斯坦";
    public const string Mob9037 = "英伦谣曲";
    public const string Mob9038 = "艾涅瓦科";
    public const string Mob9039 = "基拉劳斯火山";
    public const string Mob9040 = "幻岛女王";
    public const string Mob9041 = "命运司康";
    public const string Mob9042 = "海藻发菜";
    public const string Mob9043 = "萨达瓦尔";
    public const string Mob9044 = "萨姆斯纳哈尔";
    public const string Mob9045 = "阿齐扎";
    public const string Mob9046 = "贾利亚";
    public const string Mob9047 = "洁净明亮";
    public const string Mob9048 = "荣光之道";
    public const string Mob9049 = "母国家乡";
    public const string Mob9050 = "伊兹迪哈尔";
    public const string Mob9051 = "莫路珍娜";
    public const string Mob9052 = "纳杰瓦尔";
    public const string Mob9053 = "点击按动";
    public const string Mob9054 = "炎夏冷波";
    public const string Mob9055 = "不动静止";
    public const string Mob9056 = "诉说真理";
    public const string Mob9057 = "看守随员";
    public const string Mob9058 = "珍稀皇林";
    public const string Mob9059 = "和平贴纸";
    public const string Mob9060 = "一堆乐趣";
    public const string Mob9061 = "解锁之钥";
    public const string Mob9100 = "宝石之眼";
    public const string Mob9101 = "静谧之海";
    public const string Mob9102 = "高贵骑士";
    public const string Mob9103 = "薄荷浆果";
    public const string Mob9104 = "藤蔓之花";
    public const string Mob9105 = "翡翠权杖";
    public const string Mob9106 = "星辰探索者";
    public const string Mob9107 = "疾风雏菊";
    public const string Mob9108 = "阿耳忒弥斯彗星";
    public const string Mob9109 = "强力假面";
    public const string Mob9110 = "活力绿洲";
    public const string Mob9111 = "皇家奥斯陆";
    public const string Mob9112 = "底特律都市";
    public const string Mob9113 = "机智谜语";
    public const string Mob9114 = "维加斯之雾";
    public const string Mob9115 = "能量之掌";
    public const string Mob9116 = "睡神主宰";
    public const string Mob9117 = "深红之胶";
    public const string Mob9118 = "勇敢黄铜";
    public const string Mob9119 = "静谧靛蓝";
    public const string Mob9120 = "花漾热情";
    public const string Mob9121 = "紫藤之穹";
    public const string Mob9122 = "橄榄石之铃";
    public const string Mob9123 = "娇小胡萝卜";
    public const string Mob9124 = "新星追寻者";
    public const string Mob9125 = "鸢尾蜜桃";
    public const string Mob9126 = "电光紫罗兰";
    public const string Mob9127 = "柑橘王冠";
    public const string Mob9128 = "橙色万岁";
    public const string Mob9129 = "北极星之光";
    public const string Mob9130 = "英雄之志";
    public const string Mob9131 = "星辰粉蝶";
    public const string Mob9132 = "珊瑚火花";
    public const string Mob9133 = "烈火红星";
    public const string Mob9134 = "顶级色彩";
    public const string Mob9135 = "莲花公主";
    public const string Mob9136 = "图书管理员";
    public const string Mob9137 = "黄金红宝石";
    public const string Mob9138 = "破晓粉碎者";
    public const string Mob9139 = "魔幻莉安娜";
    public const string Mob9140 = "冰晶石";
    public const string Mob9141 = "阳光之路";
    public const string Mob9142 = "罗塞塔铃声";
    public const string Mob9143 = "自由之火";
    public const string Mob9144 = "探索者号";
    public const string Mob9145 = "高贵神韵";
    public const string Mob9146 = "金光圣座";
    public const string Mob9147 = "幻影灵魂";
    public const string Mob9148 = "闪耀含羞草";
    public const string Mob9149 = "星辰荣耀";
    public const string Mob9150 = "深沉之蓝";
    public const string Mob9151 = "路径核查";
    public const string Mob9152 = "小颠簸";
    public const string Mob9153 = "粉彩轻雾";
    public const string Mob9154 = "伟大的伊格尼斯";
    public const string Mob9155 = "银色王冠";
    public const string Mob9156 = "山岭守护者";
    public const string Mob9157 = "幻影玩偶";
    public const string Mob9158 = "转盘之眼";
    public const string Mob9159 = "心灵震慑";
    public const string Mob9160 = "白昼之光";
    public const string Mob9161 = "秋日女高音";
    public const string Mob9162 = "夏日清流";
    public const string Mob9163 = "雨季归来";
    public const string Mob9164 = "新地平线";
    public const string Mob9165 = "海岸地图";
    public const string Mob9166 = "草原之夜";
    public const string Mob9167 = "矮牵牛伞";
    public const string Mob9168 = "熠熠鸢尾";
    public const string Mob9169 = "酒庄贝蒂";
    public const string Mob9170 = "撒哈拉绿洲";
    public const string Mob9171 = "安第斯之谜";
    public const string Mob9172 = "棉花糖猫头鹰";
    public const string Mob9173 = "冰川栗色";
    public const string Mob9174 = "引路人";
    public const string Mob9175 = "海军上将";
    public const string Mob9176 = "水晶之靴";
    public const string Mob9177 = "闪电之刃";
    public const string Mob9178 = "薰衣草古龙水";
    public const string Mob9179 = "大丽花之语";
    public const string Mob9180 = "荣誉先驱";
    public const string Mob9181 = "高贵大丽花";
    public const string Mob9182 = "热带巨蟒";
    public const string Mob9183 = "水蓝领航";
    public const string Mob9184 = "明亮小精灵";
    public const string Mob9185 = "月色之纱";
    public const string Mob9186 = "乐活公园";
    public const string Mob9187 = "灵动蓟花";
    public const string Mob9188 = "南瓜小镇";
    public const string Mob9189 = "幽灵之火";
    public const string Mob9190 = "迪斯科大师";
    public const string Mob10001 = "再次需求";
    public const string Mob10002 = "秘境格列佛";
    public const string Mob10003 = "代理君主";
    public const string Mob10004 = "加勒比音乐";
    public const string Mob10005 = "隐士飞行";
    public const string Mob10006 = "永恒道路";
    public const string Mob10007 = "白色石块";
    public const string Mob10008 = "绅士家族";
    public const string Mob10009 = "鹿儿岛大社";
    public const string Mob10010 = "名门帕默 ";
    public const string Mob10011 = "安地斯兀鹰";
    public const string Mob10012 = "胜利龙骑兵";
    public const string Mob10013 = "迷雾黄钻";
    public const string Mob10014 = "东松柴";
    public const string Mob10015 = "绅士治疗";
    public const string Mob10016 = "核果洛基";
    public const string Mob10017 = "赞赞赞";
    public const string Mob10018 = "野上渐强";
    public const string Mob10019 = "绅士波涛";
    public const string Mob10020 = "白色箭矢";
    public const string Mob10021 = "银幕明星";
    public const string Mob10022 = "名门玛夏斯";
    public const string Mob10023 = "顶尖意志";
    public const string Mob10024 = "赤松维多利亚";
    public const string Mob10025 = "白美人";
    public const string Mob10026 = "家庭女孩";
    public const string Mob10027 = "绅士仙客来";
    public const string Mob10028 = "上流巅峰";
    public const string Mob10029 = "死而复生";
    public const string Mob10030 = "新生藤正";
    public const string Mob10031 = "歌唱时间";
    public const string Mob10032 = "土井国际";
    public const string Mob10033 = "租借太阳";
    public const string Mob10034 = "中心演员";
    public const string Mob10035 = "全面获胜";
    public const string Mob10036 = "富士剑";
    public const string Mob10037 = "国王之罪";
    public const string Mob10038 = "双重喷射";
    public const string Mob10039 = "长市乔治";
    public const string Mob10052 = "八江之星";
    public const string Mob10053 = "酷帅路径";
    public const string Mob10054 = "沙之淑女";
    public const string Mob10055 = "名门阿露丹";
    public const string Mob10056 = "世界轻触";
    public const string Mob10057 = "真实生日";
    public const string Mob10058 = "女孩晴天";
    public const string Mob10059 = "加油信息";
    public const string Mob10060 = "双赢之鹰";
    public const string Mob10061 = "室内壁球";
    public const string Mob10062 = "明王架式";
    public const string Mob10063 = "佛斯特之路";
    public const string Mob10064 = "世界十字弓";
    public const string Mob10065 = "闪光雷电";
    public const string Mob10066 = "特西欧集合";
    public const string Mob10067 = "国际韧性";
    public const string Mob10068 = "土井奇迹";
    public const string Mob10069 = "赤竹起源";
    public const string Mob10070 = "珊奇诺塔兹";
    public const string Mob10071 = "赤松中心";
    public const string Mob10072 = "御狐火炬";
    public const string Mob10073 = "老虎火箭";
    public const string Mob10074 = "丰中家主";
    public const string Mob10075 = "硬币歌手";
    public const string Mob10076 = "宝座之龙";
    public const string Mob10077 = "优秀首长";
    public const string Mob10078 = "圣列特意志";
    public const string Mob10079 = "第一雀跃";
    public const string Mob10080 = "联合天女";
    public const string Mob10081 = "东之姜黄";
    public const string Mob10082 = "爵士世纪";
    public const string Mob10083 = "清野丝绒";
    public const string Mob10084 = "金色秘密";
    public const string Mob10085 = "历史好友";
    public const string Mob10086 = "实事求是";
    public const string Mob10087 = "一级方程式记号";
    public const string Mob10088 = "星光果酱";
    public const string Mob10089 = "家庭烘培";
    public const string Mob10090 = "联合赢家";
    public const string Mob10091 = "满意天堂";
    public const string Mob10092 = "国际极端";
    public const string Mob10093 = "看护明星";
    public const string Mob10094 = "春色里程";
    public const string Mob10095 = "琥珀史诗";
    public const string Mob10096 = "名门夏卡尔";
    public const string Mob10097 = "银幕大师";
    public const string Mob10098 = "曼波鼓启动";
    public const string Mob10099 = "三坂基德";
    public const string Mob10100 = "仙客来挑战";
    public const string Mob10101 = "全面顶点";
    public const string Mob10102 = "欣德";
    public const string Mob10103 = "矿山辛巴";
    public const string Mob10104 = "黛内卢利称号";
    public const string Mob10105 = "激励带田";
    public const string Mob10106 = "迪米蒂";
    public const string Mob10107 = "小麦顶尖跑者";
    public const string Mob10108 = "佐田要点";
    public const string Mob10109 = "春色千岁";
    public const string Mob10110 = "埃姆提飓风";
    public const string Mob10111 = "连结火热";
    public const string Mob10112 = "绢丝月光";
    public const string Mob10113 = "和津台风";
    public const string Mob10114 = "感恩舞台";
    public const string Mob10115 = "饭丰兜风";
    public const string Mob10116 = "战车天空";
    public const string Mob10117 = "辰野存在";
    public const string Mob10118 = "春色卓越";
    public const string Mob10119 = "雄壮皇子";
    public const string Mob10120 = "春色荣光";
    public const string Mob10121 = "兄弟制定";
    public const string Mob10122 = "国际战神";
    public const string Mob10123 = "富士见手表";
    public const string Mob10124 = "权威舞蹈";
    public const string Mob10125 = "天照巨大";
    public const string Mob10126 = "取位机关";
    public const string Mob10127 = "富山胜利";
    public const string Mob10128 = "闪光巅峰";
    public const string Mob10129 = "黛内闪电";
    public const string Mob10130 = "舞野创升";
    public const string Mob10131 = "丰中急速";
    public const string Mob10132 = "夜袭之王";
    public const string Mob10133 = "奥马凯萨";
    public const string Mob10134 = "巅峰圣歌";
    public const string Mob10135 = "美丽坚定";
    public const string Mob10136 = "安昙宫殿";
    public const string Mob10137 = "极地巨星";
    public const string Mob10138 = "激励阿肯特";
    public const string Mob10139 = "原则模型";
    public const string Mob10140 = "交叉黄金";
    public const string Mob10141 = "铁扫帚进王";
    public const string Mob10142 = "和平电影";
    public const string Mob10143 = "甲冑亲王";
    public const string Mob10144 = "柠檬衣领";
    public const string Mob10145 = "希腊女孩";
    public const string Mob10146 = "千禧花河畔";
    public const string Mob10147 = "赤松冲击";
    public const string Mob10148 = "鲜明目标";
    public const string Mob10149 = "国际机灵";
    public const string Mob10150 = "闪耀挚友";
    public const string Mob10151 = "旅游之王";
    public const string Mob10152 = "优良甜品";
    public const string Mob10153 = "派遣医师";
    public const string Mob10154 = "楠松侦探";
    public const string Mob10155 = "帆翔工坊";
    public const string Mob10156 = "黄金大树";
    public const string Mob10157 = "东方七号";
    public const string Mob10158 = "百万吨风暴";
    public const string Mob10159 = "赤松八幡";
    public const string Mob10160 = "白金王子";
    public const string Mob10162 = "玫瑰骑士";
    public const string Mob10163 = "绿茵声援";
    public const string Mob10164 = "无声狩猎";
    public const string Mob10165 = "日出旗帜";
    public const string Mob10166 = "英雄工具";
    public const string Mob10167 = "遗留理想乡";
    public const string Mob10168 = "音速奔驰";
    public const string Mob10169 = "美食开拓";
    public const string Mob10170 = "建构因子";
    public const string Mob10171 = "传安回归";
    public const string Mob10172 = "大朱腾落";
    public const string Mob10173 = "路易西罗";
    public const string Mob10174 = "光亮标记";
    public const string Mob10175 = "隐居战士";
    public const string Mob10176 = "名门兰伯特";
    public const string Mob10177 = "活力中坚";
    public const string Mob10178 = "满杯皇";
    public const string Mob10179 = "感伤情绪";
    public const string Mob10180 = "宇宙空间";
    public const string Mob10181 = "大轮先进";
    public const string Mob10182 = "勇气皇";
    public const string Mob10183 = "神圣微光";
    public const string Mob10184 = "跑者台风";
    public const string Mob10185 = "羽化成神";
    public const string Mob10186 = "幸松闪电";
    public const string Mob10187 = "海洋假期";
    public const string Mob10188 = "起飞巨星";
    public const string Mob10189 = "喷射尾翼";
    public const string Mob10190 = "佐多光明";
    public const string Mob10191 = "松广首领";
    public const string Mob10192 = "双重旗帜";
    public const string Mob10193 = "花形派对";
    public const string Mob10194 = "西方头目";
    public const string Mob10195 = "创世皇帝";
    public const string Mob10196 = "大理花";
    public const string Mob10197 = "舞蹈缪斯";
    public const string Mob10198 = "虎冈";
    public const string Mob10199 = "火焰路线";
    public const string Mob10200 = "雨水";
    public const string Mob10201 = "鲜亮苍蓝";
    public const string Mob10202 = "镜中身影";
    public const string Mob10203 = "蓝色暴利";
    public const string Mob10204 = "否定纷乱";
    public const string Mob10205 = "光亮景致";
    public const string Mob10206 = "尼罗河红唇";
    public const string Mob10207 = "紫红精选";
    public const string Mob10208 = "加倍亲吻";
    public const string Mob10209 = "薄荷射击";
    public const string Mob10210 = "调皮美钻";
    public const string Mob10211 = "在地英雄";
    public const string Mob10212 = "暖心水果";
    public const string Mob10213 = "崛起巅峰";
    public const string Mob10214 = "アントラグラス";
    public const string Mob10215 = "エリラスト";
    public const string Mob10216 = "ゴッドリセール";
    public const string Mob10218 = "アンブロミニョン";
    public const string Mob10219 = "ミヤギノビジン";
    public const string Mob10220 = "エムティーナンゴク";
    public const string Mob10221 = "ブラストハッピー";
    public const string Mob10223 = "コレッテキセキ";
    public const string Mob10224 = "ラージーサイド";
    public const string Mob10225 = "コイノハナ";
    public const string Mob10226 = "マリリンシルバー";
    public const string Mob10227 = "ビッグバンワンダ";
    public const string Mob10229 = "コットンガール";
    public const string Mob10230 = "アドミカミ";
    public const string Mob10231 = "ポンドリコーダー";
    public const string Mob10232 = "マインテラコッタ";
    public const string Mob10233 = "オシロノアン";
    public const string Mob10234 = "ダンダンハニー";
    public const string Mob10235 = "エイノソフト";
    public const string Mob10236 = "ルリヒナギク";
    public const string Mob10237 = "グレンフェンサー";
    public const string Mob10238 = "フレンチスタイル";
    public const string Mob10239 = "テンセイガテラス";
    public const string Mob10240 = "オールラヴ";
    public const string Mob10241 = "ミミナリライジン";
    public const string Mob10242 = "スターミルフィーユ";
    public const string Mob10243 = "マーズプラン";
    public const string Mob10244 = "シャイニングバブル";
    public const string Mob10245 = "サマーストーム";
    public const string Mob10246 = "コットンパワー";
    public const string Mob10247 = "ライトニングハーツ";
    public const string Mob10248 = "ラオメディア";
    public const string Mob10249 = "マスターグリフォン";
    public const string Mob10250 = "シトラシトレイン";
    public const string Mob10251 = "ダイヤダイヤ";
    public const string Mob10252 = "オーラブレイク";
    public const string Mob20000 = "长发的赛马娘";
    public const string Mob20001 = "黑鹿毛的赛马娘";
    public const string Mob20002 = "戴眼镜的赛马娘";
    public const string Mob20003 = "不屈不挠的赛马娘";
    public const string Mob20004 = "有威严的赛马娘";
    public const string Mob20005 = "自信的赛马娘";
    public const string Mob20006 = "果敢的赛马娘";
    public const string Mob20007 = "淘气的赛马娘";
    public const string Mob20008 = "矮个子的赛马娘";
    public const string Mob20009 = "青鹿毛的赛马娘";
    public const string Mob20010 = "鹿毛的赛马娘";

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