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

            // **绝对不能调 input.Clear()**——它会把 m_mouseDownPoint 置空。
            // 点击是"按下记一帧、抬起才合成 Click"的状态机，按下的那一帧被我们清掉，
            // 抬起时就永远合不出 Click。结果是搜索框一拿到焦点，整个界面的鼠标全死：
            // 点不掉焦点、按钮没反应、物品也拖不动（实机反馈"退不出去，别的功能都测试不了"）。
            //
            // 只置 m_isCleared：它单独作用时只挡住 IsKeyDownOnce / LastKey 这类**键盘**查询，
            // Click / Tap / Press 这几个属性不受它管，鼠标照常。
            // 而且下一帧 WidgetInput.Update() 开头就会把它复位，不会积累。
            input.m_isCleared = true;

            // Back/Cancel 是原版把 Esc 翻译过来的普通属性，m_isCleared 管不到。
            // 不清的话，打字时按 Esc 会连界面一起关掉——我们要的是"第一下退出搜索、第二下才关界面"。
            input.Back = false;
            input.Cancel = false;
            return;
        }

        if (input.IsKeyDownOnce(Key.B) && componentInput.m_componentPlayer.ComponentGui.ModalPanelWidget == null)
        {
            StashBackpack.Open(componentInput.m_componentPlayer);
        }
    }
}
