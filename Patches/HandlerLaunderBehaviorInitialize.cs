using BusinessEmployment.Behaviours;
using BusinessEmployment.Helpers;
using HarmonyLib;
#if MONO
using ScheduleOne.Employees;
using ScheduleOne.Property;
#else
using Il2CppScheduleOne.Employees;
using Il2CppScheduleOne.Property;
#endif

namespace BusinessEmployment.Patches;

[HarmonyPatch(typeof(Packager))]
public class HandlerLaunderBehaviorInitialize
{
    [HarmonyPatch("UpdateBehaviour")]
    [HarmonyPostfix]
    private static void AddLaunderBehaviour(Packager __instance)
    {
        if (__instance == null) return;
        if (__instance.AssignedProperty == null) return;
        if (!Utils.Is<Business>(__instance.AssignedProperty, out _)) return;
        if (!__instance.CanWork()) return;
        if (__instance.Fired) return;
        if (__instance.PackagingBehaviour.Active || __instance.MoveItemBehaviour.Active) return;
        __instance.MarkIsWorking();
        LaunderBehaviour.Tick(__instance);
    }
}