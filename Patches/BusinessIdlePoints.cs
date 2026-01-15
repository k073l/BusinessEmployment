using HarmonyLib;
using MelonLoader;
using UnityEngine;
using Object = UnityEngine.Object;
#if MONO
using ScheduleOne.Property;
#else
using Il2CppScheduleOne.Property;
#endif

namespace BusinessEmployment.Patches;

[HarmonyPatch(typeof(Business))]
internal class BusinessIdlePoints
{
    [HarmonyPatch("Awake")]
    [HarmonyPostfix]
    private static void AddIdlePointAndEmployeeCapacity(Business __instance)
    {
        if (__instance == null)
        {
            Melon<BusinessEmployment>.Logger.Error("Business instance is null");
            return;
        }

        var spawnPoint = __instance.SpawnPoint;
        if (spawnPoint == null)
        {
            Melon<BusinessEmployment>.Logger.Error($"SpawnPoint for {__instance.PropertyName} is null");
            return;
        }
        var idlePoints = new GameObject("EmployeeIdlePoints");
        idlePoints.transform.SetParent(__instance.transform);
        var newIdlePoint = Object.Instantiate(spawnPoint, spawnPoint.position, spawnPoint.rotation);
        newIdlePoint.transform.SetParent(idlePoints.transform);
        var transformList = new List<Transform> { newIdlePoint };
        
        __instance.EmployeeIdlePoints = transformList.ToArray();
        __instance.EmployeeCapacity = 1;
    }
}