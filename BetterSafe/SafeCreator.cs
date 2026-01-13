using System.Collections;
using BusinessEmployment.Helpers;
using MelonLoader;
using S1API.Building;
using S1API.Items;
using S1API.Rendering;
using S1API.Shops;
using S1API.Storage;
using UnityEngine;
using S1Storage = ScheduleOne.Storage;

namespace BusinessEmployment.BetterSafe;

public class SafeCreator
{
    private const string SAFE_ID = "gold_safe";
    private const string SAFE_NAME = "Golden Safe";
    private const float SAFE_PRICE = 1000f;
    internal const int SLOT_COUNT = 20;

    public static bool SafeCreated;
    public static bool SafeAdded;
    
    private static readonly Color GoldMetalColor = new Color(
        1.000f, 0.766f, 0.336f, 1f
    );

    public static void CreateSafe()
    {
        var safeItem = BuildableItemCreator.CloneFrom("safe")
            .WithBasicInfo(SAFE_ID, SAFE_NAME, "An upgraded version of the safe. Can hold only cash.")
            .WithPricing(SAFE_PRICE)
            .Build();
        // Wire up events
        BuildEvents.OnBuildableItemInitialized += ItemBuilt;
        BuildEvents.OnGridItemCreated += ItemBuilt;
        BuildEvents.OnSurfaceItemCreated += ItemBuilt;
        StorageEvents.OnStorageCreated += StorageCreated;
        StorageEvents.OnStorageLoading += StorageLoaded;
        StorageEvents.OnStorageOpening += StorageOpened;
        SafeCreated = true;
    }

    public static void AddToShop()
    {
        var safe = ItemManager.GetItemDefinition(SAFE_ID);
        if (safe == null)
        {
            Melon<BusinessEmployment>.Logger.Error("Error while adding safe to shop");
            return;
        }

        var shop = ShopManager.GetShopByName("Bleuball's Boutique");
        if (shop == null)
        {
            Melon<BusinessEmployment>.Logger.Error("Shop not found");
        }

        shop.AddItem(safe);
        SafeAdded = true;
    }

    private static void EnsureCapacity(StorageEntity storage)
    {
        if (storage == null) return;
        if (!storage.SetSlotCount(SLOT_COUNT))
        {
            Melon<BusinessEmployment>.Logger.Error("Failed to add additional slots");
            return;
        }

        // Wasteful to 
        MelonCoroutines.Start(FilterHelper.WaitSearchAdd(10f));
    }

    private static void StorageCreated(StorageEventArgs args)
    {
        if (args is not { Storage: not null, ItemId: SAFE_ID }) return;
        args.Storage.Name = SAFE_NAME;
        EnsureCapacity(args.Storage);
    }

    private static void StorageOpened(StorageEventArgs args)
    {
        args?.Storage?.SyncCustomNameToDisplayName();
    }

    private static void StorageLoaded(StorageLoadingEventArgs args)
    {
        if (args is not { Storage: not null, ItemId: SAFE_ID }) return;
        args.Storage.Name = SAFE_NAME;
        EnsureCapacity(args.Storage);
    }

    private static void ItemBuilt(BuildEventArgs args)
    {
        if (args != null && SafeCreated)
        {
            MaterialHelper.ReplaceMaterials(
                args.GameObject,
                mat => mat.name.ToLower().Contains("safe_body"),
                material =>
                {
                    // CreateMetallicVariant doesn't work for me smh my head
                    MaterialHelper.RemoveAllTextures(material);
                    MaterialHelper.SetColor(material, "_BaseColor", GoldMetalColor);
                    MaterialHelper.SetColor(material, "_Color", GoldMetalColor);
                    MaterialHelper.SetFloat(material, "_Metallic", 0.85f);
                    MaterialHelper.SetFloat(material, "_Smoothness", 0.65f);
                    MaterialHelper.SetFloat(material, "_Glossiness", 0.65f);
                });
        }
    }
}
