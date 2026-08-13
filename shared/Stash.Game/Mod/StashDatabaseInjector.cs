using System.Xml.Linq;
using Engine;

namespace Stash.Game;

/// <summary>
/// 手工把"给玩家实体加一个组件"注入进游戏数据库。
///
/// **为什么不能直接写在 .xdb 里**：联机版 <c>ModsManager.Modify</c> 判重时写的是
/// <c>item.Attribute("Name").Name == change.Attribute("Name").Name</c>——
/// 比的是**属性名**（两边都是字面量 "Name"），不是属性值。
/// 于是只要目标节点里已经有任意一个 <c>MemberComponentTemplate</c>，
/// 新加的就被当成"重复的参数"直接丢掉。Player 模板下有一堆组件，必然命中。
///
/// 结果就是背包组件从来没挂到玩家身上：穿戴检测正常（那读的是衣物），
/// 但打开背包时找不到库存组件，于是什么都不发生。
/// SCTM 当年也是因为同一个坑才自己写的注入器。
/// </summary>
public static class StashDatabaseInjector
{
    /// <summary>玩家实体模板的 GUID（原版 Database.xml）。</summary>
    public const string PlayerEntityGuid = "4be6c1c5-d65d-4537-8a8b-a391969e6dc2";

    /// <summary>原版 Chest 组件模板，拿来当我们库存组件的继承父。</summary>
    public const string ChestComponentGuid = "7312ec8c-10c7-4133-85b4-4a35bd44c956";

    /// <summary>这些 GUID 是我们自己的，固定写死——每次生成新的会让存档里出现重复定义。</summary>
    private const string BackpackMemberGuid = "b6f1c0de-2f77-4f0a-8f2a-5c9d1f0a7e11";
    private const string BackpackClassGuid = "b6f1c0de-2f77-4f0a-8f2a-5c9d1f0a7e12";
    private const string BackpackSlotsGuid = "b6f1c0de-2f77-4f0a-8f2a-5c9d1f0a7e13";

    private const string CraftingGridMemberGuid = "bcde5dc9-130c-4b6c-9af2-87d05a25215f";
    private const string CraftingGridClassGuid = "d4fb82d9-24b5-4aed-b0fa-a12e136abb95";
    private const string CraftingGridSlotsGuid = "3424faf7-4114-466d-96ca-e5ccf82b2f4a";

    public static void Inject(XElement? database)
    {
        if (database == null)
        {
            return;
        }

        try
        {
            XElement? player = FindByGuid(database, PlayerEntityGuid);
            if (player == null)
            {
                Log.Warning("[Stash] 数据库里找不到玩家实体模板，背包组件没能挂上。");
                return;
            }

            AddCraftingGrid(database, player);

            if (FindByGuid(database, BackpackMemberGuid) != null)
            {
                return;
            }

            player.Add(new XElement("MemberComponentTemplate",
                new XAttribute("Name", "StashBackpack"),
                new XAttribute("Guid", BackpackMemberGuid),
                new XAttribute("InheritanceParent", ChestComponentGuid),
                new XElement("Parameter",
                    new XAttribute("Name", "Class"),
                    new XAttribute("Guid", BackpackClassGuid),
                    new XAttribute("Value", "Game.ComponentStashBackpack"),
                    new XAttribute("Type", "string")),
                new XElement("Parameter",
                    new XAttribute("Name", "SlotsCount"),
                    new XAttribute("Guid", BackpackSlotsGuid),
                    new XAttribute("Value", StashBackpackTiers.MaxSlots.ToString()),
                    new XAttribute("Type", "int"))));

            Log.Information("[Stash] 背包组件已注入玩家实体模板。");
        }
        catch (Exception exception)
        {
            Log.Warning($"[Stash] 注入背包组件失败：{exception.Message}");
        }
    }

    /// <summary>
    /// 给玩家挂一个 3×3 的合成格，无线合成终端要用。
    ///
    /// 组件类型是 <c>ComponentStashCraftingGrid</c> 而**不是** <c>ComponentCraftingTable</c>——
    /// 玩家身上本来就有一个 2×2 的合成台，再挂个同类型的会让
    /// <c>ComponentGui</c> 里那句 <c>FindComponent&lt;ComponentCraftingTable&gt;(throwOnError: true)</c>
    /// 出事，按 E 直接打不开物品栏。详见 ComponentStashCraftingGrid 的类注释。
    /// </summary>
    private static void AddCraftingGrid(XElement database, XElement player)
    {
        if (FindByGuid(database, CraftingGridMemberGuid) != null)
        {
            return;
        }

        player.Add(new XElement("MemberComponentTemplate",
            new XAttribute("Name", "StashCraftingGrid"),
            new XAttribute("Guid", CraftingGridMemberGuid),
            new XAttribute("InheritanceParent", ChestComponentGuid),
            new XElement("Parameter",
                new XAttribute("Name", "Class"),
                new XAttribute("Guid", CraftingGridClassGuid),
                new XAttribute("Value", "Game.ComponentStashCraftingGrid"),
                new XAttribute("Type", "string")),
            new XElement("Parameter",
                new XAttribute("Name", "SlotsCount"),
                new XAttribute("Guid", CraftingGridSlotsGuid),
                new XAttribute("Value", global::Game.ComponentStashCraftingGrid.RequiredSlots.ToString()),
                new XAttribute("Type", "int"))));

        Log.Information("[Stash] 合成格组件已注入玩家实体模板。");
    }

    private static XElement? FindByGuid(XElement root, string guid)
    {
        foreach (XElement element in root.DescendantsAndSelf())
        {
            if (string.Equals(element.Attribute("Guid")?.Value, guid, StringComparison.OrdinalIgnoreCase))
            {
                return element;
            }
        }

        return null;
    }
}
