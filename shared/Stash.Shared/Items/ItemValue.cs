namespace Stash.Shared.Items;

/// <summary>
/// Survivalcraft 把"物品"编码成一个 int：contents(10bit) | light(4bit) | data(18bit)。
/// 常量与 <c>Game.Terrain</c> 一致（联机版与插件版 1.9.2.1 已核对为同值）。
/// 这里重新实现是为了让共享层不依赖游戏程序集，从而可以单测。
/// </summary>
public static class ItemValue
{
    public const int ContentsMask = 1023;
    public const int LightShift = 10;
    public const int LightMask = 15360;
    public const int DataShift = 14;
    public const int DataMask = unchecked((int)0xFFFFC000);

    /// <summary>data 位可表示的最大值（18 bit）。</summary>
    public const int MaxData = (1 << 18) - 1;

    public static int Contents(int value) => value & ContentsMask;

    public static int Data(int value) => (value & DataMask) >>> DataShift;

    public static int Light(int value) => (value & LightMask) >> LightShift;

    public static int Make(int contents, int data = 0, int light = 0) =>
        (contents & ContentsMask) | ((light << LightShift) & LightMask) | (data << DataShift);

    public static int ReplaceContents(int value, int contents) =>
        (value & ~ContentsMask) | (contents & ContentsMask);

    public static int ReplaceData(int value, int data) =>
        (value & ~DataMask) | (data << DataShift);

    public static int ReplaceLight(int value, int light) =>
        (value & ~LightMask) | ((light << LightShift) & LightMask);

    /// <summary>
    /// 判断两个槽位值是否属于"同一种可堆叠物品"。光照位不参与比较——它不是物品属性。
    /// </summary>
    public static bool SameItem(int a, int b) => (a & ~LightMask) == (b & ~LightMask);
}
