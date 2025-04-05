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
            Kart targetKart = __instance.Target;
            if (targetKart != null && targetKart.Driver != null && targetKart.Driver.IsLocal && targetKart.Driver.IsHuman)
            {
                Logger.LogInfo($"Local HUMAN Player {targetKart.Driver.Id} hit by effect: {_BonusEffect}");

                switch (_BonusEffect)
                {
                    case EBonusEffect.BONUSEFFECT_BOOST:
                        buttplugManager.VibrateDevicePulse(100, 1800);
                        break;
                    case EBonusEffect.BONUSEFFECT_LEVITATE:
                        buttplugManager.VibrateDevicePulse(60, 3000);
                        break;
                    default:
                        buttplugManager.VibrateDevicePulse(60);
                        break;
                }
            }
        }
    }
    
    [HarmonyPatch(typeof(Kart))]
[HarmonyPatch("LaunchMiniBoost")]
public class Kart_DriftBoostPatch
{
    private static ManualLogSource Logger = BepInEx.Logging.Logger.CreateLogSource("DriftBoostMod");

    static void Postfix(Kart __instance)
    {
        // Check if this Kart belongs to the local human player
        if (__instance != null && __instance.Driver != null && __instance.Driver.IsLocal && __instance.Driver.IsHuman)
        {
            Kart.DRIFT_STATE currentDriftState = __instance.m_driftState;

            switch (currentDriftState)
            {
                case Kart.DRIFT_STATE.FIRST_THRESHOLD:
                    // Player executed the first level (blue sparks) mini-turbo
                    Logger.LogInfo($"Local Player {__instance.Driver.Id} executed a Level 1 Drift Boost!");
                    buttplugManager.VibrateDevicePulse(50, 600);
                    break;

                case Kart.DRIFT_STATE.SECOND_THRESHOLD:
                    // Player executed the second level (red/orange sparks) mini-turbo
                    Logger.LogInfo($"Local Player {__instance.Driver.Id} executed a Level 2 Drift Boost!");
                    buttplugManager.VibrateDevicePulse(50, 1800);
                    break;
                default:
                    break;
            }
        }
    }
}

    [HarmonyPatch(typeof(Driver))]
    [HarmonyPatch("OnCollisionEnter")]
    public class Driver_CollisionPatch
    {
        private static ManualLogSource Logger = BepInEx.Logging.Logger.CreateLogSource("CollisionMod");

        static void Postfix(Driver __instance, Collision collision)
        {
            Kart kart = __instance.Kart;

            if (kart != null && __instance.IsLocal && __instance.IsHuman)
            {
                GameObject collidedObject = collision.gameObject;
                int collidedLayer = collidedObject.layer;
                string layerName = LayerMask.LayerToName(collidedLayer);

                // Only proceed if the collision is with a wall or vehicle
                if (layerName == "ColWall" || layerName == "Vehicle")
                {
                    // Check collision force/velocity magnitude
                    float collisionForce = collision.relativeVelocity.magnitude;

                    // Apply threshold for significant collisions (now 4.0f)
                    if (collisionForce > 4.0f)
                    {
                        Logger.LogInfo($"Local Player {__instance.Id} collided with {collidedObject.name} (Layer: {layerName}) with force: {collisionForce}");

                        // Trigger vibration
                        int intensity = Mathf.Min(100, Mathf.RoundToInt(collisionForce * 10));
                        buttplugManager.VibrateDevicePulse(intensity, 300);
                    }
                }
            }
        }
    }
    
    
}