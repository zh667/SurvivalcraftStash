namespace Stash.Shared.Search;

/// <summary>
/// 汉字转拼音。**只为搜索服务**，所以做得很省：不带声调、不分音节、一字一读音。
///
/// 为什么不用现成的拼音库：SC 的模组是单个 dll 丢进包里，没有 NuGet 依赖可用，
/// 而完整的拼音表（两万多字）也没必要——需要的只是**物品名里出现过的那些字**。
///
/// 这张表是从游戏本体的 <c>Assets/Lang/zh-CN.json</c> 里把所有方块显示名的汉字扫出来，
/// 再加上本模组自己的名字凑成的——**原版方块名的覆盖率经脚本核对为 100%**。
/// 之外还补了一批别的模组常用的字（地图、传送、遗迹、齿轮之类），但那部分只能算尽力而为。
/// 以后原版加了新方块、出现表里没有的字，那个字会被**原样保留**，
/// 只是那一条搜不到拼音，不会报错也不会漏掉中文直搜。
///
/// 多音字一律取物品名里实际的那个读音（例如「粘土」的粘取 nian、「栅栏」的栅取 zha、
/// 「牛仔裤」的仔取 zai、「调料」的调取 tiao）。
/// </summary>
public static class StashPinyin
{
    /// <summary>"字拼音 字拼音 …"。写成一整串是为了省掉几百行字典字面量。</summary>
    private const string Table =
          "一yi 上shang 下xia 不bu 与yu 世shi 丛cong 严yan 个ge 中zhong 丸wan 书shu 了le 二er 云yun 井jing 人ren "
        + "仔zai 仙xian 件jian 传chuan 位wei 作zuo 信xin 值zhi 储chu 像xiang 光guang 关guan 具ju 兽shou 冰bing "
        + "凉liang 刀dao 制zhi 刷shua 刺ci 剂ji 前qian 剑jian 力li 动dong 勺shao 包bao 匙shi 升sheng 半ban 南nan "
        + "卵luan 卷juan 压ya 叉cha 发fa 变bian 可ke 台tai 右you 叶ye 号hao 合he 后hou 告gao 呢ni 和he 咒zhou 哨shao "
        + "器qi 囊nang 团tuan 围wei 图tu 圆yuan 土tu 圣sheng 地di 块kuai 型xing 基ji 堆dui 塑su 塔ta 塞sai 墓mu "
        + "墙qiang 墟xu 墩dun 声sheng 外wai 大da 头tou 奶nai 子zi 孔kong 存cun 宝bao 实shi 家jia 导dao 射she 小xiao "
        + "屋wu 屑xie 岗gang 岩yan 工gong 左zuo 已yi 布bu 帆fan 带dai 常chang 帽mao 幼you 废fei 度du 延yan 建jian "
        + "开kai 异yi 弓gong 弩nu 弹dan 彩cai 心xin 性xing 怪guai 恤xu 感gan 成cheng 我wo 或huo 户hu 房fang 把ba 护hu "
        + "指zhi 按an 捆kun 换huan 掌zhang 敏min 数shu 料liao 斧fu 无wu 旧jiu 时shi 星xing 春chun 是shi 显xian "
        + "晶jing 木mu 末mo 术shu 机ji 杉shan 村cun 束shu 条tiao 杨yang 杯bei 板ban 极ji 枪qiang 柱zhu 柳liu 柴chai "
        + "柵zha 栅zha 标biao 栏lan 树shu 核he 格ge 框kuang 桥qiao 桦hua 桩zhuang 桶tong 梁liang 梯ti 棉mian 棒bang "
        + "模mo 橡xiang 檩lin 欢huan 武wu 段duan 殿dian 毛mao 毯tan 民min 气qi 水shui 池chi 汤tang 沙sha 油you "
        + "泉quan 法fa 泥ni 泵beng 活huo 浅qian 浆jiang 海hai 渣zha 温wen 湿shi 源yuan 漆qi 灌guan 火huo 灯deng "
        + "灰hui 炉lu 炭tan 炸zha 点dian 烂lan 烟yan 烤kao 烧shao 焦jiao 煤mei 熔rong 燃ran 爆bao 片pian 牌pai 牙ya "
        + "牛niu 物wu 狼lang 玄xuan 环huan 玻bo 珠zhu 球qiu 理li 璃li 瓜gua 瓶ping 瓷ci 生sheng 用yong 甲jia 电dian "
        + "界jie 白bai 的de 皮pi 盒he 盔kui 盘pan 盾dun 真zhen 矛mao 短duan 石shi 矿kuang 砂sha 砖zhuan 硝xiao 硫liu "
        + "碎sui 碑bei 碗wan 磁ci 磺huang 示shi 神shen 秋qiu 种zhong 程cheng 空kong 穿chuan 窑yao 窗chuang 端duan "
        + "笔bi 符fu 笼long 筑zhu 策ce 管guan 箭jian 箱xiang 篝gou 篱li 粉fen 粒li 粘nian 紫zi 红hong 级ji 纸zhi "
        + "线xian 细xi 终zhong 经jing 绳sheng 编bian 罐guan 网wang 羽yu 耕geng 耙pa 肉rou 胆dan 背bei 胸xiong "
        + "能neng 脱tuo 腐fu 腿tui 舟zhou 船chuan 色se 花hua 苗miao 草cao 药yao 落luo 藏cang 藤teng 蛋dan 行xing "
        + "衣yi 表biao 衫shan 衬chen 袋dai 袜wa 裤ku 计ji 记ji 诞dan 调tiao 质zhi 路lu 车che 轨gui 转zhuan 轮lun "
        + "软ruan 轴zhou 辑ji 运yun 远yuan 迟chi 迹ji 送song 逻luo 遗yi 釉you 里li 野ye 量liang 金jin 针zhen 钟zhong "
        + "钥yao 钮niu 钻zuan 铁tie 铃ling 铅qian 铜tong 铲chan 链lian 锁suo 锅guo 锗zhe 锚mao 锤chui 锭ding 锹qiao "
        + "镐gao 镜jing 门men 阀fa 阱jing 阵zhen 阶jie 陶tao 陷xian 随sui 雀que 雕diao 雪xue 雷lei 霉mei 非fei "
        + "面mian 革ge 靴xue 靶ba 鞋xie 鞍an 音yin 顶ding 领ling 颜yan 马ma 验yan 高gao 魔mo 鱼yu 鸟niao 鸡ji 鹅e "
        + "麦mai 黑hei 齿chi ";

    private static readonly Dictionary<char, string> s_map = BuildMap();

    /// <summary>转换结果的缓存。物品名反复参与匹配，转一次就够了。</summary>
    private static readonly Dictionary<string, string> s_cache = new();

    private static Dictionary<char, string> BuildMap()
    {
        var map = new Dictionary<char, string>();
        foreach (string token in Table.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            if (token.Length > 1)
            {
                map[token[0]] = token[1..];
            }
        }

        return map;
    }

    /// <summary>
    /// 把一串文字转成连写的小写拼音。表里没有的字符原样保留（英文数字因此也能一起搜）。
    /// </summary>
    public static string Of(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        lock (s_cache)
        {
            if (s_cache.TryGetValue(text, out string? cached))
            {
                return cached;
            }
        }

        var builder = new System.Text.StringBuilder(text.Length * 3);
        foreach (char c in text)
        {
            if (s_map.TryGetValue(c, out string? pinyin))
            {
                builder.Append(pinyin);
            }
            else
            {
                builder.Append(char.ToLowerInvariant(c));
            }
        }

        string result = builder.ToString();
        lock (s_cache)
        {
            s_cache[text] = result;
        }

        return result;
    }
}
