using System;
using System.Collections;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Mnemosyne.Networking
{
    /// <summary>
    /// Announces the client to a host dashboard and keeps the host informed about active scenes.
    /// </summary>
    public sealed class UdpClientReporter : MonoBehaviour
    {
        [Header("Host")]
        [SerializeField] private string hostAddress = "auto";
        [SerializeField] private int hostPort = 4949;
        [SerializeField] private string sharedSecret = string.Empty;
        [Tooltip("Port used by the client listener to receive gameplay commands (e.g. 3939).")]
        [SerializeField] private int commandPort = 3939;

        [Header("Discovery")]
        [SerializeField] private bool autoDiscoverHost = true;
        [SerializeField] private int discoveryPort = 4949;
        [SerializeField] private string discoveryAction = "DiscoverHost";
        [SerializeField] private string hostAnnouncementAction = "HostAnnouncement";
        [SerializeField] private float discoveryTimeoutSeconds = 2f;
        [SerializeField] private float discoveryRetryIntervalSeconds = 5f;
        [SerializeField] private int maxDiscoveryAttempts = 3;

        [Header("Actions")]
        [SerializeField] private string registerAction = "RegisterClient";
        [SerializeField] private string heartbeatAction = "Heartbeat";

        [Header("Timing")]
        [SerializeField] private bool registerOnEnable = true;
        [SerializeField] private float heartbeatIntervalSeconds = 10f;

        [Header("Debug")]
        [SerializeField] private bool enableLogging = true;

        private IPEndPoint cachedEndpoint;
        private string cachedHostAddress;
        private UdpClient udpClient;
        private Coroutine heartbeatRoutine;
        private Coroutine initializeRoutine;
        private bool hasSentRegistration;
        private bool hasLoggedMissingHost;
        private UdpCommandListener commandListener;

        private bool IsHostResolved =>
            !string.IsNullOrWhiteSpace(hostAddress) &&
            !string.Equals(hostAddress, "auto", StringComparison.OrdinalIgnoreCase);

        private void OnEnable()
        {
            SceneManager.activeSceneChanged += HandleSceneChanged;
            commandListener = UdpCommandListener.Instance;
            if (commandListener != null)
            {
                commandListener.CommandReceived += HandleCommandReceived;
            }

            hasSentRegistration = false;
            hasLoggedMissingHost = false;
            initializeRoutine = StartCoroutine(InitializeRoutine());
        }

        private void OnDisable()
        {
            SceneManager.activeSceneChanged -= HandleSceneChanged;

            if (commandListener != null)
            {
                commandListener.CommandReceived -= HandleCommandReceived;
                commandListener = null;
            }

            if (initializeRoutine != null)
            {
                StopCoroutine(initializeRoutine);
                initializeRoutine = null;
            }

            StopHeartbeat();
            DisposeClient();
            ResetCachedEndpoint();
        }

        private IEnumerator InitializeRoutine()
        {
            if (autoDiscoverHost)
            {
                var retryWait = new WaitForSecondsRealtime(Mathf.Max(1f, discoveryRetryIntervalSeconds));
                while (ShouldAttemptDiscovery())
                {
                    var discoveryTask = DiscoverHostAsync();
                    while (!discoveryTask.IsCompleted)
                    {
                        yield return null;
                    }

                    if (discoveryTask.IsFaulted)
                    {
                        LogWarning($"UdpClientReporter discovery failed: {discoveryTask.Exception?.GetBaseException().Message}");
                    }
                    else if (discoveryTask.Result)
                    {
                        break;
                    }

                    if (!autoDiscoverHost || !ShouldAttemptDiscovery())
                    {
                        break;
                    }

                    yield return retryWait;
                }
            }

            MaybeStartReporting(forceRegister: registerOnEnable);
        }

        [ContextMenu("Send Registration")]
        public void RegisterClient()
        {
            if (!EnsureHostReady("registration"))
            {
                return;
            }

            var payload = new ClientRegistrationPayload
            {
                deviceId = SystemInfo.deviceUniqueIdentifier,
                deviceName = SystemInfo.deviceName,
                platform = Application.platform.ToString(),
                buildVersion = Application.version,
                ipv4 = ResolveLocalIPv4(),
                scene = SceneManager.GetActiveScene().name,
                timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                commandPort = commandPort
            };

            if (SendCommand(registerAction, payload))
            {
                hasSentRegistration = true;
            }
        }

        private void HandleCommandReceived(UdpCommandMessage message)
        {
            if (message == null || string.IsNullOrEmpty(message.action))
            {
                return;
            }

            if (!string.Equals(message.action, hostAnnouncementAction, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            HostAnnouncementPayload payload = null;
            if (!string.IsNullOrEmpty(message.payload))
            {
                try
                {
                    payload = JsonUtility.FromJson<HostAnnouncementPayload>(message.payload);
                }
                catch (ArgumentException ex)
                {
                    LogWarning($"UdpClientReporter host announcement parse error: {ex.Message}");
                }
            }

            if (ApplyHostAnnouncement(payload, null))
            {
                MaybeStartReporting();
            }
        }

        private void MaybeStartReporting(bool forceRegister = false)
        {
            if (!IsHostResolved)
            {
                return;
            }

            hasLoggedMissingHost = false;

            if (!hasSentRegistration && (forceRegister || registerOnEnable))
            {
                RegisterClient();
            }

            StartHeartbeat();
        }

        private bool ShouldAttemptDiscovery()
        {
            return !IsHostResolved &&
                   discoveryPort > 0 &&
                   maxDiscoveryAttempts > 0 &&
                   !string.IsNullOrWhiteSpace(discoveryAction) &&
                   !string.IsNullOrWhiteSpace(hostAnnouncementAction);
        }

        private void OnValidate()
        {
            if (commandPort < 1)
            {
                commandPort = 1;
            }
            else if (commandPort > 65535)
            {
                commandPort = 65535;
            }
        }

        private async Task<bool> DiscoverHostAsync()
        {
            var requestPayload = new ClientDiscoveryPayload
            {
                deviceId = SystemInfo.deviceUniqueIdentifier,
                deviceName = SystemInfo.deviceName,
                platform = Application.platform.ToString(),
                buildVersion = Application.version,
                scene = SceneManager.GetActiveScene().name,
                requestPort = hostPort,
                commandPort = commandPort
            };

            var requestMessage = new UdpCommandMessage
            {
                action = discoveryAction,
                timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                payload = JsonUtility.ToJson(requestPayload)
            };

            var payloadJson = JsonUtility.ToJson(requestMessage);
            var buffer = Encoding.UTF8.GetBytes(payloadJson);
            var broadcastEndpoint = new IPEndPoint(IPAddress.Broadcast, discoveryPort);

            for (var attempt = 0; attempt < maxDiscoveryAttempts; attempt++)
            {
                try
                {
                    using var client = new UdpClient(0) { EnableBroadcast = true };

                    await client.SendAsync(buffer, buffer.Length, broadcastEndpoint);

                    var receiveTask = client.ReceiveAsync();
                    var delayTask = Task.Delay(TimeSpan.FromSeconds(Mathf.Max(0.25f, discoveryTimeoutSeconds)));

                    var completed = await Task.WhenAny(receiveTask, delayTask);
                    if (completed != receiveTask)
                    {
                        try
                        {
                            client.Close();
                            await receiveTask;
                        }
                        catch
                        {
                            // ignored
                        }

                        continue;
                    }

                    var result = await receiveTask;
                    var responseJson = Encoding.UTF8.GetString(result.Buffer);

                    UdpCommandMessage responseMessage;
                    try
                    {
                        responseMessage = JsonUtility.FromJson<UdpCommandMessage>(responseJson);
                    }
                    catch (ArgumentException ex)
                    {
                        LogWarning($"UdpClientReporter discovery response parse error: {ex.Message}");
                        continue;
                    }

                    if (responseMessage == null ||
                        !string.Equals(responseMessage.action, hostAnnouncementAction, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    HostAnnouncementPayload announcement = null;
                    if (!string.IsNullOrEmpty(responseMessage.payload))
                    {
                        try
                        {
                            announcement = JsonUtility.FromJson<HostAnnouncementPayload>(responseMessage.payload);
                        }
                        catch (ArgumentException ex)
                        {
                            LogWarning($"UdpClientReporter host announcement payload parse error: {ex.Message}");
                        }
                    }

                    if (ApplyHostAnnouncement(announcement, result.RemoteEndPoint))
                    {
                        LogDebug($"UdpClientReporter discovered host {hostAddress}:{hostPort}.");
                        return true;
                    }
                }
                catch (Exception ex)
                {
                    LogWarning($"UdpClientReporter discovery attempt failed: {ex.Message}");
                }
            }

            return false;
        }

        private bool ApplyHostAnnouncement(HostAnnouncementPayload payload, IPEndPoint remote)
        {
            var resolvedAddress = !string.IsNullOrWhiteSpace(payload?.hostAddress)
                ? payload.hostAddress
                : remote?.Address.ToString();

            if (string.IsNullOrWhiteSpace(resolvedAddress))
            {
                return false;
            }

            var resolvedPort = payload?.hostPort > 0 ? payload.hostPort : hostPort;

            var addressChanged = !string.Equals(hostAddress, resolvedAddress, StringComparison.OrdinalIgnoreCase);
            var portChanged = resolvedPort != hostPort;

            hostAddress = resolvedAddress;
            hostPort = resolvedPort;
            discoveryPort = hostPort;

            if (addressChanged || portChanged)
            {
                ResetCachedEndpoint();
                hasSentRegistration = false;
            }

            hasLoggedMissingHost = false;
            return true;
        }

        private void StartHeartbeat()
        {
            if (heartbeatIntervalSeconds <= 0f || heartbeatRoutine != null || !IsHostResolved)
            {
                return;
            }

            heartbeatRoutine = StartCoroutine(HeartbeatLoop());
        }

        private void StopHeartbeat()
        {
            if (heartbeatRoutine == null)
            {
                return;
            }

            StopCoroutine(heartbeatRoutine);
            heartbeatRoutine = null;
        }

        private IEnumerator HeartbeatLoop()
        {
            var wait = new WaitForSeconds(Mathf.Max(1f, heartbeatIntervalSeconds));
            while (enabled)
            {
                yield return wait;
                SendHeartbeat();
            }
        }

        private void SendHeartbeat()
        {
            if (!EnsureHostReady("heartbeat"))
            {
                return;
            }

            var payload = new ClientHeartbeatPayload
            {
                deviceId = SystemInfo.deviceUniqueIdentifier,
                scene = SceneManager.GetActiveScene().name,
                timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                commandPort = commandPort
            };

            SendCommand(heartbeatAction, payload, includeCmdId: false);
        }

        private void HandleSceneChanged(Scene previous, Scene current)
        {
            SendHeartbeat();
        }

        private bool SendCommand(string action, object payload, bool includeCmdId = true)
        {
            if (!EnsureHostReady($"sending '{action}'"))
            {
                return false;
            }

            if (!TryResolveEndpoint(out var endpoint))
            {
                return false;
            }

            EnsureClient();

            var message = new UdpCommandMessage
            {
                action = action,
                timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            };

            if (includeCmdId)
            {
                message.cmdId = Guid.NewGuid().ToString("N").Substring(0, 8);
            }

            if (payload != null)
            {
                try
                {
                    message.payload = JsonUtility.ToJson(payload);
                }
                catch (ArgumentException ex)
                {
                    LogWarning($"UdpClientReporter payload serialization failed: {ex.Message}");
                    return false;
                }
            }

            if (!string.IsNullOrEmpty(sharedSecret))
            {
                message.signature = ComputeSignature(message);
            }

            var json = JsonUtility.ToJson(message);
            var bytes = Encoding.UTF8.GetBytes(json);

            try
            {
                udpClient.Send(bytes, bytes.Length, endpoint);
                return true;
            }
            catch (Exception ex)
            {
                LogWarning($"UdpClientReporter failed to send '{action}' message: {ex.Message}");
                return false;
            }
        }

        private bool TryResolveEndpoint(out IPEndPoint endpoint)
        {
            endpoint = null;

            if (!IsHostResolved)
            {
                return false;
            }

            if (cachedEndpoint != null &&
                string.Equals(hostAddress, cachedHostAddress, StringComparison.OrdinalIgnoreCase) &&
                cachedEndpoint.Port == hostPort)
            {
                endpoint = cachedEndpoint;
                return true;
            }

            try
            {
                var addresses = Dns.GetHostAddresses(hostAddress);
                foreach (var address in addresses)
                {
                    if (address.AddressFamily != AddressFamily.InterNetwork)
                    {
                        continue;
                    }

                    cachedEndpoint = new IPEndPoint(address, hostPort);
                    cachedHostAddress = hostAddress;
                    endpoint = cachedEndpoint;
                    return true;
                }
            }
            catch (Exception ex)
            {
                LogWarning($"UdpClientReporter failed to resolve host '{hostAddress}': {ex.Message}");
            }

            LogWarning($"UdpClientReporter could not resolve an IPv4 endpoint for '{hostAddress}'.");
            return false;
        }

        private bool EnsureHostReady(string context)
        {
            if (IsHostResolved)
            {
                hasLoggedMissingHost = false;
                return true;
            }

            if (hasLoggedMissingHost)
            {
                return false;
            }

            var reason = autoDiscoverHost
                ? "host has not responded to discovery yet"
                : "host address is not configured";
            LogWarning($"UdpClientReporter skipped {context}: {reason}.");
            hasLoggedMissingHost = true;
            return false;
        }

        private void EnsureClient()
        {
            if (udpClient != null)
            {
                return;
            }

            udpClient = new UdpClient
            {
                EnableBroadcast = true
            };
        }

        private void DisposeClient()
        {
            if (udpClient == null)
            {
                return;
            }

            try
            {
                udpClient.Close();
            }
            catch
            {
                // ignore shutdown errors
            }

            udpClient.Dispose();
            udpClient = null;
        }

        private void ResetCachedEndpoint()
        {
            cachedEndpoint = null;
            cachedHostAddress = null;
        }

        private string ComputeSignature(UdpCommandMessage message)
        {
            var canonical = $"{message.action}|{message.payload ?? string.Empty}|{message.timestamp}";
            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(sharedSecret));
            return Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes(canonical)));
        }

        private static string ResolveLocalIPv4()
        {
            try
            {
                foreach (var adapter in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (adapter.OperationalStatus != OperationalStatus.Up ||
                        adapter.NetworkInterfaceType == NetworkInterfaceType.Loopback)
                    {
                        continue;
                    }

                    var properties = adapter.GetIPProperties();
                    foreach (var unicast in properties.UnicastAddresses)
                    {
                        if (unicast.Address.AddressFamily == AddressFamily.InterNetwork &&
                            !IPAddress.IsLoopback(unicast.Address))
                        {
                            return unicast.Address.ToString();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // LogWarning($"UdpClientReporter failed to resolve local IPv4: {ex.Message}");
            }

            return string.Empty;
        }

        private void LogDebug(string message)
        {
            if (enableLogging)
            {
                Debug.Log(message);
            }
        }

        private void LogWarning(string message)
        {
            if (enableLogging)
            {
                Debug.LogWarning(message);
            }
        }

        private void LogError(string message)
        {
            Debug.LogError(message);
        }

        [Serializable]
        private sealed class ClientRegistrationPayload
        {
            public string deviceId;
            public string deviceName;
            public string platform;
            public string buildVersion;
            public string ipv4;
            public string scene;
            public long timestamp;
            public int commandPort;
        }

        [Serializable]
        private sealed class ClientHeartbeatPayload
        {
            public string deviceId;
            public string scene;
            public long timestamp;
            public int commandPort;
        }

        [Serializable]
        private sealed class ClientDiscoveryPayload
        {
            public string deviceId;
            public string deviceName;
            public string platform;
            public string buildVersion;
            public string scene;
            public int requestPort;
            public int commandPort;
        }

        [Serializable]
        private sealed class HostAnnouncementPayload
        {
            public string hostName;
            public string hostAddress;
            public int hostPort;
            public int commandPort;
        }
    }
}
