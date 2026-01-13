using System.Collections;
using BusinessEmployment.BetterSafe;
using MelonLoader;
using BusinessEmployment.Helpers;
using S1API.Lifecycle;
using UnityEngine;
#if MONO
using FishNet;
#else
using Il2CppFishNet;
#endif

[assembly: MelonInfo(
    typeof(BusinessEmployment.BusinessEmployment),
    BusinessEmployment.BuildInfo.Name,
    BusinessEmployment.BuildInfo.Version,
    BusinessEmployment.BuildInfo.Author
)]
[assembly: MelonColor(1, 255, 0, 0)]
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
    public const string Description = "does stuff i guess";
    public const string Author = "me";
    public const string Version = "1.0.0";
}

public class BusinessEmployment : MelonMod
{
    private static MelonLogger.Instance _logger;

    public override void OnInitializeMelon()
    {
        _logger = LoggerInstance;
        _logger.Msg("BusinessEmployment initialized");
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
                break;
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