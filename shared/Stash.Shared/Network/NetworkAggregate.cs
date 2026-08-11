using Stash.Shared.Inventory;
using Stash.Shared.Items;
using Stash.Shared.Sorting;

namespace Stash.Shared.Network;

/// <summary>网络里某一种物品的汇总。</summary>
public readonly record struct NetworkEntry(int Value, long Count);

/// <summary>一个容器在网络里的位置：第几个容器、里面的槽位快照。</summary>
public sealed record NetworkContainer(int ContainerIndex, IReadOnlyList<SlotSnapshot> Slots);

public static class NetworkAggregate
{
    /// <summary>把若干容器的内容合并成"物品 → 总数"的列表，并按给定方式排序。</summary>
    public static List<NetworkEntry> Build(
        IEnumerable<NetworkContainer> containers,
        IItemCatalog catalog,
        SortMethod method)
    {
        var totals = new Dictionary<int, long>();
        foreach (NetworkContainer container in containers)
        {
            foreach (SlotSnapshot slot in container.Slots)
            {
                if (slot.IsEmpty)
                {
                    continue;
                }

                int key = ItemValue.ReplaceLight(slot.Value, 0);
                totals[key] = totals.GetValueOrDefault(key) + slot.Count;
            }
        }

        var entries = new List<NetworkEntry>(totals.Count);
        foreach ((int value, long count) in totals)
        {
            entries.Add(new NetworkEntry(value, count));
        }

        entries.Sort(CreateComparer(catalog, method));
        return entries;
    }

    private static Comparison<NetworkEntry> CreateComparer(IItemCatalog catalog, SortMethod method) =>
        method switch
        {
            SortMethod.Name => (a, b) => Compare(catalog.GetDisplayName(a.Value), catalog.GetDisplayName(b.Value), a, b),
            SortMethod.CountDescending => (a, b) =>
            {
                int c = b.Count.CompareTo(a.Count);
                return c != 0 ? c : Fallback(a, b);
            },
            SortMethod.CountAscending => (a, b) =>
            {
                int c = a.Count.CompareTo(b.Count);
                return c != 0 ? c : Fallback(a, b);
            },
            SortMethod.RawValue => Fallback,
            _ => (a, b) =>
            {
                int c = string.Compare(catalog.GetCategory(a.Value), catalog.GetCategory(b.Value), StringComparison.CurrentCultureIgnoreCase);
                if (c != 0)
                {
                    return c;
                }

                c = catalog.GetDisplayOrder(a.Value).CompareTo(catalog.GetDisplayOrder(b.Value));
                return c != 0 ? c : Fallback(a, b);
            },
        };

    private static int Compare(string left, string right, NetworkEntry a, NetworkEntry b)
    {
        int c = string.Compare(left, right, StringComparison.CurrentCultureIgnoreCase);
        return c != 0 ? c : Fallback(a, b);
    }

    private static int Fallback(NetworkEntry a, NetworkEntry b)
    {
        int c = ItemValue.Contents(a.Value).CompareTo(ItemValue.Contents(b.Value));
        if (c != 0)
        {
            return c;
        }

        c = ItemValue.Data(a.Value).CompareTo(ItemValue.Data(b.Value));
        return c != 0 ? c : b.Count.CompareTo(a.Count);
    }
}
