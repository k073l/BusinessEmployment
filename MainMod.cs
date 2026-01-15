using System.Collections;
using BusinessEmployment.BetterSafe;
using MelonLoader;
using MelonLoader.Preferences;
using S1API.GameTime;
using S1API.Lifecycle;
using UnityEngine;

[assembly: MelonInfo(
    typeof(BusinessEmployment.BusinessEmployment),
    BusinessEmployment.BuildInfo.Name,
    BusinessEmployment.BuildInfo.Version,
    BusinessEmployment.BuildInfo.Author
)]
[assembly: MelonColor(1, 255, 195, 86)]
[assembly: MelonGame("TVGS", "Schedule I")]

// Specify platform domain based on build target (remove this if your mod supports both via S1API)
#if MONO
[assembly: MelonPlatformDomain(MelonPlatformDomainAttribute.CompatibleDomains.MONO)]
#else
[assembly: MelonPlatformDomain(MelonPlatformDomainAttribute.CompatibleDomains.IL2CPP)]
#endif

namespace BusinessEmployment;

public static class BuildInfo
{
    public const string Name = "BusinessEmployment";
    public const string Description = "Adds employees to Businesses. Automates laundering.";
    public const string Author = "k073l";
    public const string Version = "1.0.1";
}

public class BusinessEmployment : MelonMod
{
    private static MelonLogger.Instance _logger;
    private static MelonPreferences_Category _category;
    internal static MelonPreferences_Entry<float> EmpCut;
    internal static MelonPreferences_Entry<float> SafeCost;
    internal static MelonPreferences_Entry<bool> EnableSafeAutoRestock;

    public override void OnInitializeMelon()
    {
        _logger = LoggerInstance;
        _logger.Msg("BusinessEmployment initialized");

        _category = MelonPreferences.CreateCategory("BusinessEmployment", "Business Employment Settings");
        EmpCut = _category.CreateEntry("BusinessEmploymentEmployeeCut", 5f, "Employee Cut",
            "Additional payment for Business employees for restocking the Golden Safe. (% of the cash total)",
            validator: new ValueRange<float>(0, 100));
        SafeCost = _category.CreateEntry("BusinessEmploymentGoldSafeCost", 5000f, "Golden Safe price",
            "Price of the Golden Safe item in the Boutique",
            validator: new ValueRange<float>(0f, 1E+09f));
        EnableSafeAutoRestock = _category.CreateEntry("BusinessEmploymentEnableSafeAutoRestock", true,
            "Enable Golden Safe Auto-Restock",
            "If enabled, businesses with employees will automatically restock their Golden Safes when you sleep.");

        GameLifecycle.OnPreLoad += CreateSafe;
    }

    public override void OnSceneWasLoaded(int buildIndex, string sceneName)
    {
        switch (sceneName)
        {
            case "Menu":
                SafeCreator.SafeCreated = false;
                SafeCreator.SafeAdded = false;
                break;
            case "Main":
                MelonCoroutines.Start(AddDelayed());
                TimeManager.OnSleepStart += SafeMethods.RefillSafe;
                break;
        }
    }

    public override void OnSceneWasUnloaded(int buildIndex, string sceneName)
    {
        if (sceneName != "Main") return;
        try
        {
            TimeManager.OnSleepStart -= SafeMethods.RefillSafe;
        }
        catch (Exception ex)
        {
            // ignored
        }
    }

    private static void CreateSafe()
    {
        if (SafeCreator.SafeCreated) return;
        _logger.Msg("Creating safe");
        SafeCreator.CreateSafe();
    }

    private static IEnumerator AddDelayed()
    {
        yield return new WaitForSeconds(2f);
        if (SafeCreator.SafeAdded) yield break;
        _logger.Msg("Adding safe to the shop");
        SafeCreator.AddToShop();
    }
}