using Engine;
using Game;
using Stash.Shared.Storage;

namespace Stash.Game;

/// <summary>
/// 盖在一个库存槽位上的锁定标记。
///
/// 关键机制：SC 的 <c>HitTestGlobal</c> 是**子控件优先**（从最后一个子控件往前找），
/// 而 <see cref="InventorySlotWidget"/> 自己判断"这次点击是不是点在我身上"用的是
/// <c>HitTestGlobal(tap) == this</c>。所以只要我们往槽位里塞一个可命中的子控件盖住它，
/// 原版的取放逻辑就会自动让路——不需要 Harmony，也不用改原版代码。
///
/// 平时（非锁定模式）这个覆盖层不可命中，只画一个角标，不影响正常取放。
/// </summary>
public sealed class SlotLockOverlay : CanvasWidget
{
    private static readonly Color LockedTint = new(255, 80, 80, 90);
    private static readonly Color EditTint = new(255, 255, 255, 40);

    private readonly int m_slotIndex;
    private readonly RectangleWidget m_tint;
    private readonly ClickableWidget m_clickable;

    public SlotLockOverlay(int slotIndex)
    {
        m_slotIndex = slotIndex;
        HorizontalAlignment = WidgetAlignment.Stretch;
        VerticalAlignment = WidgetAlignment.Stretch;

        m_tint = new RectangleWidget
        {
            FillColor = Color.Transparent,
            OutlineColor = Color.Transparent,
            HorizontalAlignment = WidgetAlignment.Stretch,
            VerticalAlignment = WidgetAlignment.Stretch,
            IsHitTestVisible = false,
        };
        Children.Add(m_tint);

        m_clickable = new ClickableWidget
        {
            HorizontalAlignment = WidgetAlignment.Stretch,
            VerticalAlignment = WidgetAlignment.Stretch,
            IsHitTestVisible = false,
        };
        Children.Add(m_clickable);
    }

    /// <summary>锁定编辑模式：开启时接管点击，关闭时完全让位给原版取放。</summary>
    public bool EditMode { get; set; }

    public override void Update()
    {
        m_clickable.IsHitTestVisible = EditMode;
        IsHitTestVisible = EditMode;

        PlayerStashData player = StashStore.ForCurrentPlayer();
        bool locked = player.LockedSlots.Contains(m_slotIndex);

        m_tint.FillColor = locked ? LockedTint : (EditMode ? EditTint : Color.Transparent);

        if (EditMode && m_clickable.IsClicked)
        {
            player.ToggleLock(m_slotIndex);
            StashStore.Save();
            AudioManager.PlaySound("Audio/UI/ButtonClick", 1f, 0f, 0f);
        }
    }
}
