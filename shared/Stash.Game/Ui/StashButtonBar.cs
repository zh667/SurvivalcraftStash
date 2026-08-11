using Engine;
using Game;
using Stash.Shared.Transfer;

namespace Stash.Game;

/// <summary>
/// 挂在库存界面上的一排按钮。**一个界面只挂一排**，避免两侧各挂一排互相压住。
///
/// 用代码搭而不是加 XML 布局：XML 要走 ContentManager 覆盖，容易和别的 Mod / 材质包打架；
/// 代码搭出来的控件只属于这一份界面实例，界面关掉就跟着没了。
/// </summary>
public sealed class StashButtonBar : StackPanelWidget
{
    private readonly ComponentGui? m_gui;
    private readonly PanelContainer? m_player;
    private readonly PanelContainer? m_container;

    private readonly ButtonWidget? m_sortContainerButton;
    private readonly ButtonWidget? m_sortPlayerButton;
    private readonly ButtonWidget? m_depositButton;
    private readonly ButtonWidget? m_depositAllButton;
    private readonly ButtonWidget? m_takeButton;
    private readonly ButtonWidget? m_lockButton;
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

        if (container != null)
        {
            m_sortContainerButton = AddButton(StashText.Sort);
        }

        if (player != null)
        {
            m_sortPlayerButton = AddButton(container != null ? StashText.SortBackpack : StashText.Sort);
        }

        if (player != null && container != null)
        {
            m_depositButton = AddButton(StashText.Deposit);
            m_depositAllButton = AddButton(StashText.DepositAll);
            m_takeButton = AddButton(StashText.Restock);
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

    /// <summary>界面里没有任何可操作的容器时就别挂了。</summary>
    public static bool IsUseful(PanelContainer? player, PanelContainer? container) => player != null || container != null;

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

        if (m_sortContainerButton is { IsClicked: true } && m_container != null)
        {
            Report(StashOperations.Sort(m_container), StashText.Sorted, StashText.Nothing);
        }

        if (m_sortPlayerButton is { IsClicked: true } && m_player != null)
        {
            Report(StashOperations.Sort(m_player), StashText.Sorted, StashText.Nothing);
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

    private void Report(int amount, Func<int, string> success, string empty) =>
        Notify(amount > 0 ? success(amount) : empty);

    private void Notify(string message)
    {
        m_gui?.DisplaySmallMessage(message, Color.White, blinking: false, playNotificationSound: false);
        AudioManager.PlaySound("Audio/UI/ButtonClick", 1f, 0f, 0f);
    }
}
