using Engine;
using Game;

namespace Stash.Game;

/// <summary>
/// 逐个格子给背包"改妆"：图标从背后取景 + 不显示耐久条。
///
/// **为什么不是改方块**：所有衣物共用 <c>ClothingBlock</c>（索引 203）一个实例，
/// 取景方向（<c>GetIconViewOffset</c>）和耐久条判据（<c>Durability >= 0</c>）都是**按方块**的，
/// 改了就是所有衣物一起改。
///
/// 我试过写个子类顶掉 203 号方块——**这条路是死的，而且会让所有世界都打不开**：
/// <list type="bullet">
/// <item><c>BlocksManager.LoadBlocksData</c> 按**类名**匹配 BlocksData 的行，
/// 子类叫别的名字就一行都匹配不上，方块的属性（CraftingId / Durability / 显示名…）全是默认值。</item>
/// <item><c>ClothingBlock</c> 里所有语言查询都写成 <c>$"{GetType().Name}:{index}"</c>，
/// 类名一变，全部衣物的名字就都变成字面量 <c>[ClothingBlock:13]</c>。</item>
/// </list>
///
/// 好在这两样在**控件**上都有逐个开关：
/// <c>InventorySlotWidget.HideHealthBar</c> 和 <c>BlockIconWidget.CustomViewMatrix</c>，
/// 两个平台都有。所以改控件，不改方块。
///
/// 由 <c>ModLoader.GuiUpdate</c> 驱动，从 <c>GameWidget</c> 往下扫——快捷栏、物品栏、箱子、
/// 我们自己的界面、以及**拖动中的那个图标**全在这棵树底下。扫描做了节流，不是每帧都走。
/// （图鉴那种独立屏幕不在这棵树里，那儿的图标仍是原版取景。）
/// </summary>
public static class StashSlotDresser
{
    /// <summary>隔几帧扫一次。界面变化不需要逐帧跟，四分之一秒级别足够。</summary>
    private const int FrameInterval = 15;

    /// <summary>从背后看的机位。原版 ClothingBlock 是 (-1, 1, -1)（看到胸前），水平取反即背面。</summary>
    private static readonly Matrix FromBehind =
        Matrix.CreateLookAt(new Vector3(1f, 1f, 1f), Vector3.Zero, Vector3.UnitY);

    private static int s_countdown;

    public static void Update(ComponentGui? gui)
    {
        // 从 **GameWidget** 起扫，而不是 GuiWidget。
        // 拖动中的那个图标挂在 DragHostWidget 上，而它是 GuiWidget 的**兄弟**
        // （InventorySlotWidget 里写的是 GameWidget.Children.Find<DragHostWidget>()），
        // 只扫 GuiWidget 就会漏掉它——实机表现是"拖动时背包又变回正面（肩带）"。
        if (gui?.m_componentPlayer?.GameWidget is not { } root)
        {
            return;
        }

        if (--s_countdown > 0)
        {
            return;
        }

        s_countdown = FrameInterval;

        try
        {
            Walk(root);
        }
        catch (Exception exception)
        {
            // 纯装饰，出问题不该影响游戏。
            Log.Warning($"[Stash] 调整背包图标失败：{exception.Message}");
        }
    }

    private static void Walk(Widget widget)
    {
        if (widget is InventorySlotWidget slot)
        {
            Dress(slot);
            return;
        }

        if (widget is ContainerWidget container)
        {
            foreach (Widget child in container.Children)
            {
                Walk(child);
            }
        }
    }

    /// <summary>我们自己搭的界面可以直接调这个，不用等扫描。</summary>
    public static void Dress(InventorySlotWidget slot)
    {
        BlockIconWidget? icon = slot.m_blockIconWidget;
        if (icon == null)
        {
            return;
        }

        bool isBackpack = IsBackpack(icon.Value);

        // 只在"该是什么样"和"现在是什么样"不一致时才写，避免每次扫描都碰一遍控件。
        if (isBackpack)
        {
            if (!icon.CustomViewMatrix.HasValue)
            {
                icon.CustomViewMatrix = FromBehind;
            }

            slot.HideHealthBar = true;
            return;
        }

        // 格子里换成别的东西了：把我们改过的两样还回去。
        // 注意别动那些**本来**就设了这两项的格子（衣物界面的四个槽自带 HideHealthBar），
        // 所以只在"我们设过取景矩阵"时才还原耐久条。
        if (icon.CustomViewMatrix.HasValue && icon.CustomViewMatrix.Value == FromBehind)
        {
            icon.CustomViewMatrix = null;
            slot.HideHealthBar = false;
        }
    }

    private static bool IsBackpack(int value)
    {
        if (Terrain.ExtractContents(value) != StashBackpackTiers.ClothingBlockIndex)
        {
            return false;
        }

        int clothingIndex = ClothingBlock.GetClothingIndex(Terrain.ExtractData(value));
        return StashBackpackTiers.ByClothingIndex(clothingIndex) != null;
    }
}
