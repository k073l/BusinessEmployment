using BusinessEmployment.BetterSafe;
using BusinessEmployment.Helpers;
using BusinessEmployment.Persistence;
using MelonLoader;
using UnityEngine;
using Utils = BusinessEmployment.Helpers.Utils;
#if MONO
using ScheduleOne.Employees;
using ScheduleOne.ItemFramework;
using ScheduleOne.ObjectScripts;
using ScheduleOne.Property;
#else
using Il2CppScheduleOne.Employees;
using Il2CppScheduleOne.ItemFramework;
using Il2CppScheduleOne.ObjectScripts;
using Il2CppScheduleOne.Property;
#endif


namespace BusinessEmployment.Behaviours;

public class LaunderBehaviour
{
    // Registry keyed by employee
    private static readonly Dictionary<Packager, LaunderBehaviour> _instances = new();

    private readonly Packager employee;
    private readonly Property property;
    private Business propertyAsBusiness;
    private ELaunderEmployeeState state = ELaunderEmployeeState.Idle;
    private PlaceableStorageEntity? currentSE;
    private LaunderingStation? station;

    public LaunderBehaviourSaveData SaveData;

    private LaunderBehaviour(Packager employee)
    {
        this.employee = employee;
        property = employee.AssignedProperty;
        if (Utils.Is<Business>(property, out var business))
        {
            if (business == null) Melon<BusinessEmployment>.Logger.Error("Property cannot be cast to business!");
            propertyAsBusiness = business;
        }
        else
            Melon<BusinessEmployment>.Logger.Error("Property cannot be cast to business!");

        var propertyCode = property.PropertyCode;
        var saved = LaunderBehaviourSave.Instance.SaveDatas.Where(s => s.PropertyCode == propertyCode);
        if (saved.Any())
        {
            SaveData = saved.First();
        }
        else
        {
            SaveData = new LaunderBehaviourSaveData { PropertyCode = propertyCode, MoneyLeftToLaunder = 0 };
            LaunderBehaviourSave.Instance.SaveDatas.Add(SaveData);
        }
    }

    private static LaunderBehaviour? GetOrCreate(Packager employee)
    {
        if (employee == null)
            return null;

        CleanupNullEmployees();

        if (_instances.TryGetValue(employee, out var behaviour))
            return behaviour;

        behaviour = new LaunderBehaviour(employee);
        _instances[employee] = behaviour;
        return behaviour;
    }

    public static void Tick(Packager employee)
    {
        var behaviour = GetOrCreate(employee);
        behaviour?.Tick();
    }

    private void Tick()
    {
        if (!IsValidState())
        {
            SetIdleAndWaitOutside();
            return;
        }

        // Allow active states to complete before checking work status
        if (state == ELaunderEmployeeState.Idle)
        {
            if (!employee.CanWork() || propertyAsBusiness.currentLaunderTotal >= propertyAsBusiness.LaunderCapacity)
            {
                SetIdleAndWaitOutside();
                return;
            }
        }

        ProcessCurrentState();
        if (Time.frameCount % 30 == 0)
            MelonDebug.Msg($"Cash: {SaveData.MoneyLeftToLaunder}, state: {state}");
    }

    private bool IsValidState()
    {
        return propertyAsBusiness != null && property != null && employee != null;
    }

    private void SetIdleAndWaitOutside()
    {
        state = ELaunderEmployeeState.Idle;
        employee.SubmitNoWorkReason("There's nothing for me to do right now.", "I need to have cash to launder or free laundering capacity.");
        employee.SetIdle(true);
        employee?.SetWaitOutside(true);
    }

    private void ProcessCurrentState()
    {
        switch (state)
        {
            case ELaunderEmployeeState.Idle:
                HandleIdleState();
                break;
            case ELaunderEmployeeState.RetrieveCash:
                HandleRetrieveCashState();
                break;
            case ELaunderEmployeeState.DepositCash:
                HandleDepositCashState();
                break;
            default:
                HandleIdleState();
                break;
        }
    }

    private void HandleIdleState()
    {
        if (SaveData.MoneyLeftToLaunder > 0 &&
            propertyAsBusiness.currentLaunderTotal < propertyAsBusiness.LaunderCapacity)
        {
            if (EnsureLaunderingStationExists())
            {
                employee.SetWaitOutside(false);
                MoveToLaunderingStation();
                return;
            }
        }

        var storages = GetAllStoragesWithCash().ToList();
        if (!storages.Any())
        {
            MelonDebug.Msg("No reachable storage with cash found");
            return;
        }

        foreach (var storage in storages)
        {
            var accessPoint = FindReachableAccessPoint(storage);
            if (accessPoint.HasValue)
            {
                employee.SetWaitOutside(false);
                currentSE = storage;
                employee.SetDestination(accessPoint.Value);
                state = ELaunderEmployeeState.RetrieveCash;
                return;
            }
        }
    }

