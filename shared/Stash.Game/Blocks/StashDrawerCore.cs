using Engine;
using Game;
using GameEntitySystem;
using TemplatesDatabase;

namespace Stash.Game;

/// <summary>
/// 抽屉的平台无关逻辑：放置建实体、点一下存入 / 空手点取出一组。
/// 抽屉不开界面——"不用开界面就能存取"正是它相对箱子的意义。
/// </summary>
public static class StashDrawerCore
{
    public static ComponentBlockEntity? OnDrawerAdded(
        Project project,
        SubsystemBlockEntities blockEntities,
        int value,
        int x,
        int y,
        int z)
    {
        StashDrawerTier? tier = StashDrawerTiers.ByBlockIndex(Terrain.ExtractContents(value));
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

    /// <summary>
    /// 交互：手上有可堆叠物品就把背包里所有同种物品都塞进来；空手（或拿着别的东西）就取出一组。
    /// </summary>
    public static bool Interact(ComponentStashDrawer drawer, ComponentMiner miner)
    {
        IInventory inventory = miner?.Inventory;
        if (drawer == null || inventory == null)
        {
            return false;
        }

        int activeSlot = inventory.ActiveSlotIndex;
        int heldValue = activeSlot >= 0 ? inventory.GetSlotValue(activeSlot) : 0;
        int heldCount = activeSlot >= 0 ? inventory.GetSlotCount(activeSlot) : 0;

        if (heldCount > 0 && CanStore(drawer, heldValue))
        {
            int moved = DepositAllOfType(drawer, inventory, heldValue);
            Notify(miner, moved > 0 ? StashText.DrawerStored(moved) : StashText.DrawerFull);
            return true;
        }

        int taken = TakeOneStack(drawer, inventory);
        Notify(miner, taken > 0 ? StashText.DrawerTaken(taken) : StashText.DrawerEmpty);
        return true;
    }

    private static bool CanStore(ComponentStashDrawer drawer, int value)
    {
        if (drawer.GetSlotCapacity(0, value) <= 0)
        {
            return false;
        }

        int stored = drawer.StoredValue;
        return stored == 0 || Shared.Items.ItemValue.SameItem(stored, value);
    }

    /// <summary>把玩家身上所有这种物品都收进来，返回搬进去的数量。</summary>
    private static int DepositAllOfType(ComponentStashDrawer drawer, IInventory inventory, int value)
    {
        int capacity = drawer.GetSlotCapacity(0, value);
        int room = capacity - drawer.StoredCount;
        int moved = 0;

        for (int slot = 0; slot < inventory.SlotsCount && room > 0; slot++)
        {
            int count = inventory.GetSlotCount(slot);
            if (count <= 0 || !Shared.Items.ItemValue.SameItem(inventory.GetSlotValue(slot), value))
            {
                continue;
            }

            int take = MathUtils.Min(count, room);
            int removed = inventory.RemoveSlotItems(slot, take);
            if (removed <= 0)
            {
                continue;
            }

            drawer.AddSlotItems(0, value, removed);
            moved += removed;
            room -= removed;
        }

        return moved;
    }

    /// <summary>取出一组（按该物品的原版堆叠上限），放不下就少给。</summary>
    private static int TakeOneStack(ComponentStashDrawer drawer, IInventory inventory)
    {
        int value = drawer.StoredValue;
        int stored = drawer.StoredCount;
        if (value == 0 || stored <= 0)
        {
            return 0;
        }

        int stackSize = MathUtils.Max(1, BlocksManager.Blocks[Terrain.ExtractContents(value)].GetMaxStacking(value));
        int wanted = MathUtils.Min(stackSize, stored);
        int removed = drawer.RemoveSlotItems(0, wanted);
        if (removed <= 0)
        {
            return 0;
        }

        int leftover = ComponentInventoryBase.AcquireItems(inventory, value, removed);
        if (leftover > 0)
        {
            // 背包塞不下就放回抽屉，别把玩家的东西弄丢。
            drawer.AddSlotItems(0, value, leftover);
        }

        return removed - leftover;
    }

    private static void Notify(ComponentMiner miner, string message) =>
        miner.ComponentPlayer?.ComponentGui.DisplaySmallMessage(message, Color.White, blinking: false, playNotificationSound: false);
}
