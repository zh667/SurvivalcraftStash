using Engine;
using Game;

namespace Stash.Game;

/// <summary>
/// 界面能用多大地方。
///
/// **这不是可选的讲究，是硬约束**——SC 的虚拟画布宽度写死在
/// <c>ScreensManager.LayoutAndDrawWidgets</c> 里：
/// <code>
/// float num = 850f / MathUtils.Clamp(SettingsManager.UIScale, 0.5f, 1.2f);
/// Vector2 availableSize = new Vector2(num, num / vector.X * vector.Y);
/// float num3 = num * 9f / 16f;          // 高度不低于宽 × 9/16
/// </code>
/// 也就是说画布宽度只在 <b>708（缩放 1.2）~ 1700（缩放 0.5）</b> 之间，默认 0.8 → 1062。
/// 高度至少是宽的 9/16。
///
/// 换算成实际情况：
/// <code>
/// 缩放 0.5  画布 1700×956   原版面板 614×382 之后每边还剩 543
/// 缩放 0.8  画布 1062×598   每边剩 224          ← 默认
/// 缩放 1.2  画布  708×398   每边剩 47，上下各剩 8   ← 手机玩家常用
/// </code>
/// 一个原版格子 72 单位、我们终端里 50 单位——**47 单位塞不下任何东西**。
/// 所以 JEI 那种"面板旁边永远挂一列物品"在 SC 上做不到，只能做成整屏浮层。
///
/// 我们自己的 <see cref="StashTerminalWidget"/> 一度是 816×438，在 708 宽的画布上
/// 左右各被切掉 54 单位（整整一列格子点不到）。现在一律先问这里要预算。
/// </summary>
public static class StashScreenMetrics
{
    /// <summary>UI 缩放拉满时的画布。拿不到实测值就按这个来，宁可小一点也别溢出。</summary>
    public const float SafeWidth = 708f;
    public const float SafeHeight = 398f;

    /// <summary>左右两条竖排按钮（返回/衣物/物品栏…）各占 64 宽，别压上去。</summary>
    private const float SideControlsWidth = 68f;

    /// <summary>底部快捷栏 + 状态条。面板本来就是居中的，这里只是别贴太近。</summary>
    private const float BottomControlsHeight = 24f;

    /// <summary>
    /// 当前画布尺寸。取 <c>ScreensManager.RootWidget.ActualSize</c>——
    /// 那是布局算完之后的真实可用区域，比自己按公式反推可靠（公式还得考虑窗口宽高比）。
    /// 布局还没跑过时 ActualSize 是 0，这时退回保底值。
    /// </summary>
    public static Vector2 Canvas
    {
        get
        {
            try
            {
                Vector2 size = ScreensManager.RootWidget?.ActualSize ?? Vector2.Zero;
                if (size.X >= 100f && size.Y >= 100f)
                {
                    return size;
                }
            }
            catch
            {
                // 布局线程之外访问、或者根控件还没建好——都退回保底值。
            }

            return new Vector2(SafeWidth, SafeHeight);
        }
    }

    /// <summary>一个模态面板最大能做多大。超过这个尺寸的部分会被 GameWidget 的 ClampToBounds 切掉。</summary>
    public static Vector2 PanelBudget
    {
        get
        {
            Vector2 canvas = Canvas;
            return new Vector2(
                MathUtils.Max(canvas.X - SideControlsWidth * 2f, 320f),
                MathUtils.Max(canvas.Y - BottomControlsHeight, 260f));
        }
    }

    /// <summary>
    /// 把一个想要的尺寸压进预算，返回需要乘的系数（≤1）。
    /// 等比缩放而不是裁剪：格子变小总比点不到强。
    /// </summary>
    public static float FitScale(Vector2 desired, float minimum = 0.6f)
    {
        Vector2 budget = PanelBudget;
        if (desired.X <= 0f || desired.Y <= 0f)
        {
            return 1f;
        }

        float scale = MathUtils.Min(budget.X / desired.X, budget.Y / desired.Y, 1f);
        return MathUtils.Max(scale, minimum);
    }

    /// <summary>面板底部还能不能再腾出 <paramref name="extra"/> 这么高而不溢出画布。</summary>
    public static bool HasRoomBelow(float panelHeight, float extra) =>
        panelHeight + extra <= PanelBudget.Y;
}
