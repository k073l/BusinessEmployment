using BusinessEmployment.Helpers;
using MelonLoader;
#if MONO
using ScheduleOne.ItemFramework;
#else
using Il2CppScheduleOne.ItemFramework;
using Il2CppInterop.Runtime.Injection;
#endif

namespace BusinessEmployment.BetterSafe;

[RegisterTypeInIl2Cpp]
public class ItemFilter_Cash: ItemFilter
{
#if !MONO
    public ItemFilter_Cash(IntPtr ptr) : base(ptr)
    {
    }
    public ItemFilter_Cash() : base(ClassInjector.DerivedConstructorPointer<ItemFilter_Cash>())
    {
        ClassInjector.DerivedConstructorBody(this);
    }
#endif
    
    public override bool DoesItemMatchFilter(ItemInstance item)
    {
        return Utils.Is<CashInstance>(item, out _);
    }
}