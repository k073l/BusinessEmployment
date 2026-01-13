using ScheduleOne.ItemFramework;

namespace BusinessEmployment.BetterSafe;

public class ItemFilter_Cash: ItemFilter
{
    public override bool DoesItemMatchFilter(ItemInstance item)
    {
        return item is CashInstance;
    }
}