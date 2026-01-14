using BusinessEmployment.BetterSafe;
using BusinessEmployment.Persistence;
using MelonLoader;
using ScheduleOne.Employees;
using ScheduleOne.ItemFramework;
using ScheduleOne.Money;
using ScheduleOne.ObjectScripts;
using ScheduleOne.Property;
using UnityEngine;
using Utils = BusinessEmployment.Helpers.Utils;

namespace BusinessEmployment.Behaviours;

public class LaunderBehaviour
{
    // Registry keyed by employee
    private static readonly Dictionary<Packager, LaunderBehaviour> _instances
        = new Dictionary<Packager, LaunderBehaviour>();

    private static readonly Func<PlaceableStorageEntity?, bool> SafePredicate = pse =>
        pse?.StorageEntity != null &&
        pse.StorageEntity.ItemSlots?.Count == SafeCreator.SLOT_COUNT &&
        pse.StorageEntity.transform.Find("Safe") != null;
    
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

    private static LaunderBehaviour GetOrCreate(Packager employee)
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

    public static LaunderBehaviour FindByProperty(Property property)
    {
        return _instances.Values.FirstOrDefault(behaviour => behaviour.property == property);
    }

    public static void Tick(Packager employee)
    {
        var behaviour = GetOrCreate(employee);
        behaviour?.Tick();
    }

    private void Tick()
    {
        if (!IsValidState() || !employee.CanWork() || propertyAsBusiness.currentLaunderTotal >= propertyAsBusiness.LaunderCapacity)
        {
            SetIdleAndWaitOutside();
            return;
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
            return null;

        foreach (var accessPoint in storage.AccessPoints)
        {
            if (employee.Movement.CanGetTo(accessPoint.position))
                return accessPoint.position;
        }
        
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
            return;

        foreach (var itemSlot in currentSE.StorageEntity.ItemSlots)
        {
            if (Utils.Is<CashInstance>(itemSlot.ItemInstance, out var cashInstance))
            {
                if (cashInstance != null)
                    WithdrawForLaunder(propertyAsBusiness.LaunderCapacity, cashInstance);
            }

            if (SaveData.MoneyLeftToLaunder >= propertyAsBusiness.LaunderCapacity)
                break;
        }
    }

    private bool EnsureLaunderingStationExists()
    {
        if (station != null)
            return true;

        station = property.BuildableItems
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

    public IEnumerable<PlaceableStorageEntity?> GetAllStoragesWithCash(bool onlySafes = false)
    {
        var storages = property.BuildableItems
            .Select(bi => Utils.Is<PlaceableStorageEntity>(bi, out var r) ? r : null)
            .Where(r => r != null)
            .Where(pse => pse.StorageEntity != employee.GetHome().Storage)
            .Where(pse => pse.OutputSlots.Any(os => os.ItemInstance is CashInstance))
            .OrderBy(SafePredicate); // Safes last (false < true)

        return onlySafes ? storages.Where(SafePredicate) : storages;
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