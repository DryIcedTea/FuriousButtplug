using Buttplug.Client;
using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using BepInEx.Logging;
using UnityEngine;

namespace FuriousButtplug
{
    internal class BPManager
    {
        private ButtplugClient client = new ButtplugClient("FuriousButtplug");
        private string _intifaceIP;
        private ManualLogSource _logger;

        private CancellationTokenSource _currentVibrationCts;
        private readonly object _vibrationLock = new object();
        private DateTime _lastVibrationTime;

        public BPManager(ManualLogSource logger)
        {
            _logger = logger;
        }

        private void LogInnerExceptions(Exception ex)
        {
            while (ex != null)
            {
                _logger.LogError($"Inner Exception: {ex.Message}");
                ex = ex.InnerException;
            }
        }
        
        public async Task ScanForDevices()
        {
            if (!client.Connected)
            {
                _logger.LogInfo("Buttplug not connected, cannot scan for devices");
                return;
            }
            _logger.LogInfo("Scanning for devices");
            await client.StartScanningAsync();
            await Task.Delay(30000);
            _logger.LogInfo("Stopping scanning for devices");
            await client.StopScanningAsync();
        }

        public async Task ConnectButtplug(string meIntifaceIP)
        {
            _intifaceIP = meIntifaceIP;
            if (client.Connected)
            {
                _logger.LogInfo("Buttplug already connected, skipping");
                return;
            }
            _logger.LogInfo("Buttplug Client Connecting");
            _logger.LogInfo($"Connecting to {_intifaceIP}");
            client.Dispose();
            client = new ButtplugClient("FuriousButtplug");

            try
            {
                await client.ConnectAsync(new ButtplugWebsocketConnector(new Uri($"ws://{_intifaceIP}")));
                _logger.LogInfo("Connection successful");
            }
            catch (TypeInitializationException ex) when (ex.TypeName == "Newtonsoft.Json.JsonWriter")
            {
                _logger.LogError($"JsonWriter initialization failed: {ex.Message}");
                LogInnerExceptions(ex.InnerException);
                return;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Connection failed: {ex.Message}");
                return;
            }

            _logger.LogInfo($"{client.Devices.Count()} devices found on startup.");
            foreach (var device in client.Devices)
            {
                _logger.LogInfo($"- {device.Name} ({device.DisplayName} : {device.Index})");
            }
            client.DeviceAdded += HandleDeviceAdded;
            client.DeviceRemoved += HandleDeviceRemoved;
            client.ServerDisconnect += (object o, EventArgs e) => _logger.LogInfo("Intiface Server disconnected.");
            _logger.LogInfo("Buttplug Client Connected");
        }

        public async Task DisconnectButtplug()
        {
            if (!client.Connected)
            {
                _logger.LogInfo("Buttplug not connected, skipping");
                return;
            }

            _logger.LogInfo("Disconnecting Buttplug Client");
            await client.DisconnectAsync();
        }

        private void HandleDeviceAdded(object sender, DeviceAddedEventArgs e)
        {
            _logger.LogInfo($"Buttplug Device {e.Device.Name} ({e.Device.DisplayName} : {e.Device.Index}) Added");
        }

        private void HandleDeviceRemoved(object sender, DeviceRemovedEventArgs e)
        {
            _logger.LogInfo($"Buttplug Device {e.Device.Name} ({e.Device.DisplayName} : {e.Device.Index}) Removed");
        }

        private bool HasVibrators()
        {
            if (!client.Connected)
            {
                return false;
            }
            else if (client.Devices.Count() == 0)
            {
                _logger.LogInfo("Either buttplug is not connected or no devices are available");
                return false;
            }
            else if (!client.Devices.Any(device => device.VibrateAttributes.Count > 0))
            {
                _logger.LogInfo("No connected devices have vibrators available.");
                return false;
            }
            return true;
        }

        public async Task VibrateDevice(float level)
        {
            if (!HasVibrators())
            {
                return;
            }
            float intensity = Mathf.Clamp(level, 0f, 100f) / 100f;
            foreach (var device in client.Devices)
            {
                if (device.VibrateAttributes.Count > 0)
                {
                    _logger.LogInfo($"Vibrating at {intensity}");
                    await device.VibrateAsync(intensity);
                }
                else
                {
                    _logger.LogInfo($"No vibrators on device {device.Name}");
                }
            }
        }

        public async Task VibrateDevicePulse(float level)
        {
            await VibrateDevicePulse(level, 400);
        }

        public async Task VibrateDevicePulse(float level, int duration)
        {
            if (!HasVibrators())
            {
                return;
            }

            lock (_vibrationLock)
            {
                _currentVibrationCts?.Cancel();
                _currentVibrationCts = new CancellationTokenSource();
            }

            var token = _currentVibrationCts.Token;
            float intensity = Mathf.Clamp(level, 0f, 100f);
            _logger.LogInfo($"VibrateDevicePulse {intensity}");

            try
            {
                await VibrateDevice(intensity);
                _lastVibrationTime = DateTime.Now;

                try
                {
                    await Task.Delay(duration, token);
                }
                catch (TaskCanceledException)
                {
                    return;
                }

                if (!token.IsCancellationRequested)
                {
                    await VibrateDevice(0);
                }
            }
            catch (Exception ex)
            {
                _logger.LogInfo($"Error during vibration: {ex.Message}");
                await VibrateDevice(0);
            }
        }

        private async Task VibrateDeviceWithDuration(float intensity, int duration)
        {
            await VibrateDevice(intensity);
            await Task.Delay(duration);
        }

        public async Task StopDevices()
        {
            await VibrateDevice(0);
        }
    }
}