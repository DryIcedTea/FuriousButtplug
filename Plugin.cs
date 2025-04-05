using System.Threading.Tasks;
using BepInEx;
using BepInEx.Logging;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

namespace FuriousButtplug;

[BepInPlugin("DryIcedMatcha.FuriousButtplug", "Garfield Kart Furious Buttplug", "1.0.0")]
public class Plugin : BaseUnityPlugin
{
    internal static Plugin Instance { get; private set; }
    internal static new ManualLogSource Logger;
    private static BPManager buttplugManager;
    private Harmony harmony;
    
    // Configuration entries
    private ConfigEntry<string> configServerAddress;
    
    // Drift settings
    private ConfigEntry<int> configDriftBaseIntensity;
    private ConfigEntry<int> configDriftLevel1Intensity;
    private ConfigEntry<int> configDriftLevel2Intensity;
    
    // Boost settings
    private ConfigEntry<int> configBoostIntensity;
    private ConfigEntry<int> configMiniBoostLevel1Intensity;
    private ConfigEntry<int> configMiniBoostLevel2Intensity;
    
    // Collision settings
    private ConfigEntry<float> configCollisionForceMultiplier;
    
    // Lap settings
    private ConfigEntry<int> configLapCompleteIntensity;
    private ConfigEntry<int> configRaceFinishIntensity;
    
    //Player hit with effect settings
    private ConfigEntry<int> configPlayerHitEffectIntensity;
    
    private ConfigEntry<int> configPlayerUFOEffectIntensity;
    
    //Vibration Toggles
    private ConfigEntry<bool> configEnableDriftChargeVibration;
    private ConfigEntry<bool> configEnableDriftBoostVibration;
    private ConfigEntry<bool> configEnableCollisionVibration;
    private ConfigEntry<bool> configEnableLapCompleteVibration;
    private ConfigEntry<bool> configEnableRaceFinishVibration;
    private ConfigEntry<bool> configEnablePlayerHitVibration;
    private ConfigEntry<bool> configEnableUFOVibration;
    private ConfigEntry<bool> configEnableBoostPadVibration;
    

