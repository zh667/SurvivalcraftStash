using Engine;
using Game;
using Stash.Shared.Transfer;

namespace Stash.Game;

/// <summary>
/// 挂在库存界面上的一排按钮。**一个界面只挂一排**，避免两侧各挂一排互相压住。
///
/// 用代码搭而不是加 XML 布局：XML 要走 ContentManager 覆盖，容易和别的 Mod / 材质包打架；
/// 代码搭出来的控件只属于这一份界面实例，界面关掉就跟着没了。
///
/// **整理只作用于玩家自己的物品栏。** 箱子里怎么摆是玩家自己的事，替他重排没有意义；
/// 存储终端的显示本来就是汇总排序过的，底下箱子乱不乱都不影响检索。
/// 早先给箱子也放了整理按钮，实机反馈是"在物品栏界面点整理却说没有要整理的"——
/// 因为那个按钮整理的是旁边那个空的合成格。现在只留一个整理，作用对象明确。
/// </summary>
public sealed class StashButtonBar : StackPanelWidget
{
    private readonly ComponentGui? m_gui;
    private readonly PanelContainer? m_player;
    private readonly PanelContainer? m_container;

    private readonly ButtonWidget? m_sortButton;
    private readonly ButtonWidget? m_depositButton;
    private readonly ButtonWidget? m_depositAllButton;
    private readonly ButtonWidget? m_takeButton;
    private readonly ButtonWidget? m_lockButton;
    private readonly ButtonWidget? m_backpackButton;
    private readonly ButtonWidget m_undoButton;
    private readonly List<SlotLockOverlay> m_lockOverlays = new();

    /// <param name="player">玩家自己的库存区（可能没有，例如纯容器界面）。</param>
    /// <param name="container">界面里的另一个容器区（箱子/熔炉/发射器…）。</param>
    public StashButtonBar(ComponentGui? gui, PanelContainer? player, PanelContainer? container)
    {
        m_gui = gui;
        m_player = player;
        m_container = container;

        Direction = LayoutDirection.Horizontal;
        HorizontalAlignment = WidgetAlignment.Center;
        VerticalAlignment = WidgetAlignment.Near;
        Margin = new Vector2(0f, 4f);

        if (player != null)
        {
            m_sortButton = AddButton(StashText.Sort);
        }

        if (player != null && container != null)
        {
            m_depositButton = AddButton(StashText.Deposit);
            m_depositAllButton = AddButton(StashText.DepositAll);
            m_takeButton = AddButton(StashText.Restock);
        }

        // 背包入口挂在这里而不是衣物界面：联机版派发 ClothingWidgetOpen 时传的钩子名是空串
        // （原版写漏了），那个钩子永远不会触发。放在按钮栏里两版都能用，也更好找。
        if (player != null && gui?.m_componentPlayer != null)
        {
            m_backpackButton = AddButton(StashText.OpenBackpack);
        }

        if (player != null)
        {
            m_lockButton = AddButton(StashText.Lock);
            m_lockButton.IsAutoCheckingEnabled = true;
            AttachLockOverlays(player);
        }

        m_undoButton = AddButton(StashText.Undo);
    }

    /// <summary>给玩家自己的每个槽位盖一层锁定标记（平时不拦点击，只画角标）。</summary>
    private void AttachLockOverlays(PanelContainer player)
    {
        for (int i = 0; i < player.Widgets.Count; i++)
        {
            var overlay = new SlotLockOverlay(player.SlotIndexes[i]);
            player.Widgets[i].Children.Add(overlay);
            m_lockOverlays.Add(overlay);
        }
    }

    /// <summary>没有玩家库存这一侧就没什么可做的（整理和搬运都以它为主体），别挂了。</summary>
    public static bool IsUseful(PanelContainer? player, PanelContainer? container) => player != null;

    private ButtonWidget AddButton(string text)
    {
        var button = new BevelledButtonWidget
        {
            Text = text,
            Size = new Vector2(60f, 30f),
            Margin = new Vector2(2f, 0f),
        };

        Children.Add(button);
        return button;
    }

    public override void Update()
    {
        m_undoButton.IsEnabled = StashOperations.CanUndo;

        bool editingLocks = m_lockButton is { IsChecked: true };
        foreach (SlotLockOverlay overlay in m_lockOverlays)
        {
            overlay.EditMode = editingLocks;
        }

        // 锁定模式下先别急着整理/搬运，避免玩家边点格子边误触。
        if (editingLocks)
        {
            return;
        }

        if (m_sortButton is { IsClicked: true } && m_player != null)
        {
            int sorted = StashOperations.Sort(m_player);
            Notify(sorted > 0 ? StashText.Sorted(sorted) : StashText.Nothing);
        }

        if (m_depositButton is { IsClicked: true })
        {
            Transfer(m_player, m_container, TransferMode.Smart);
        }

        if (m_depositAllButton is { IsClicked: true })
        {
            Transfer(m_player, m_container, TransferMode.All);
        }

        if (m_takeButton is { IsClicked: true })
        {
            Transfer(m_container, m_player, TransferMode.All);
        }

        if (m_backpackButton is { IsClicked: true } && m_gui?.m_componentPlayer != null)
        {
            StashBackpack.Open(m_gui.m_componentPlayer);
        }

        if (m_undoButton.IsClicked)
        {
            Notify(StashOperations.Undo() ? StashText.Undone : StashText.NothingToUndo);
        }
    }

    private void Transfer(PanelContainer? from, PanelContainer? to, TransferMode mode)
    {
        if (from == null || to == null)
        {
            return;
        }

        int moved = StashOperations.Deposit(from, to, mode);
        Notify(moved > 0 ? StashText.Moved(moved) : StashText.NothingMoved);
    }


    private void Notify(string message)
    {
        m_gui?.DisplaySmallMessage(message, Color.White, blinking: false, playNotificationSound: false);
        AudioManager.PlaySound("Audio/UI/ButtonClick", 1f, 0f, 0f);
    }
}
