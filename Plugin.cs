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
    public static bool isBoostActive = false; // Track if we are currently boosting
    public static long boostEndTime = 0;

    static void Postfix(Kart __instance)
    {
        // Check if this Kart belongs to the local human player
        if (__instance != null && __instance.Driver != null && __instance.Driver.IsLocal && __instance.Driver.IsHuman)
        {
            Kart.DRIFT_STATE currentDriftState = __instance.m_driftState;
            
            Logger.LogInfo($"Local Player {__instance.Driver.Id} STOPPED drifting via BOOST.");
            Kart_StartDriftPatch.isDrifting = false; // Reset drift flag

            switch (currentDriftState)
            {
                case Kart.DRIFT_STATE.FIRST_THRESHOLD:
                    // Player executed the first level (blue sparks) mini-turbo
                    Logger.LogInfo($"Local Player {__instance.Driver.Id} executed a Level 1 Drift Boost!");
                    buttplugManager.VibrateDevicePulse(50, 600);
                    isBoostActive = true;
                    break;

                case Kart.DRIFT_STATE.SECOND_THRESHOLD:
                    // Player executed the second level (red/orange sparks) mini-turbo
                    Logger.LogInfo($"Local Player {__instance.Driver.Id} executed a Level 2 Drift Boost!");
                    buttplugManager.VibrateDevicePulse(50, 1800);
                    isBoostActive = true;
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
    
    
    /// <summary>
    /// Executes when the player starts drifting!
    /// </summary>
    [HarmonyPatch(typeof(Kart))]
    [HarmonyPatch("SetDriftState")] // Method that updates the internal drift state enum
    public class Kart_StartDriftPatch
    {
        private static ManualLogSource Logger = BepInEx.Logging.Logger.CreateLogSource("DriftMod");
        public static bool isDrifting = false; // Track if we are currently drifting

        static void Prefix(Kart __instance, Kart.DRIFT_STATE state)
        {
            // Check if it's the local human player
            if (__instance != null && __instance.Driver != null && __instance.Driver.IsLocal && __instance.Driver.IsHuman)
            {
                // Check if the *current* state is NONE and the *new* state is NOT NONE
                if (__instance.m_driftState == Kart.DRIFT_STATE.NONE && state != Kart.DRIFT_STATE.NONE)
                {
                    if (!isDrifting) // Prevent firing multiple times if state rapidly changes > NONE
                    {
                        isDrifting = true;
                        Logger.LogInfo($"Local Player {__instance.Driver.Id} STARTED drifting. New state: {state}");

                        // --- Trigger START drift vibration here ---
                        buttplugManager.VibrateDevice(20);
                    }
                }
                // Handle the case where the state goes back to NONE (drift ended)
                else if (__instance.m_driftState != Kart.DRIFT_STATE.NONE && state == Kart.DRIFT_STATE.NONE)
                {
                    Logger.LogInfo($"Local Player {__instance.Driver.Id} STOPPED drifting (SetDriftState to NONE)");
                    isDrifting = false; // Reset drift flag when stopping
                    if (!Kart_DriftBoostPatch.isBoostActive)
                    {
                        buttplugManager.VibrateDevice(0); // Stop vibration
                    }
                    Kart_DriftBoostPatch.isBoostActive = false; // Reset boost flag
                    
                }
            }
        }

         // Helper Postfix just to reset the flag if something unexpected happens
        static void Postfix(Kart __instance, Kart.DRIFT_STATE state)
        {
            if (__instance != null && __instance.Driver != null && __instance.Driver.IsLocal && __instance.Driver.IsHuman)
            {
                if (state == Kart.DRIFT_STATE.NONE)
                {
                    isDrifting = false;
                    //buttplugManager.VibrateDevice(0); // Stop vibration
                }
            }
        }
    }
    
    
    //Executes when the player changes their drift state
    [HarmonyPatch(typeof(Kart))]
    [HarmonyPatch("SetDriftState")]
public class Kart_DriftStateChangePatch // Renamed to reflect its new purpose
{
    private static ManualLogSource Logger = BepInEx.Logging.Logger.CreateLogSource("DriftMod");
    
    static void Prefix(Kart __instance, Kart.DRIFT_STATE state)
    {
        if (__instance != null && __instance.Driver != null && __instance.Driver.IsLocal && __instance.Driver.IsHuman)
        {
            Kart.DRIFT_STATE currentState = __instance.m_driftState;

            // Only act if the state is actually changing
            if (currentState != state)
            {
                switch (state) // Check the NEW state
                {
                    case Kart.DRIFT_STATE.NONE:
                        // Drift ended (or wasn't started), stop vibration
                        Logger.LogInfo($"Local Player {__instance.Driver.Id} Drift State changed to NONE. Stopping vibration.");
                        // --- Stop continuous drift vibration ---
                        //buttplugManager.VibrateDevice(0); // Stop vibration
                        break;

                    case Kart.DRIFT_STATE.NO_BOOST:
                        // Just started drifting, or fell back from level 1
                        Logger.LogInfo($"Local Player {__instance.Driver.Id} Drift State changed to NO_BOOST. Setting vibration intensity 1.");
                        buttplugManager.VibrateDevice(10); // Start vibration
                        break;

                    case Kart.DRIFT_STATE.FIRST_THRESHOLD:
                        // Reached level 1 (blue sparks)
                        Logger.LogInfo($"Local Player {__instance.Driver.Id} Drift State changed to FIRST_THRESHOLD. Setting vibration intensity 2.");
                        buttplugManager.VibrateDevice(20);
                        break;

                    case Kart.DRIFT_STATE.SECOND_THRESHOLD:
                        // Reached level 2 (red/orange sparks)
                        Logger.LogInfo($"Local Player {__instance.Driver.Id} Drift State changed to SECOND_THRESHOLD. Setting vibration intensity 3.");
                        buttplugManager.VibrateDevice(30);
                        break;
                }
            }
        }
    }
}
    
    
}