    private void Awake()
    {
        // Plugin startup logic
        Logger = base.Logger;
        Instance = this;
        Logger.LogInfo($"Plugin FuriousButtplug is loaded!");
        
        InitializeConfig();
        
        buttplugManager = new BPManager(Logger);

        harmony = new Harmony("com.dryicedmatcha.furiousbuttplug");
        harmony.PatchAll();

        Task.Run(async () =>
        {
            await buttplugManager.ConnectButtplug(configServerAddress.Value);
            await buttplugManager.ScanForDevices();
        });
    }
    
private void InitializeConfig()
{
    // Connection settings
    configServerAddress = Config.Bind("Connection",
        "ServerAddress",
        "localhost:12345",
        "Address of the Intiface/Buttplug server");

    // Drift settings
    configDriftBaseIntensity = Config.Bind("Drift",
        "BaseIntensity",
        10,
        "Base vibration intensity when starting to drift (0-100)");

    configDriftLevel1Intensity = Config.Bind("Drift",
        "Level1Intensity",
        20,
        "Vibration intensity at drift level 1 (blue sparks) (0-100)");

    configDriftLevel2Intensity = Config.Bind("Drift",
        "Level2Intensity",
        30,
        "Vibration intensity at drift level 2 (red sparks) (0-100)");

    // Boost settings
    configBoostIntensity = Config.Bind("Boost",
        "Intensity",
        100,
        "Vibration intensity when hitting a boost pad/item (0-100)");

    configMiniBoostLevel1Intensity = Config.Bind("MiniBoost",
        "Level1Intensity",
        50,
        "Vibration intensity for level 1 drift mini-boost (0-100)");

    configMiniBoostLevel2Intensity = Config.Bind("MiniBoost",
        "Level2Intensity",
        50,
        "Vibration intensity for level 2 drift mini-boost (0-100)");

    // Collision settings
    configCollisionForceMultiplier = Config.Bind("Collision",
        "ForceMultiplier",
        10.0f,
        "Multiplier for collision force to determine vibration intensity when colliding with cars/walls");

    // Lap settings
    configLapCompleteIntensity = Config.Bind("Lap",
        "CompleteIntensity",
        60,
        "Vibration intensity when completing a lap (0-100)");

    configRaceFinishIntensity = Config.Bind("Lap",
        "RaceFinishIntensity",
        60,
        "Vibration intensity when finishing the race (0-100)");

    // Effect settings
    configPlayerHitEffectIntensity = Config.Bind("Effects",
        "HitEffectIntensity",
        60,
        "Vibration intensity when player is hit by an effect/item (0-100)");

    configPlayerUFOEffectIntensity = Config.Bind("Effects",
        "UFOEffectIntensity",
        60,
        "Vibration intensity for UFO/Levitate effect (0-100)");
    
    // Vibration toggles
    configEnableDriftChargeVibration = Config.Bind("Vibration Toggles",
        "EnableDriftChargeVibration",
        true,
        "Enable vibration during drift charging");

    configEnableDriftBoostVibration = Config.Bind("Vibration Toggles",
        "EnableDriftBoostVibration",
        true,
        "Enable vibration for drift boosts/miniturbo");

    configEnableCollisionVibration = Config.Bind("Vibration Toggles",
        "EnableCollisionVibration",
        true,
        "Enable vibration when colliding with objects or vehicles");

    configEnableLapCompleteVibration = Config.Bind("Vibration Toggles", 
        "EnableLapCompleteVibration",
        true,
        "Enable vibration when completing a lap");

    configEnableRaceFinishVibration = Config.Bind("Vibration Toggles",
        "EnableRaceFinishVibration",
        true,
        "Enable vibration when finishing the race");

    configEnablePlayerHitVibration = Config.Bind("Vibration Toggles",
        "EnablePlayerHitVibration",
        true,
        "Enable vibration when player is hit by items/effects");

    configEnableUFOVibration = Config.Bind("Vibration Toggles",
        "EnableUFOVibration",
        true,
        "Enable vibration for UFO/levitate effect");

    configEnableBoostPadVibration = Config.Bind("Vibration Toggles",
        "EnableBoostPadVibration",
        true,
        "Enable vibration for boost pads/items");
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
                        if (Plugin.Instance.configEnableBoostPadVibration.Value)
                        {
                            buttplugManager.VibrateDevicePulse(Plugin.Instance.configBoostIntensity.Value, 1800);
                        }
                        break;
                    case EBonusEffect.BONUSEFFECT_LEVITATE:
                        if (Plugin.Instance.configEnableUFOVibration.Value)
                        {
                            buttplugManager.VibrateDevicePulse(Plugin.Instance.configPlayerUFOEffectIntensity.Value, 3000);
                        }
                        break;
                    default:
                        if (Plugin.Instance.configEnablePlayerHitVibration.Value)
                        {
                            buttplugManager.VibrateDevicePulse(Plugin.Instance.configPlayerHitEffectIntensity.Value);
                        }
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
    public static bool isBoostActive = false;
    public static long boostEndTime = 0;

    static void Postfix(Kart __instance)
    {
        // Check if this Kart belongs to player
        if (__instance != null && __instance.Driver != null && __instance.Driver.IsLocal && __instance.Driver.IsHuman)
        {
            Kart.DRIFT_STATE currentDriftState = __instance.m_driftState;

            Logger.LogInfo($"Local Player {__instance.Driver.Id} STOPPED drifting via BOOST.");
            Kart_StartDriftPatch.isDrifting = false; // Reset drift flag

            // Only trigger vibrations if the drift boost vibration toggle is enabled
            if (Plugin.Instance.configEnableDriftBoostVibration.Value)
            {
                switch (currentDriftState)
                {
                    case Kart.DRIFT_STATE.FIRST_THRESHOLD:
                        // Player executed the first level (blue sparks) mini-turbo
                        Logger.LogInfo($"Local Player {__instance.Driver.Id} executed a Level 1 Drift Boost!");
                        buttplugManager.VibrateDevicePulse(Plugin.Instance.configMiniBoostLevel1Intensity.Value, 600);
                        isBoostActive = true;
                        break;

                    case Kart.DRIFT_STATE.SECOND_THRESHOLD:
                        // Player executed the second level (red/orange sparks) mini-turbo
                        Logger.LogInfo($"Local Player {__instance.Driver.Id} executed a Level 2 Drift Boost!");
                        buttplugManager.VibrateDevicePulse(Plugin.Instance.configMiniBoostLevel2Intensity.Value, 1800);
                        isBoostActive = true;
                        break;
                    default:
                        break;
                }
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
                if (!Plugin.Instance.configEnableCollisionVibration.Value)
                    return;
                
                GameObject collidedObject = collision.gameObject;
                int collidedLayer = collidedObject.layer;
                string layerName = LayerMask.LayerToName(collidedLayer);

                // Only proceed if the collision is with a wall or vehicle
                if (layerName == "ColWall" || layerName == "Vehicle")
                {
                    float collisionForce = collision.relativeVelocity.magnitude;
                    
                    if (collisionForce > 4.0f)
                    {
                        Logger.LogInfo($"Local Player {__instance.Id} collided with {collidedObject.name} (Layer: {layerName}) with force: {collisionForce}");
                        
                        int intensity = Mathf.Min(100, Mathf.RoundToInt(collisionForce * Plugin.Instance.configCollisionForceMultiplier.Value));
                        buttplugManager.VibrateDevicePulse(intensity, 300);
                    }
                }
            }
        }
    }
    
    

    [HarmonyPatch(typeof(Kart))]
    [HarmonyPatch("SetDriftState")] 
    public class Kart_StartDriftPatch
    {
        private static ManualLogSource Logger = BepInEx.Logging.Logger.CreateLogSource("DriftMod");
        public static bool isDrifting = false; // Track if we are currently drifting

        static void Prefix(Kart __instance, Kart.DRIFT_STATE state)
        {
            // Check if player
            if (__instance != null && __instance.Driver != null && __instance.Driver.IsLocal && __instance.Driver.IsHuman)
            {
                // Check if the *current* state is NONE and the *new* state is NOT NONE
                if (__instance.m_driftState == Kart.DRIFT_STATE.NONE && state != Kart.DRIFT_STATE.NONE)
                {
                    if (!isDrifting)
                    {
                        isDrifting = true;
                        Logger.LogInfo($"Local Player {__instance.Driver.Id} STARTED drifting. New state: {state}");


                        buttplugManager.VibrateDevice(Plugin.Instance.configDriftBaseIntensity.Value);
                    }
                }
                
                else if (__instance.m_driftState != Kart.DRIFT_STATE.NONE && state == Kart.DRIFT_STATE.NONE)
                {
                    Logger.LogInfo($"Local Player {__instance.Driver.Id} STOPPED drifting (SetDriftState to NONE)");
                    isDrifting = false; 
                    if (!Kart_DriftBoostPatch.isBoostActive)
                    {
                        buttplugManager.VibrateDevice(0); 
                    }
                    Kart_DriftBoostPatch.isBoostActive = false; 
                    
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
                    //buttplugManager.VibrateDevice(0); 
                }
            }
        }
    }
    
[HarmonyPatch(typeof(Kart))]
[HarmonyPatch("SetDriftState")]
public class Kart_DriftStateChangePatch
{
    private static ManualLogSource Logger = BepInEx.Logging.Logger.CreateLogSource("DriftMod");

    static void Prefix(Kart __instance, Kart.DRIFT_STATE state)
    {
        if (__instance != null && __instance.Driver != null && __instance.Driver.IsLocal && __instance.Driver.IsHuman)
        {
            
            if (!Plugin.Instance.configEnableDriftChargeVibration.Value)
                return;

            Kart.DRIFT_STATE currentState = __instance.m_driftState;
            
            if (currentState != state)
            {
                switch (state) 
                {
                    case Kart.DRIFT_STATE.NONE:
                        // Drift ended (or wasn't started), stop vibration
                        Logger.LogInfo($"Local Player {__instance.Driver.Id} Drift State changed to NONE. Stopping vibration.");
                        //buttplugManager.VibrateDevice(0); // Stop vibration
                        break;

                    case Kart.DRIFT_STATE.NO_BOOST:
                        // Just started drifting, or fell back from level 1
                        Logger.LogInfo($"Local Player {__instance.Driver.Id} Drift State changed to NO_BOOST. Setting vibration intensity 1.");
                        buttplugManager.VibrateDevice(Plugin.Instance.configDriftBaseIntensity.Value); // Start vibration
                        break;

                    case Kart.DRIFT_STATE.FIRST_THRESHOLD:
                        // Reached level 1 (blue sparks)
                        Logger.LogInfo($"Local Player {__instance.Driver.Id} Drift State changed to FIRST_THRESHOLD. Setting vibration intensity 2.");
                        buttplugManager.VibrateDevice(Plugin.Instance.configDriftLevel1Intensity.Value);
                        break;

                    case Kart.DRIFT_STATE.SECOND_THRESHOLD:
                        // Reached level 2 (red/orange sparks)
                        Logger.LogInfo($"Local Player {__instance.Driver.Id} Drift State changed to SECOND_THRESHOLD. Setting vibration intensity 3.");
                        buttplugManager.VibrateDevice(Plugin.Instance.configDriftLevel2Intensity.Value);
                        break;
                }
            }
        }
    }
}

[HarmonyPatch(typeof(RcVehicleRaceStats))]
[HarmonyPatch("CrossStartLine")]
public class RcVehicleRaceStats_LapFinishPatch
{
    private static ManualLogSource Logger = BepInEx.Logging.Logger.CreateLogSource("LapFinishMod");

    static void Postfix(RcVehicleRaceStats __instance, int _gameTime, bool _bReverse)
    {
        Kart kart = __instance.GetVehicle() as Kart;

        if (kart == null || kart.Driver == null || _bReverse)
        {
            return;
        }

        if (kart.Driver.IsLocal && kart.Driver.IsHuman)
        {
            int currentLap = __instance.GetNbLapCompleted();
            int totalLaps = __instance.GetRaceNbLap();

            if (__instance.IsRaceEnded())
            {
                Logger.LogInfo($"Local Player {kart.Driver.Id} FINISHED THE RACE!");
                if (Plugin.Instance.configEnableRaceFinishVibration.Value)
                {
                    buttplugManager.VibrateDevicePulse(Plugin.Instance.configRaceFinishIntensity.Value, 2000);
                }
                return;
            }

            //Check for Regular Lap Change
            if (currentLap > 0)
            {
                Logger.LogInfo($"Local Player {kart.Driver.Id} started NEW LAP ({currentLap}/{totalLaps})!");
                if (Plugin.Instance.configEnableLapCompleteVibration.Value)
                {
                    buttplugManager.VibrateDevicePulse(Plugin.Instance.configLapCompleteIntensity.Value, 700);
                }
            }
        }
    }
}

    
}