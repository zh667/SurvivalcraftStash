using Engine;
using Game;

namespace Stash.Game;

/// <summary>
/// 把整屏浮层挂进游戏界面 / 收回来。
///
/// **挂在哪**：<c>ComponentGui.ControlsContainerWidget</c>（GameWidget.xml 里那个
/// <c>&lt;CanvasWidget Name="ControlsContainer"&gt;</c>）。它最后一个孩子是
/// <c>DragHostWidget</c>——拖动物品时的那个图标就画在那里。
/// 我们插在 DragHost **前面**，于是浮层盖住面板，而拖拽图标仍然盖在浮层之上。
///
/// **为什么不用 ModalPanelWidget**：那是**一个**槽位。把浮层塞进去会顶掉正开着的
/// 箱子 / 工作台界面，而"一键填料"恰恰需要那个界面背后的合成格还活着。
/// 所以浮层是独立的一层，打开时只是把原面板 <c>IsVisible = false</c> 藏起来。
///
/// 出问题一律吞掉并打日志：浮层只是附加功能，不该把游戏带崩
/// （之前 StashClothingBlock 那次就是没兜住，直接进不去世界了）。
/// </summary>
public static class StashOverlayHost
{
    private static Widget? s_overlay;
    private static ContainerWidget? s_parent;

    /// <summary>被我们藏起来的原面板，关闭浮层时要放回来。</summary>
    private static Widget? s_hiddenPanel;

    public static bool IsOpen => s_overlay != null;

    public static void Toggle(ComponentGui gui, Func<Widget> factory)
    {
        if (IsOpen)
        {
            Close(gui);
        }
        else
        {
            Open(gui, factory);
        }
    }

    public static void Open(ComponentGui gui, Func<Widget> factory)
    {
        Close(gui);

        try
        {
            if (gui?.ControlsContainerWidget is not { } container)
            {
                Log.Warning("[Stash] 拿不到 ControlsContainerWidget，配方浏览器开不了。");
                return;
            }

            Widget overlay = factory();

            // 插在 DragHostWidget 前面：拖拽图标必须始终在最上层，
            // 否则从浮层里往外拖东西时图标会被浮层盖住。
            int insertAt = container.Children.Count;
            for (int i = 0; i < container.Children.Count; i++)
            {
                if (container.Children[i] is DragHostWidget)
                {
                    insertAt = i;
                    break;
                }
            }

            container.Children.Insert(insertAt, overlay);

            s_overlay = overlay;
            s_parent = container;

            // 把正开着的面板藏起来。不藏的话两层都在，正好是玩家最烦的"UI 重叠"。
            if (gui.ModalPanelWidget is { } panel)
            {
                s_hiddenPanel = panel;
                panel.IsVisible = false;
            }

            // 画布尺寸一起打：UI 缩放 1.2 时只有 708×398，排版问题九成出在这里，
            // 而玩家截图看不出他把缩放调到了多少。
            StashDiag.Log($"配方浏览器已打开：插在第 {insertAt}/{container.Children.Count} 个孩子处"
                + $"，画布 {StashScreenMetrics.Canvas}，面板预算 {StashScreenMetrics.PanelBudget}"
                + $"，藏起来的面板 {(s_hiddenPanel?.GetType().Name ?? "无")}");
        }
        catch (Exception exception)
        {
            Log.Warning($"[Stash] 打开配方浏览器失败：{exception.Message}");
            s_overlay = null;
            s_parent = null;
        }
    }

    public static void Close(ComponentGui? gui)
    {
        try
        {
            if (s_overlay != null && s_parent != null)
            {
                // Children.Remove 在这个引擎里返回 void，只能移除后自己回查一次。
                s_parent.Children.Remove(s_overlay);
                bool removed = !s_parent.Children.Contains(s_overlay);

                StashDiag.Log($"配方浏览器关闭：移除{(removed ? "成功" : "失败")}"
                    + $"，恢复面板 {(s_hiddenPanel?.GetType().Name ?? "无")}");

                if (!removed)
                {
                    // 浮层还挂在界面树上却被我们忘掉了 = 一层永远关不掉的黑幕。
                    // 真出现过这种事的话，玩家会形容成"界面卡住了，什么都点不了"。
                    StashDiag.Warn("配方浏览器：浮层不在预期的父节点里，可能残留在界面上");
                }
            }

            if (s_hiddenPanel != null)
            {
                s_hiddenPanel.IsVisible = true;
            }

            // 浮层里的搜索框带着焦点被销毁的话，按键会一直被吃掉——
            // 终端那边踩过这个坑（"白色竖杠一直在闪，退不出去"）。
            StashHotkeys.TypingInProgress = false;
        }
        catch (Exception exception)
        {
            Log.Warning($"[Stash] 关闭配方浏览器失败：{exception.Message}");
        }
        finally
        {
            s_overlay = null;
            s_parent = null;
            s_hiddenPanel = null;
        }
    }

    /// <summary>换界面 / 换世界时调，别让上一局的浮层留着。</summary>
    public static void Reset() => Close(null);
}
