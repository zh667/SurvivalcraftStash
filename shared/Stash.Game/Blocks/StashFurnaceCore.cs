using Engine;
using Game;
using GameEntitySystem;
using TemplatesDatabase;

namespace Stash.Game;

/// <summary>
/// 分级熔炉的平台无关逻辑：放置、破坏、升级、开界面。
/// 结构和 <see cref="StashChestCore"/> 一致，两个平台的行为类只是薄壳。
/// </summary>
public static class StashFurnaceCore
{
    /// <summary>
    /// 建立方块实体。和箱子一样走底层 API（FindDatabaseObject → PopulateFromDatabaseObject →
    /// CreateEntity），因为插件版没有联机版的 <c>SubsystemBlockEntities.CreateBlockEntity</c>。
    /// </summary>
    public static ComponentBlockEntity? OnFurnaceAdded(
        Project project,
        SubsystemBlockEntities blockEntities,
        int value,
        int x,
        int y,
        int z)
    {
        StashFurnaceTier? tier = StashFurnaceTiers.ByBlockIndex(Terrain.ExtractContents(value));
        if (tier == null)
        {
            return null;
        }

        if (StashPlatform.IsReady && !StashPlatform.Current.IsAuthoritative)
        {
            return null;
        }

        if (blockEntities.GetBlockEntity(x, y, z) != null)
        {
            return null;
        }

        DatabaseObject databaseObject = project.GameDatabase.Database.FindDatabaseObject(
            tier.EntityTemplateName, project.GameDatabase.EntityTemplateType, throwIfNotFound: true);

        var values = new ValuesDictionary();
        values.PopulateFromDatabaseObject(databaseObject);
        values.GetValue<ValuesDictionary>("BlockEntity").SetValue("Coordinates", new Point3(x, y, z));

        Entity entity = project.CreateEntity(values);
        project.AddEntity(entity);
        return entity.FindComponent<ComponentBlockEntity>();
    }

    public static void OnFurnaceRemoved(Project project, SubsystemBlockEntities blockEntities, int x, int y, int z)
    {
        ComponentBlockEntity blockEntity = blockEntities.GetBlockEntity(x, y, z);
        if (blockEntity == null)
        {
            return;
        }

        var position = new Vector3(x, y, z) + new Vector3(0.5f);
        foreach (IInventory inventory in blockEntity.Entity.FindComponents<IInventory>())
        {
            inventory.DropAllItems(position);
        }

        project.RemoveEntity(blockEntity.Entity, disposeEntity: true);
    }

    /// <summary>
    /// 原地升级：换方块 → 把炉内的东西搬进新炉子 → 由调用方消耗升级件。
    ///
    /// 熔炉的槽位是**有含义的**（0..n 原料、倒数第三格燃料、倒数第二格产物、最后一格残留），
    /// 所以不能像箱子那样把内容压平了顺序重填——必须按槽位一一对应搬过去。
    /// 各档格子数相同（都是 5），对应关系是直接的。
    /// </summary>
    public static bool TryUpgrade(
        Project project,
        SubsystemTerrain terrain,
        SubsystemBlockEntities blockEntities,
        int x,
        int y,
        int z,
        int upgradeData,
        out string failureReason)
    {
        failureReason = string.Empty;

        int oldValue = terrain.Terrain.GetCellValue(x, y, z);
        int oldContents = Terrain.ExtractContents(oldValue);
        StashFurnaceTier? target = StashFurnaceTiers.ResolveUpgrade(oldContents, upgradeData);
        if (target == null)
        {
            failureReason = StashText.FurnaceUpgradeWrongTier;
            return false;
        }

        ComponentBlockEntity oldEntity = blockEntities.GetBlockEntity(x, y, z);
        Dictionary<int, (int Value, int Count)> contents = oldEntity != null
            ? ReadSlots(oldEntity)
            : new Dictionary<int, (int, int)>();

        if (oldEntity != null)
        {
            StashDiag.Log($"熔炉升级：({x},{y},{z}) 旧炉内容 "
                + $"{StashDiag.Describe(FindFurnace(oldEntity))}");

            // 内容已经读出来了，直接丢弃旧实体，免得 OnBlockRemoved 把东西撒一地。
            ClearSlots(oldEntity);
            project.RemoveEntity(oldEntity.Entity, disposeEntity: true);
        }

        int data = Terrain.ExtractData(oldValue);
        terrain.ChangeCell(x, y, z, Terrain.MakeBlockValue(target.Index, 0, data));

        ComponentBlockEntity newEntity = blockEntities.GetBlockEntity(x, y, z);
        if (newEntity == null)
        {
            // 到这一步旧实体已经没了，内容只在 contents 这个字典里。
            // 直接 return 会把玩家的东西**凭空吞掉**——宁可撒一地也不能没。
            StashDiag.Warn($"熔炉升级：({x},{y},{z}) ChangeCell 之后取不到新方块实体，"
                + $"把 {contents.Count} 组东西掉在地上");
            DropContents(project, contents, x, y, z);
            failureReason = StashText.UpgradeFailed;
            return false;
        }

        int leftover = WriteSlots(newEntity, contents);
        if (leftover > 0)
        {
            StashDiag.Warn($"熔炉升级：有 {leftover} 组东西没能写进新炉子（槽位对不上），掉在地上");
        }

        StashDiag.Log($"熔炉升级：({x},{y},{z}) → {target.EntityTemplateName}，新炉内容 "
            + $"{StashDiag.Describe(FindFurnace(newEntity))}");
        return true;
    }

