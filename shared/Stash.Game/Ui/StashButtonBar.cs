using Engine;
using Game;

namespace Stash.Game;

/// <summary>
/// 挂在库存界面上的一个「整理」按钮。
///
/// **只有整理，也只出现在两个地方：玩家物品栏、背包。**
/// 箱子和存储终端里怎么摆是玩家自己的事，终端的显示本来就是排好序的，
/// 那两个界面不挂任何按钮。存入/全存/取出/锁定/撤销这些实机试下来只是碍事，全部去掉。
///
/// 用代码搭而不是加 XML 布局：XML 要走 ContentManager 覆盖，容易和别的 Mod / 材质包打架；
/// 代码搭出来的控件只属于这一份界面实例，界面关掉就跟着没了。
/// </summary>
public sealed class StashButtonBar : StackPanelWidget
{
    private readonly ComponentGui? m_gui;
    private readonly PanelContainer m_target;
    private readonly ButtonWidget m_sortButton;

    /// <param name="target">整理谁——玩家物品栏，或者背包。</param>
    public StashButtonBar(ComponentGui? gui, PanelContainer target)
    {
        m_gui = gui;
        m_target = target;

        Direction = LayoutDirection.Horizontal;
        HorizontalAlignment = WidgetAlignment.Center;

        // 贴面板底边。放顶部会压住标题，放面板外（负坐标）又可能被裁掉。
        VerticalAlignment = WidgetAlignment.Far;
        Margin = new Vector2(0f, 6f);

        m_sortButton = new BevelledButtonWidget
        {
            Text = StashText.Sort,
            Size = new Vector2(72f, 30f),
        };
        Children.Add(m_sortButton);
    }

    public override void Update()
    {
        if (!m_sortButton.IsClicked)
        {
            return;
        }

        int sorted = StashOperations.Sort(m_target);
        m_gui?.DisplaySmallMessage(
            sorted > 0 ? StashText.Sorted(sorted) : StashText.Nothing,
            Color.White,
            blinking: false,
            playNotificationSound: false);
        AudioManager.PlaySound("Audio/UI/ButtonClick", 1f, 0f, 0f);
    }
}
