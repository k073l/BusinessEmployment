using System.Reflection;
using BusinessEmployment.Helpers;
using HarmonyLib;
using MelonLoader;
using MelonLoader.Preferences;
using UnityEngine;
using Object = UnityEngine.Object;
#if MONO
using TMPro;
using ScheduleOne.Property;
using ScheduleOne.Money;

#else
using Il2CppTMPro;
using Il2CppScheduleOne.Property;
using Il2CppScheduleOne.Money;
#endif

namespace BusinessEmployment.Patches;

[HarmonyPatch(typeof(Business))]
internal class BusinessPostAwake
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

        AddBusinessToEntries(__instance);
    }

    private static void AddBusinessToEntries(Business business)
    {
        var entry = BusinessEmployment.CapacityCategory.CreateEntry($"{business.PropertyName}_Capacity",
            business.LaunderCapacity,
            $"Capacity of {business.PropertyName}",
            "Used for determining how much cash can be laundered at a time in this business.",
            validator: new ValueRange<float>(1f, 100_000f));
        entry.OnEntryValueChanged.Subscribe((oldValue, newValue) =>
        {
            if (Mathf.Approximately(oldValue, newValue)) return;
            ApplyAmount(business, newValue);
        });
        ApplyAmount(business, entry.Value);
        BusinessEmployment.BusinessCapacities.Add(entry);
        return;

        void ApplyAmount(Business b, float amount)
        {
            b.LaunderCapacity = amount;
            var containerObj = GetMemberValue(b, "Container");

            if (containerObj == null || !Utils.Is<Component>(containerObj, out var container) || container == null)
            {
                MelonLogger.Error("Container not found or not a Component");
                return;
            }

            var contentsGo = container.gameObject;
            var max = Utils.FindByPath(contentsGo, "LaunderingInterface/CurrentOperations/Total/Max");
            if (max == null) return;
            var maxText = max.GetComponent<TextMeshProUGUI>();
            if (maxText != null) maxText.text = MoneyManager.FormatAmount(amount);
        }
    }

    private static object? GetMemberValue(object? obj, string name)
    {
        if (obj == null) return null;

        var type = obj.GetType();

        // try field first
        var field = type.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (field != null)
            return field.GetValue(obj);

        // then property
        var prop = type.GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (prop != null)
            return prop.GetValue(obj);

        return null;
    }
}