    /// <summary>
    /// 兜底：把搬不进去的东西变成掉落物。
    /// 升级是玩家主动操作，出岔子时"东西撒了一地"能捡回来，"东西没了"就是灾难。
    /// </summary>
    private static void DropContents(
        Project project, Dictionary<int, (int Value, int Count)> contents, int x, int y, int z)
    {
        var pickables = project.FindSubsystem<SubsystemPickables>(throwOnError: false);
        if (pickables == null)
        {
            StashDiag.Warn("熔炉升级：连 SubsystemPickables 都取不到，这些东西真的没了");
            return;
        }

        var position = new Vector3(x, y, z) + new Vector3(0.5f);
        foreach ((int value, int count) in contents.Values)
        {
            pickables.AddPickable(value, count, position, Vector3.Zero, null);
        }
    }

    private static ComponentFurnace? FindFurnace(ComponentBlockEntity blockEntity) =>
        blockEntity.Entity.FindComponent<ComponentFurnace>(throwOnError: false);

    private static Dictionary<int, (int Value, int Count)> ReadSlots(ComponentBlockEntity blockEntity)
    {
        var contents = new Dictionary<int, (int, int)>();
        ComponentFurnace? furnace = FindFurnace(blockEntity);
        if (furnace == null)
        {
            return contents;
        }

        for (int slot = 0; slot < furnace.SlotsCount; slot++)
        {
            int count = furnace.GetSlotCount(slot);
            if (count > 0)
            {
                contents[slot] = (furnace.GetSlotValue(slot), count);
            }
        }

        return contents;
    }

    private static void ClearSlots(ComponentBlockEntity blockEntity)
    {
        ComponentFurnace? furnace = FindFurnace(blockEntity);
        if (furnace == null)
        {
            return;
        }

        for (int slot = 0; slot < furnace.SlotsCount; slot++)
        {
            int count = furnace.GetSlotCount(slot);
            if (count > 0)
            {
                furnace.RemoveSlotItems(slot, count);
            }
        }
    }

    /// <returns>没能写进去的组数。调用方负责把它们掉在地上。</returns>
    private static int WriteSlots(
        ComponentBlockEntity blockEntity, Dictionary<int, (int Value, int Count)> contents)
    {
        ComponentFurnace? furnace = FindFurnace(blockEntity);
        if (furnace == null)
        {
            return contents.Count;
        }

        var leftover = new Dictionary<int, (int, int)>();

        foreach ((int slot, (int value, int count)) in contents)
        {
            // 槽位越界，或者那一格根本不收这种东西（燃料格只收燃料），都算写不进去。
            if (slot < furnace.SlotsCount && furnace.GetSlotCapacity(slot, value) >= count)
            {
                furnace.AddSlotItems(slot, value, count);
            }
            else
            {
                leftover[slot] = (value, count);
            }
        }

        if (leftover.Count > 0)
        {
            Point3 coordinates = blockEntity.Coordinates;
            DropContents(blockEntity.Entity.Project, leftover, coordinates.X, coordinates.Y, coordinates.Z);
        }

        return leftover.Count;
    }

    /// <summary>
    /// 打开界面。**直接复用原版 <c>FurnaceWidget</c>**——它的构造函数就收
    /// <c>(IInventory, ComponentFurnace)</c>，我们的组件是 <c>ComponentFurnace</c> 的子类，
    /// 火焰、进度条、五个槽位全都是现成的，一行新界面代码都不用写。
    ///
    /// 档位不往面板上加标签（原版这个面板已经排满了，加东西容易压到格子——
    /// 实机反馈里"UI 重叠"吃过一次亏），改成开的时候飘一句提示。
    /// </summary>
    public static bool OpenFurnaceUi(ComponentPlayer player, ComponentFurnace furnace, StashFurnaceTier tier)
    {
        if (player?.ComponentGui == null)
        {
            return false;
        }

        player.ComponentGui.ModalPanelWidget = new FurnaceWidget(player.ComponentMiner.Inventory, furnace);
        player.ComponentGui.DisplaySmallMessage(
            StashText.FurnaceOpened(tier), Color.White, blinking: false, playNotificationSound: false);
        AudioManager.PlaySound("Audio/UI/ButtonClick", 1f, 0f, 0f);
        return true;
    }
}
