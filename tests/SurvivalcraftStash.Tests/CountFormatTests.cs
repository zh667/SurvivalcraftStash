using Xunit;

namespace SurvivalcraftStash.Tests;

/// <summary>
/// 抽屉数量的缩写规则。这段逻辑本身不依赖游戏，抄一份到测试里守住行为
/// （真正的实现在 Stash.Game 里，那层要引用游戏程序集，Linux 上跑不了单测）。
/// </summary>
public class CountFormatTests
{
    private static string Format(int count)
    {
        if (count < 10_000)
        {
            return count.ToString();
        }

        if (count < 1_000_000)
        {
            return (count / 1000f).ToString("0.#") + "k";
        }

        return (count / 1_000_000f).ToString("0.##") + "M";
    }

    [Theory]
    [InlineData(0, "0")]
    [InlineData(40, "40")]
    [InlineData(9999, "9999")]
    [InlineData(10_000, "10k")]
    [InlineData(40_960, "41k")]
    [InlineData(163_840, "163.8k")]
    [InlineData(2_000_000, "2M")]
    public void 大数缩写为k和M(int count, string expected) => Assert.Equal(expected, Format(count));
}
