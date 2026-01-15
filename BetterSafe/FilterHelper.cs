using System.Collections;
using System.Reflection;
using BusinessEmployment.Helpers;
using MelonLoader;
using S1API.Storages;
using UnityEngine;
#if MONO
using ScheduleOne.ObjectScripts;
using ScheduleOne.Property;
using S1Storage = ScheduleOne.Storage;
using S1ItemFramework = ScheduleOne.ItemFramework;
#else
using Il2CppScheduleOne.ObjectScripts;
using Il2CppScheduleOne.Property;
using S1Storage = Il2CppScheduleOne.Storage;
using S1ItemFramework = Il2CppScheduleOne.ItemFramework;
#endif

namespace BusinessEmployment.BetterSafe;

internal class FilterHelper
{
    private static ItemFilter_Cash filter_Cash;
    
    private static MelonLogger.Instance _logger = Melon<BusinessEmployment>.Logger;
    
    internal static IEnumerator WaitSearchAdd(float timeout)
    {
        filter_Cash ??= new ItemFilter_Cash();
        
        var startTime = Time.time;
        while (Time.time - startTime <= timeout)
        {
            // I hope 20 slots + "Safe" in children is a good enough heuristic
            var betterSafes = Property.Properties
                .AsEnumerable()
                .Select(p => p.BuildableItems)
                .Where(items => items != null)
                .Select(items => items.AsEnumerable())
                .Aggregate((a, b) => a.Concat(b))
                .Select(bi => Utils.Is<PlaceableStorageEntity>(bi, out var r)
                    ? r
                    : null)
                .Where(r => r != null)
                .Select(pse => pse.StorageEntity) // All storage entities placed in properties
                .Where(s => s.ItemSlots.Count == SafeCreator.SLOT_COUNT && s.transform.Find("Safe") != null);
            foreach (var betterSafe in betterSafes)
            {
                foreach (var slot in betterSafe.ItemSlots)
                {
                    if (slot.HardFilters.AsEnumerable().Any(itFi => itFi.GetType() == filter_Cash.GetType())) continue;
                    slot.AddFilter(filter_Cash);
                }
            }

            yield return new WaitForSecondsRealtime(0.5f);
        }
    }
}