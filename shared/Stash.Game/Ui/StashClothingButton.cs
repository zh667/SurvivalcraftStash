using Engine;
using Game;

namespace Stash.Game;

/// <summary>
/// 往衣物界面里加一个"背包"按钮。
///
/// 背包本身是穿在身上的衣物，拖进拖出是原版白送的（衣物槽就是普通库存槽），
/// 但要**打开**它得有个入口——这就是那个入口。
/// 挂钩点是 <c>ModLoader.ClothingWidgetOpen</c>，两个平台都有。
/// </summary>
public sealed class StashClothingButton : StackPanelWidget
{
    private readonly ComponentPlayer m_player;
    private readonly ButtonWidget m_openButton;

    public StashClothingButton(ComponentPlayer player)
    {
        m_player = player;
        Direction = LayoutDirection.Horizontal;
        HorizontalAlignment = WidgetAlignment.Center;
        VerticalAlignment = WidgetAlignment.Far;
        Margin = new Vector2(0f, 6f);

        m_openButton = new BevelledButtonWidget
        {
            Text = StashText.OpenBackpack,
            Size = new Vector2(80f, 32f),
        };
        Children.Add(m_openButton);
    }

    public override void Update()
    {
        m_openButton.IsEnabled = StashBackpack.GetWornTier(m_player) != null;

        if (m_openButton.IsClicked)
        {
            StashBackpack.Open(m_player);
        }
    }

    public static void Attach(ComponentGui gui, Widget clothingWidget)
    {
        if (gui?.m_componentPlayer == null || clothingWidget is not ContainerWidget host)
        {
            return;
        }

        foreach (Widget child in host.Children)
        {
            if (child is StashClothingButton)
            {
                return;
            }
        }

        host.Children.Add(new StashClothingButton(gui.m_componentPlayer));
    }
}
