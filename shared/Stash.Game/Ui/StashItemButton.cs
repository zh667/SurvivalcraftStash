using Engine;
using Game;

namespace Stash.Game;

/// <summary>
/// 目录里的一个可点物品格：凹框 + 物品图标 + 一块铺满的点击区。
///
/// **不能用 <c>InventorySlotWidget</c>**：那个必须绑一份真实库存的某一格，
/// 而这里显示的是"游戏里存在的所有物品"，背后没有库存。
///
/// **也不再用 <c>BevelledButtonWidget</c>**（第一版是这么写的，实机点不动）。
/// 原因是它的点击区来自 <c>Widgets/BevelledButtonContents</c> 里那个
/// <c>&lt;CanvasWidget Margin="6, 6"&gt;</c> 的孩子——点击区比按钮**四周各小 6 单位**，
/// 而且整块布局依赖标签文字撑开。我们的格子没有文字、又额外塞了个图标进去，
/// 这一套就不可靠了。这里全部用基础控件自己搭，点击区**铺满整格**，行为完全可控。
///
/// <see cref="ClickableWidget"/> 没有 Size 属性（<c>Size</c> 是 <c>CanvasWidget</c> 上的），
/// 它靠默认的 <c>DesiredSize = Infinity</c> 撑满父控件——原版按钮也是这么做的。
/// 放在**最后一个孩子**：<c>Widget.HitTestGlobal</c> 是倒着遍历孩子的，最后一个最先命中。
/// </summary>
public sealed class StashItemButton : CanvasWidget
{
    /// <summary>
    /// 和原版格子完全一致：<c>Widgets/CraftingRecipeSlot.xml</c> 里写的是
    /// <c>BevelSize="-2" DirectionalLight="0.15" CenterColor="0, 0, 0, 0"</c>——
    /// 中心透明，让底板的原版米色透上来。
    /// </summary>
    private static readonly Color NormalCenter = new(0, 0, 0, 0);

    private static readonly Color SelectedCenter = new(96, 160, 190, 150);

    private readonly BevelledRectangleWidget m_frame;
    private readonly BlockIconWidget m_icon;
    private readonly ClickableWidget m_clickable;

    public StashItemButton(float size)
    {
        Size = new Vector2(size, size);

        m_frame = new BevelledRectangleWidget
        {
            Size = new Vector2(size, size),
            BevelSize = -2f,          // 负值 = 内凹，和原版格子一个观感
            DirectionalLight = 0.15f,
            CenterColor = NormalCenter,
            IsHitTestVisible = false,
        };
        Children.Add(m_frame);

        m_icon = new BlockIconWidget
        {
            Size = new Vector2(size - 8f, size - 8f),
            HorizontalAlignment = WidgetAlignment.Center,
            VerticalAlignment = WidgetAlignment.Center,
            IsHitTestVisible = false,
        };
        Children.Add(m_icon);

        m_clickable = new ClickableWidget { SoundName = "Audio/UI/ButtonClick" };
        Children.Add(m_clickable);
    }

    /// <summary>这一格现在显示的物品值，0 = 空格。</summary>
    public int Value { get; private set; }

    public bool IsClicked => Value != 0 && m_clickable.IsClicked;

    public void SetValue(int value)
    {
        Value = value;
        m_icon.Value = value;

        // **顺序不能反。** BlockIconWidget.Light 不是独立字段，它写的是 Value 的光照位：
        //     public int Light { get => Terrain.ExtractLight(Value);
        //                        set => Value = Terrain.ReplaceLight(Value, value); }
        // 先写 Light 再写 Value 的话光照会被覆盖掉，整屏图标全是暗的。
        if (value != 0)
        {
            m_icon.Light = 15;
        }

        m_icon.IsVisible = value != 0;
    }

    /// <summary>选中的那一格底色提亮，让玩家知道右边显示的是谁的配方。</summary>
    public void SetSelected(bool selected) =>
        m_frame.CenterColor = selected ? SelectedCenter : NormalCenter;
}
