using Engine.Input;
using Game;

namespace Stash.Game;

/// <summary>
/// 快捷键。目前只有一个：**B 打开背包**。
///
/// 走 <c>ModLoader.UpdateInput</c> 钩子——SC 没有给 Mod 的按键注册表，
/// 自己读 <see cref="WidgetInput.IsKeyDownOnce"/> 是唯一的路子。
///
/// 顺带负责一件事：**打字时把输入吃掉**。存储终端的搜索框拿到焦点后，
/// 玩家敲的每个字母原本还会被原版和别的 Mod 当成热键（实机反馈"搜索时会触发其他模组热键"）。
/// </summary>
public static class StashHotkeys
{
    /// <summary>有输入框正在接收文字时置真，此时所有按键都不该被当成热键。</summary>
    public static bool TypingInProgress { get; set; }

    public static void Update(ComponentInput componentInput, WidgetInput input)
    {
        if (componentInput?.m_componentPlayer == null || input == null)
        {
            return;
        }

        if (TypingInProgress)
        {
            // 原版在本帧更早的时候已经把按键读进 m_playerInput 了，这里一并清掉，
            // 否则打字会让人物边走边转视角。
            componentInput.m_playerInput = default;
            input.Clear();
            return;
        }

        if (input.IsKeyDownOnce(Key.B) && componentInput.m_componentPlayer.ComponentGui.ModalPanelWidget == null)
        {
            StashBackpack.Open(componentInput.m_componentPlayer);
        }
    }
}
