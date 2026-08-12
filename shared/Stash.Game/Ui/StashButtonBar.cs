using Engine;
using Game;

namespace Stash.Game;

/// <summary>整理谁：决定按钮上写什么字。</summary>
public enum StashSortKind
{
    PlayerInventory,
    Backpack,
    Container,
}

/// <summary>挂在界面底部的整理按钮条，一块库存一个按钮。</summary>
public sealed class StashButtonBar : StackPanelWidget
{
    /// <summary>按钮条自己的高度。给界面加高的时候按这个值加，加完按钮就不会压到格子上。</summary>
    public const float BarHeight = 40f;

    private readonly ComponentGui? m_gui;
    private readonly List<(ButtonWidget Button, PanelContainer Target)> m_buttons = new();
    private readonly StashSideToggle? m_sideToggle;
    private readonly ButtonWidget? m_sideButton;

    /// <param name="targets">这个界面里所有值得整理的库存，按界面上从左到右的顺序。</param>
    /// <param name="allowSideToggle">
    /// 允不允许挂"物品栏 / 背包"切换。我们自己的容器界面已经自带一个，别挂第二个。
    /// </param>
    public StashButtonBar(
        ComponentGui? gui,
        List<(StashSortKind Kind, PanelContainer Target)> targets,
        bool allowSideToggle = true)
    {
        m_gui = gui;

        Direction = LayoutDirection.Horizontal;
        HorizontalAlignment = WidgetAlignment.Center;

        // 贴面板底边。注入方会先把面板加高 BarHeight，所以这一条落在新腾出来的空白里，
        // 不会像之前那样压在最后一排格子上（实机反馈"UI 和背包格子重叠了"）。
        VerticalAlignment = WidgetAlignment.Far;
        Margin = new Vector2(0f, 5f);

        foreach ((StashSortKind kind, PanelContainer target) in targets)
        {
            // 只有一块库存时不用啰嗦"整理什么"，就写"整理"。
            string text = targets.Count == 1
                ? StashText.Sort
                : kind switch
                {
                    StashSortKind.Backpack => StashText.SortBackpack,
                    StashSortKind.Container => StashText.SortChest,
                    _ => StashText.SortInventory,
                };

            var button = new BevelledButtonWidget
            {
                Text = text,
                Size = new Vector2(targets.Count == 1 ? 84f : 118f, 30f),
                Margin = new Vector2(4f, 0f),
            };

            Children.Add(button);
            m_buttons.Add((button, target));
        }

        // 原版箱子/物品栏这些界面本来没有"切到背包"的入口，这里补上——
        // 分级箱子界面早就有了，实机反馈说原版箱子也想要。
        if (!allowSideToggle || gui?.m_componentPlayer is not { } player)
        {
            return;
        }

        PanelContainer? inventory = null;
        foreach ((StashSortKind kind, PanelContainer target) in targets)
        {
            if (kind == StashSortKind.PlayerInventory)
            {
                inventory = target;
                break;
            }
        }

        if (inventory == null || StashBackpack.GetWornTier(player) == null)
        {
            return;
        }

        m_sideToggle = new StashSideToggle(player, inventory);
        m_sideButton = new BevelledButtonWidget
        {
            Text = m_sideToggle.Label,
            Size = new Vector2(118f, 30f),
            Margin = new Vector2(4f, 0f),
        };
        Children.Add(m_sideButton);
    }

    public override void Update()
    {
        if (m_sideButton is { IsClicked: true } && m_sideToggle != null)
        {
            m_sideToggle.Advance();
            m_sideButton.Text = m_sideToggle.Label;
            AudioManager.PlaySound("Audio/UI/ButtonClick", 1f, 0f, 0f);
        }

        foreach ((ButtonWidget button, PanelContainer target) in m_buttons)
        {
            if (!button.IsClicked)
            {
                continue;
            }

            int sorted = StashOperations.Sort(target);
            m_gui?.DisplaySmallMessage(
                sorted > 0 ? StashText.Sorted(sorted) : StashText.Nothing,
                Color.White,
                blinking: false,
                playNotificationSound: false);
            AudioManager.PlaySound("Audio/UI/ButtonClick", 1f, 0f, 0f);
        }
    }
}
