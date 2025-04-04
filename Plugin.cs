using System.Threading.Tasks;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace FuriousButtplug;

[BepInPlugin("DryIcedMatcha.FuriousButtplug", "Garfield Kart Furious Buttplug", "1.0.0")]
public class Plugin : BaseUnityPlugin
{
    internal static new ManualLogSource Logger;
    private static BPManager buttplugManager;
    private Harmony harmony;

    private void Awake()
    {
        // Plugin startup logic
        Logger = base.Logger;
        Logger.LogInfo($"Plugin FuriousButtplug is loaded!");
        buttplugManager = new BPManager(Logger);

        harmony = new Harmony("com.dryicedmatcha.furiousbuttplug");
        harmony.PatchAll();

        Task.Run(async () =>
        {
            await buttplugManager.ConnectButtplug("localhost:12342");
            await buttplugManager.ScanForDevices();
        });
    }

    private void OnDestroy()
    {
        harmony.UnpatchSelf();
    }

    [HarmonyPatch(typeof(BonusEffectMgr))]
    [HarmonyPatch("ActivateBonusEffect")]
    public class BonusEffectMgrPatch
    {
        static void Postfix(BonusEffectMgr __instance, EBonusEffect _BonusEffect)
        {
            var targetKart = __instance.Target;
            if (targetKart != null && targetKart.Driver.IsLocal)
            {
                // Trigger your custom action here
                Logger.LogInfo($"Custom action triggered for effect: {_BonusEffect}");
                buttplugManager.VibrateDevicePulse(100);
            }
        }
    }
}