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

    /// <summary>画布实在不够高时能压到的下限。再小按钮就点不准了（触屏尤其）。</summary>
    public const float MinBarHeight = 28f;

    private readonly ComponentGui? m_gui;
    private readonly List<(ButtonWidget Button, PanelContainer Target)> m_buttons = new();
    private readonly StashSideToggle? m_sideToggle;
    private readonly ButtonWidget? m_sideButton;
    private readonly ButtonWidget? m_recipeButton;
    private readonly StashCraftTarget? m_craftTarget;

    /// <param name="targets">这个界面里所有值得整理的库存，按界面上从左到右的顺序。</param>
    /// <param name="allowSideToggle">
    /// 允不允许挂"物品栏 / 背包"切换。我们自己的容器界面已经自带一个，别挂第二个。
    /// </param>
    /// <param name="barHeight">
    /// 注入方实际腾出来的高度。画布不够高时会小于 <see cref="BarHeight"/>，
    /// 按钮得跟着变矮，否则就会被 ClampToBounds 切掉下半截。
    /// </param>
    /// <param name="craftTarget">
    /// 这个界面里的合成格（按 E 是 2×2，工作台是 3×3），没有就传 null。
    /// 配方浏览器的"＋"按钮要往它里面填料。
    /// </param>
    public StashButtonBar(
        ComponentGui? gui,
        List<(StashSortKind Kind, PanelContainer Target)> targets,
        bool allowSideToggle = true,
        float barHeight = BarHeight,
        StashCraftTarget? craftTarget = null)
    {
        m_gui = gui;
        m_craftTarget = craftTarget;

        Direction = LayoutDirection.Horizontal;
        HorizontalAlignment = WidgetAlignment.Center;

        // 贴面板底边。注入方会先把面板加高 barHeight，所以这一条落在新腾出来的空白里，
        // 不会像之前那样压在最后一排格子上（实机反馈"UI 和背包格子重叠了"）。
        VerticalAlignment = WidgetAlignment.Far;

        float margin = MathUtils.Clamp((barHeight - 30f) * 0.5f, 1f, 5f);
        float buttonHeight = barHeight - margin * 2f;
        Margin = new Vector2(0f, margin);

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
                Size = new Vector2(targets.Count == 1 ? 84f : 118f, buttonHeight),
                Margin = new Vector2(4f, 0f),
            };

            Children.Add(button);
            m_buttons.Add((button, target));
        }

        // 配方浏览器。挂在这里而不是单开一个入口：这条按钮条本来就注入到
        // 玩家物品栏 / 箱子 / 工作台每一个界面上，正好是玩家想查配方的时机。
        if (gui != null)
        {
            m_recipeButton = new BevelledButtonWidget
            {
                Text = StashText.RecipeBrowser,
                Size = new Vector2(72f, buttonHeight),
                Margin = new Vector2(4f, 0f),
            };
            Children.Add(m_recipeButton);
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
            Size = new Vector2(118f, buttonHeight),
            Margin = new Vector2(4f, 0f),
        };
        Children.Add(m_sideButton);
    }

    public override void Update()
    {
        if (m_recipeButton is { IsClicked: true } && m_gui != null)
        {
            ComponentGui gui = m_gui;
            StashOverlayHost.Toggle(gui, () => new StashRecipeBrowser(gui, m_craftTarget, CraftSources()));
            AudioManager.PlaySound("Audio/UI/ButtonClick", 1f, 0f, 0f);
        }

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

    /// <summary>
    /// "＋"填料能从哪儿取东西：玩家物品栏优先，然后是行囊。
    /// 第一个还兼作"腾空合成格时东西退回哪里"，所以物品栏必须排头。
    /// （存储网络那一路属于无线合成终端，第二批再接。）
    /// </summary>
    private List<IInventory> CraftSources()
    {
        var sources = new List<IInventory>();

        if (m_gui?.m_componentPlayer is not { } player)
        {
            return sources;
        }

        if (player.ComponentMiner?.Inventory is { } inventory)
        {
            sources.Add(inventory);
        }

        if (StashBackpack.GetInventory(player) is { } backpack)
        {
            sources.Add(backpack);
        }

        return sources;
    }
}
