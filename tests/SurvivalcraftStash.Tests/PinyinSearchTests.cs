using Stash.Shared.Network;
using Stash.Shared.Search;
using Xunit;

namespace SurvivalcraftStash.Tests;

public class PinyinSearchTests
{
    [Theory]
    [InlineData("鹅卵石", "eluanshi")]
    [InlineData("铜锭", "tongding")]
    [InlineData("钻石箱", "zuanshixiang")]
    [InlineData("铜行囊", "tongxingnang")]
    [InlineData("存储终端", "cunchuzhongduan")]
    public void 全拼是连写不带声调的(string chinese, string expected) =>
        Assert.Equal(expected, StashPinyin.Of(chinese));

    [Fact]
    public void 非汉字原样保留并转小写()
    {
        // 混着英文数字的名字也要能过，别把它们吃掉。
        Assert.Equal("txu", StashPinyin.Of("T恤"));
        Assert.Equal("tie 42", StashPinyin.Of("铁 42"));
    }

    [Fact]
    public void 表里没有的汉字原样留着不影响别的字()
    {
        // 别的模组的物品名可能带表外的字：那个字保持原样，中文直搜照样能中。
        string pinyin = StashPinyin.Of("錒石");
        Assert.Contains("shi", pinyin);
        Assert.Contains("錒", pinyin);
    }

    [Theory]
    [InlineData("eluanshi")]  // 全拼
    [InlineData("eluan")]     // 打一半
    [InlineData("luanshi")]   // 从中间开始也算（子串匹配）
    [InlineData("鹅卵")]      // 中文直搜
    public void 拼音和中文都能搜到(string query) =>
        Assert.True(NetworkSearch.Parse(query).Matches("鹅卵石", "Terrain"));

    [Fact]
    public void 英文标识也能搜()
    {
        NetworkSearch.Query query = NetworkSearch.Parse("copper");
        Assert.True(query.Matches("铜锭", "Items", "copperingot CopperIngotBlock"));
        Assert.False(query.Matches("铁锭", "Items", "ironingot IronIngotBlock"));
    }

    [Fact]
    public void 多个词要全部命中()
    {
        // "zuan shi" 两个词都得中；"zuan mu" 里的 mu 中不了。
        Assert.True(NetworkSearch.Parse("zuan shi").Matches("钻石箱", "Items"));
        Assert.False(NetworkSearch.Parse("zuan mu").Matches("钻石箱", "Items"));
    }

    [Fact]
    public void 竖线是或()
    {
        NetworkSearch.Query query = NetworkSearch.Parse("tong|tie");
        Assert.True(query.Matches("铜锭", "Items"));
        Assert.True(query.Matches("铁锭", "Items"));
        Assert.False(query.Matches("圆石", "Terrain"));
    }

    [Fact]
    public void 井号搜类别而不是名字()
    {
        Assert.True(NetworkSearch.Parse("#Items").Matches("铜锭", "Items"));
        Assert.False(NetworkSearch.Parse("#Items").Matches("铜锭", "Terrain"));

        // 类别词不参与拼音/英文匹配，只看类别本身。
        Assert.False(NetworkSearch.Parse("#tongding").Matches("铜锭", "Items"));
    }

    [Fact]
    public void 空查询放行一切() =>
        Assert.True(NetworkSearch.Parse("   ").Matches("随便什么", "Items"));
}