    private Vector3? FindReachableAccessPoint(PlaceableStorageEntity storage)
    {
        if (storage?.AccessPoints == null)
        {
            MelonDebug.Error("Storage accesspoints is null");
            return null;
        }

        foreach (var accessPoint in storage.AccessPoints)
        {
            if (employee.Movement.CanGetTo(accessPoint.position))
                return accessPoint.position;
        }

        MelonDebug.Error("Wasn't able to find a good position to move to");
        return null;
    }

    private void HandleRetrieveCashState()
    {
        if (employee.Movement.IsMoving)
            return;

        if (currentSE == null)
        {
            state = ELaunderEmployeeState.Idle;
            return;
        }

        CollectCashFromStorage();

        if (!EnsureLaunderingStationExists())
        {
            state = ELaunderEmployeeState.Idle;
            return;
        }

        MoveToLaunderingStation();
    }

    private void CollectCashFromStorage()
    {
        if (currentSE?.StorageEntity?.ItemSlots == null)
        {
            MelonDebug.Error("Storage itemslots is null");
            return;
        }

        var itemSlots = currentSE.StorageEntity.ItemSlots;
        for (var i = 0; i < itemSlots.Count; i++)
        {
            if (Utils.Is<CashInstance>(itemSlots[i].ItemInstance, out var cashInstance))
            {
                currentSE.StorageEntity.SetSlotLocked(null, i, true, employee.NetworkObject, "Taking out items");
                if (cashInstance != null)
                    WithdrawForLaunder(propertyAsBusiness.LaunderCapacity, cashInstance);
            }

            currentSE.StorageEntity.SetSlotLocked(null, i, false, employee.NetworkObject, "Taking out items");

            if (SaveData.MoneyLeftToLaunder >= propertyAsBusiness.LaunderCapacity)
                break;
        }
    }

    private bool EnsureLaunderingStationExists()
    {
        if (station != null)
            return true;

        station = property.BuildableItems.AsEnumerable()
            .Select(bi => Utils.Is<LaunderingStation>(bi, out var r) ? r : null)
            .FirstOrDefault(r => r != null);

        if (station != null) return true;
        Melon<BusinessEmployment>.Logger.Error("Laundering station not found!");
        return false;
    }

    private void MoveToLaunderingStation()
    {
        if (station == null)
            return;

        var targetPosition = station.BoundingCollider != null
            ? station.BoundingCollider.bounds.ClosestPoint(employee.transform.position)
            : station.transform.position;

        employee.Movement.GetClosestReachablePoint(targetPosition, out var point);
        employee.SetDestination(point);
        currentSE = null;
        state = ELaunderEmployeeState.DepositCash;
    }

    private void HandleDepositCashState()
    {
        if (employee.Movement.IsMoving)
            return;

        employee.SetAnimationTrigger_Networked(null, "GrabItem");
        var availableCapacity = propertyAsBusiness.LaunderCapacity - propertyAsBusiness.currentLaunderTotal;
        if (availableCapacity > 0)
        {
            var toDeposit = Math.Min(SaveData.MoneyLeftToLaunder, availableCapacity);
            propertyAsBusiness.StartLaunderingOperation(toDeposit);
            SaveData.MoneyLeftToLaunder -= toDeposit;
        }

        state = ELaunderEmployeeState.Idle;
    }

    private void WithdrawForLaunder(float launderTarget, CashInstance cashInstance)
    {
        var shortfall = launderTarget - SaveData.MoneyLeftToLaunder;

        if (shortfall <= 0 || cashInstance.Balance <= 0)
            return;

        var toTake = Math.Min(shortfall, cashInstance.Balance);

        employee.SetAnimationTrigger_Networked(null, "GrabItem");
        cashInstance.ChangeBalance(-toTake);
        SaveData.MoneyLeftToLaunder += toTake;
    }

    public IEnumerable<PlaceableStorageEntity?> GetAllStoragesWithCash()
    {
        var storages = property.BuildableItems
            .AsEnumerable()
            .Select(bi => Utils.Is<PlaceableStorageEntity>(bi, out var r) ? r : null)
            .Where(r => r != null)
            .Where(pse => pse.StorageEntity != employee.GetHome().Storage)
            .Where(pse => pse.OutputSlots.AsEnumerable().Any(os => Utils.Is<CashInstance>(os.ItemInstance, out _)))
            .OrderBy(pse =>
                pse?.StorageEntity != null &&
                pse.StorageEntity.ItemSlots?.Count == SafeCreator.SLOT_COUNT &&
                pse.StorageEntity.transform.Find("Safe") != null
            ); // Safes last (false < true)

        return storages;
    }

    public static void Remove(Packager employee)
    {
        _instances.Remove(employee);
    }

    private static void CleanupNullEmployees()
    {
        var toRemove = _instances.Keys
            .Where(e => e == null)
            .ToList();

        foreach (var e in toRemove)
            _instances.Remove(e);
    }
}

public enum ELaunderEmployeeState
{
    Idle,
    RetrieveCash,
    DepositCash
}