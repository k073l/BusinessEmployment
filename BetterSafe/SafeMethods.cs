using BusinessEmployment.Helpers;
using MelonLoader;
using ScheduleOne;
using ScheduleOne.ItemFramework;
using ScheduleOne.Money;
using ScheduleOne.ObjectScripts;
using ScheduleOne.Property;
using UnityEngine;

namespace BusinessEmployment.BetterSafe;

public class SafeMethods
{
    /// <summary>
    /// Runs at OnSleepStart, filling all golden safes in businesses with an employee
    /// </summary>
    public static void RefillSafe()
    {
        var betterSafes = Business.OwnedBusinesses
            .AsEnumerable()
            .Where(b => b.Employees.Count > 0)
            .SelectMany(p => p.BuildableItems)
            .Select(bi => Utils.Is<PlaceableStorageEntity>(bi, out var r) ? r : null)
            .Where(r => r != null)
            .Select(pse => pse.StorageEntity)
            .Where(s => s.ItemSlots.Count == SafeCreator.SLOT_COUNT && s.transform.Find("Safe") != null);

        var percentageCut = BusinessEmployment.EmpCut.Value;
        var playerCash = MoneyManager.Instance.cashBalance;
        var cashStackLimit = MoneyManager.Instance.cashInstance.StackLimit == 1
            ? 1000
            : MoneyManager.Instance.cashInstance.StackLimit;

        var totalInserted = 0f;
        var cutMultiplier = 1 + (percentageCut / 100f);

        foreach (var safe in betterSafes)
        {
            foreach (var slot in safe.ItemSlots)
            {
                var toInsert = 0f;
                if (Utils.Is<CashInstance>(slot.ItemInstance, out var cash))
                {
                    if (cash != null)
                    {
                        var needed = cashStackLimit - cash.Balance;
                        toInsert = needed;
                    }
                }
                else
                {
                    toInsert = cashStackLimit;
                }

                // Check if we can afford this insertion + its cut
                var projectedTotal = totalInserted + toInsert;
                var projectedCost = projectedTotal * cutMultiplier;

                if (projectedCost > playerCash)
                {
                    // Reduce insertion to what we can afford
                    var maxAffordable = playerCash / cutMultiplier;
                    toInsert = maxAffordable - totalInserted;
                }

                if (toInsert > 0)
                {
                    if (Utils.Is<CashInstance>(MoneyManager.Instance.cashInstance.GetCopy(), out var cashToInsert))
                    {
                        if (cashToInsert == null) continue;
                        cashToInsert.Balance = toInsert;
                        slot.InsertItem(cashToInsert);
                    }

                    totalInserted += toInsert;
                }

                if (totalInserted * cutMultiplier >= playerCash) break;
            }

            if (totalInserted * cutMultiplier >= playerCash) break;
        }

        var totalCut = totalInserted * (percentageCut / 100f);
        var totalCost = totalInserted + totalCut;
        MoneyManager.Instance.ChangeCashBalance(-totalCost);
        Melon<BusinessEmployment>.Logger.Msg(
            $"Refilled safes! Inserted: {totalInserted}, Employee cut: {totalCut}, Total cost: {totalCost}");
    }
}