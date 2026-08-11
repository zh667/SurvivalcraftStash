using Engine;
using Game;
using GameEntitySystem;
using TemplatesDatabase;

namespace Stash.Game;

/// <summary>
/// 分级箱子的平台无关逻辑：放置、破坏、升级、开界面。
///
/// 两个平台的方块行为基类不一样（插件版有 <c>SubsystemEntityBlockBehavior</c>，联机版没有），
/// 所以行为类各写各的薄壳，真正的逻辑都在这里。
/// </summary>
public static class StashChestCore
{
    /// <summary>
    /// 建立方块实体。
    ///
    /// 不走联机版的 <c>SubsystemBlockEntities.CreateBlockEntity</c>——插件版没有这个方法。
    /// 两边其实都是同一套底层调用（FindDatabaseObject → PopulateFromDatabaseObject → CreateEntity），
    /// 这里直接用底层 API，一份代码两版通用。
    ///
    /// 联机版只有服务端建实体（原版也是这么做的），所以先问平台适配器谁是权威方。
    /// </summary>
    public static ComponentBlockEntity? OnChestAdded(
        Project project,
        SubsystemBlockEntities blockEntities,
        int value,
        int x,
        int y,
        int z)
    {
        StashChestTier? tier = StashChestTiers.ByBlockIndex(Terrain.ExtractContents(value));
        if (tier == null)
        {
            return null;
        }

        if (StashPlatform.IsReady && !StashPlatform.Current.IsAuthoritative)
        {
            return null;
        }

        var point = new Point3(x, y, z);
        if (blockEntities.GetBlockEntity(x, y, z) != null)
        {
            return null;
        }

        DatabaseObject databaseObject = project.GameDatabase.Database.FindDatabaseObject(
            tier.EntityTemplateName, project.GameDatabase.EntityTemplateType, throwIfNotFound: true);

        var values = new ValuesDictionary();
        values.PopulateFromDatabaseObject(databaseObject);
        values.GetValue<ValuesDictionary>("BlockEntity").SetValue("Coordinates", point);

        Entity entity = project.CreateEntity(values);
        project.AddEntity(entity);
        return entity.FindComponent<ComponentBlockEntity>();
    }

    public static void OnChestRemoved(Project project, SubsystemBlockEntities blockEntities, int x, int y, int z)
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
    /// 原地升级：换方块 → 把旧内容搬进新箱子 → 消耗一个升级件。
    /// "内容不丢"是这条功能的全部意义，所以先搬内容再删旧实体，任何一步失败都不动方块。
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
        StashChestTier? target = StashChestTiers.ResolveUpgrade(oldContents, upgradeData);
        if (target == null)
        {
            failureReason = StashText.UpgradeWrongTier;
            return false;
        }

        ComponentBlockEntity oldEntity = blockEntities.GetBlockEntity(x, y, z);
        List<(int Value, int Count)> contents = oldEntity != null ? ReadContents(oldEntity) : new List<(int, int)>();

        if (contents.Count > target.SlotsCount)
        {
            // 理论上不会发生（升级只会变大），留个兜底以防别的 Mod 改了容量。
            failureReason = StashText.UpgradeNoRoom;
            return false;
        }

        if (oldEntity != null)
        {
            // 先把内容读出来了，这里直接丢弃旧实体，避免 OnBlockRemoved 把东西撒一地。
            ClearContents(oldEntity);
            project.RemoveEntity(oldEntity.Entity, disposeEntity: true);
        }

        int data = Terrain.ExtractData(oldValue);
        terrain.ChangeCell(x, y, z, Terrain.MakeBlockValue(target.Index, 0, data));

        ComponentBlockEntity newEntity = blockEntities.GetBlockEntity(x, y, z);
        if (newEntity == null)
        {
            failureReason = StashText.UpgradeFailed;
            return false;
        }

        WriteContents(newEntity, contents);
        return true;
    }

    private static List<(int Value, int Count)> ReadContents(ComponentBlockEntity blockEntity)
    {
        var contents = new List<(int, int)>();
        foreach (IInventory inventory in blockEntity.Entity.FindComponents<IInventory>())
        {
            for (int slot = 0; slot < inventory.SlotsCount; slot++)
            {
                int count = inventory.GetSlotCount(slot);
                if (count > 0)
                {
                    contents.Add((inventory.GetSlotValue(slot), count));
                }
            }
        }

        return contents;
    }

    private static void ClearContents(ComponentBlockEntity blockEntity)
    {
        foreach (IInventory inventory in blockEntity.Entity.FindComponents<IInventory>())
        {
            for (int slot = 0; slot < inventory.SlotsCount; slot++)
            {
                int count = inventory.GetSlotCount(slot);
                if (count > 0)
                {
                    inventory.RemoveSlotItems(slot, count);
                }
            }
        }
    }

    private static void WriteContents(ComponentBlockEntity blockEntity, List<(int Value, int Count)> contents)
    {
        IInventory? inventory = blockEntity.Entity.FindComponent<IInventory>(throwOnError: false);
        if (inventory == null)
        {
            return;
        }

        int slot = 0;
        foreach ((int value, int count) in contents)
        {
            if (slot >= inventory.SlotsCount)
            {
                break;
            }

            inventory.AddSlotItems(slot++, value, count);
        }
    }

    /// <summary>打开界面。调用方负责保证自己在客户端（或单人）一侧。</summary>
    public static bool OpenChestUi(ComponentPlayer player, IInventory chestInventory, StashChestTier tier)
    {
        if (player?.ComponentGui == null)
        {
            return false;
        }

        player.ComponentGui.ModalPanelWidget = new StashContainerWidget(
            StashText.ChestName(tier),
            chestInventory,
            firstSlot: 0,
            slots: tier.SlotsCount,
            columns: tier.Columns,
            slotSize: tier.SlotSize,
            player.ComponentMiner.Inventory,
            player);
        AudioManager.PlaySound("Audio/UI/ButtonClick", 1f, 0f, 0f);
        return true;
    }
}
