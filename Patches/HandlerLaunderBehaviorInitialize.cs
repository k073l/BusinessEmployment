using BusinessEmployment.Behaviours;
using HarmonyLib;
using ScheduleOne.Employees;
using ScheduleOne.Property;

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
        if (__instance.AssignedProperty is not Business) return;
        if (!__instance.CanWork()) return;
        if (__instance.Fired) return;
        if (__instance.PackagingBehaviour.Active || __instance.MoveItemBehaviour.Active) return;
        __instance.MarkIsWorking();
        LaunderBehaviour.Tick(__instance);
    }
}