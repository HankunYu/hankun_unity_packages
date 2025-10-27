using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using UnityEngine;
using UnityEngine.Events;

namespace Mnemosyne.Networking
{
    /// <summary>
    /// Listens for UDP broadcast/multicast commands and dispatches them to registered handlers on the main thread.
    /// </summary>
    public sealed class UdpCommandListener : SingletonBehaviour<UdpCommandListener>
    {
        [SerializeField] private int listenPort = 3939;
        [SerializeField] private bool autoStart = true;
        [SerializeField] private bool sendAcknowledgement = true;
        [SerializeField] private string sharedSecret = string.Empty;
        [Tooltip("Commands older than this window (seconds) are ignored. Set to 0 to disable.")]
        [SerializeField] private float staleCommandWindowSeconds = 5f;
        [SerializeField] private UdpCommandBinding[] bindings = Array.Empty<UdpCommandBinding>();
        [SerializeField] private UdpCommandUnityEvent onCommandReceived = new UdpCommandUnityEvent();
        [Header("Debug")]
        [SerializeField] private bool enableLogging = true;
        [SerializeField] private bool enableBeep = true;

        /// <summary>
        /// Raised for every valid command before specific bindings execute.
        /// </summary>
        public event Action<UdpCommandMessage> CommandReceived;

        /// <summary>
        /// UnityEvent hook for inspector-based listeners.
        /// </summary>
        public UdpCommandUnityEvent OnCommandReceived => onCommandReceived;

        private readonly ConcurrentQueue<PendingCommand> _pendingCommands = new ConcurrentQueue<PendingCommand>();
        private readonly Dictionary<string, UdpCommandBinding> _bindingLookup = new Dictionary<string, UdpCommandBinding>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, long> _processedCommands = new Dictionary<string, long>();
        private readonly object _processedLock = new object();

        private const int MaxRememberedCommandIds = 128;

        private CancellationTokenSource _listenCts;
        private Thread _listenerThread;
        private UdpClient _receiveClient;
        private UdpClient _ackClient;
        private bool _isListening;
        private AudioSource _audioSource;

        private bool IsPrimaryInstance => ReferenceEquals(Instance, this);

        protected override void Awake()
        {
            base.Awake();
            if (!IsPrimaryInstance)
            {
                return;
            }

            BuildBindingLookup();
            if (enableBeep)
            {
                onCommandReceived.AddListener(PlayBeep);
            }
        }

        private void OnValidate()
        {
            BuildBindingLookup();
            if (_audioSource == null && enableBeep)
            {
                _audioSource = gameObject.TryGetComponent(out AudioSource audioSource)
                    ? audioSource
                    : gameObject.AddComponent<AudioSource>();
            }
        }
        
        private void PlayBeep(UdpCommandMessage message)
        {
            if (!enableBeep || message.action != "beep")
                return;

            float frequency = 880f;
            float duration = 0.15f;
            float volume = 0.5f;

            int sampleRate = 44100;
            int sampleLength = Mathf.CeilToInt(sampleRate * duration);
            float[] samples = new float[sampleLength];

            for (int i = 0; i < sampleLength; i++)
            {
                samples[i] = Mathf.Sin(2 * Mathf.PI * frequency * i / sampleRate) * volume;
            }

            AudioClip clip = AudioClip.Create("Beep", sampleLength, 1, sampleRate, false);
            clip.SetData(samples, 0);

            if (_audioSource == null)
                _audioSource = gameObject.AddComponent<AudioSource>();

            _audioSource.PlayOneShot(clip);
        }

        private void OnEnable()
        {
            if (!IsPrimaryInstance)
            {
                return;
            }

            if (autoStart)
            {
                StartListening();
            }
        }

        private void OnDisable()
        {
            if (!IsPrimaryInstance)
            {
                return;
            }

            StopListening();
            DrainPendingCommands();
        }

        private void Update()
        {
            if (!IsPrimaryInstance)
            {
                return;
            }

            while (_pendingCommands.TryDequeue(out var pending))
            {
                DispatchCommand(pending);
            }
        }

        private void BuildBindingLookup()
        {
            _bindingLookup.Clear();
            if (bindings == null)
            {
                return;
            }

            foreach (var binding in bindings)
            {
                if (binding == null || string.IsNullOrWhiteSpace(binding.Action))
                {
                    continue;
                }

                if (_bindingLookup.ContainsKey(binding.Action))
                {
                    LogWarning($"UdpCommandListener duplicate handler for action '{binding.Action}'. Ignoring subsequent entries.");
                    continue;
                }

                _bindingLookup.Add(binding.Action, binding);
            }
        }

        /// <summary>
        /// Begins listening for incoming UDP commands.
        /// </summary>
        public void StartListening()
        {
            if (!IsPrimaryInstance)
            {
                return;
            }

            if (_isListening)
            {
                return;
            }

            try
            {
                _receiveClient = new UdpClient(listenPort)
                {
                    EnableBroadcast = true
                };
                _ackClient = new UdpClient();
            }
            catch (Exception ex)
            {
                LogError($"UdpCommandListener failed to bind UDP port {listenPort}: {ex.Message}");
                CleanupSockets();
                return;
            }

            _listenCts = new CancellationTokenSource();
            _listenerThread = new Thread(() => ListenLoop(_listenCts.Token))
            {
                IsBackground = true,
                Name = "UdpCommandListener"
            };
            _listenerThread.Start();
            _isListening = true;
        }

        /// <summary>
        /// Stops listening and disposes networking resources.
        /// </summary>
        public void StopListening()
        {
            if (!IsPrimaryInstance)
            {
                return;
            }

            if (!_isListening)
            {
                return;
            }

            try
            {
                _listenCts?.Cancel();
                _receiveClient?.Close();
            }
            catch (Exception ex)
            {
                LogWarning($"UdpCommandListener stop warning: {ex.Message}");
            }

            try
            {
                if (_listenerThread != null && _listenerThread.IsAlive)
                {
                    _listenerThread.Join(100);
                }
            }
            catch (Exception ex)
            {
                LogWarning($"UdpCommandListener join warning: {ex.Message}");
            }

            CleanupSockets();
            _listenCts?.Dispose();
            _listenCts = null;
            _listenerThread = null;
            _isListening = false;

            lock (_processedLock)
            {
                _processedCommands.Clear();
            }
        }

        /// <summary>
        /// Registers a runtime handler for the specified action. Handlers run on the main thread.
        /// </summary>
        public void RegisterHandler(string action, Action<UdpCommandMessage> handler)
        {
            if (string.IsNullOrEmpty(action))
            {
                throw new ArgumentException("Action must be non-empty.", nameof(action));
            }

            if (handler == null)
            {
                throw new ArgumentNullException(nameof(handler));
            }

            if (!_bindingLookup.TryGetValue(action, out var binding))
            {
                binding = new UdpCommandBinding(action);
                _bindingLookup[action] = binding;
            }

            binding.RegisterHandler(handler);
        }

        /// <summary>
        /// Removes a handler registered via RegisterHandler.
        /// </summary>
        public void UnregisterHandler(string action, Action<UdpCommandMessage> handler)
        {
            if (string.IsNullOrEmpty(action) || handler == null)
            {
                return;
            }

            if (_bindingLookup.TryGetValue(action, out var binding))
            {
                binding.UnregisterHandler(handler);
            }
        }

        private void ListenLoop(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    var remoteEndpoint = new IPEndPoint(IPAddress.Any, 0);
                    var buffer = _receiveClient.Receive(ref remoteEndpoint);
                    if (buffer == null || buffer.Length == 0)
                    {
                        continue;
                    }

                    var messageJson = Encoding.UTF8.GetString(buffer);
                    var command = ParseMessage(messageJson);
                    if (command == null)
                    {
                        continue;
                    }

                    if (!IsAuthorized(command))
                    {
                        LogWarning($"UdpCommandListener rejected unauthorized command: {command.action}");
                        continue;
                    }

                    if (IsStale(command))
                    {
                        LogWarning($"UdpCommandListener dropped stale command {command.cmdId}");
                        continue;
                    }

                    if (!RegisterCommandId(command))
                    {
                        LogDebug($"UdpCommandListener ignored duplicate command {command.cmdId}");
                        continue;
                    }

                    _pendingCommands.Enqueue(new PendingCommand(command, remoteEndpoint));
                }
                catch (SocketException socketEx) when (socketEx.SocketErrorCode == SocketError.Interrupted)
                {
                    break;
                }
                catch (ObjectDisposedException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    LogWarning($"UdpCommandListener receive exception: {ex.Message}");
                }
            }
        }

        private UdpCommandMessage ParseMessage(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return null;
            }

            try
            {
                var command = JsonUtility.FromJson<UdpCommandMessage>(json);
                if (command == null || string.IsNullOrEmpty(command.action))
                {
                    LogWarning($"UdpCommandListener received malformed command: {json}");
                    return null;
                }

                return command;
            }
            catch (ArgumentException ex)
            {
                LogWarning($"UdpCommandListener JSON parse error: {ex.Message}");
                return null;
            }
        }

        private bool IsAuthorized(UdpCommandMessage message)
        {
            if (string.IsNullOrEmpty(sharedSecret))
            {
                return true;
            }

            if (string.IsNullOrEmpty(message.signature))
            {
                return false;
            }

            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(sharedSecret));
            var canonical = $"{message.action}|{message.payload ?? string.Empty}|{message.timestamp}";
            var expectedSignature = Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes(canonical)));
            return CryptographicEquals(expectedSignature, message.signature);
        }

        private bool IsStale(UdpCommandMessage message)
        {
            if (staleCommandWindowSeconds <= 0f || message.timestamp <= 0)
            {
                return false;
            }

            var timestamp = message.timestamp > 9_999_999_999
                ? DateTimeOffset.FromUnixTimeMilliseconds(message.timestamp)
                : DateTimeOffset.FromUnixTimeSeconds(message.timestamp);

            var ageSeconds = (DateTimeOffset.UtcNow - timestamp).TotalSeconds;
            return ageSeconds > staleCommandWindowSeconds;
        }

        private bool RegisterCommandId(UdpCommandMessage message)
        {
            if (string.IsNullOrEmpty(message.cmdId))
            {
                return true;
            }

            lock (_processedLock)
            {
                if (_processedCommands.ContainsKey(message.cmdId))
                {
                    return false;
                }

                _processedCommands[message.cmdId] = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                if (_processedCommands.Count > MaxRememberedCommandIds)
                {
                    PruneProcessedCommands();
                }
            }

            return true;
        }

        private void PruneProcessedCommands()
        {
            var cutoff = DateTimeOffset.UtcNow.ToUnixTimeSeconds() - (long)Math.Max(1, staleCommandWindowSeconds);
            var toRemove = new List<string>();
            foreach (var kvp in _processedCommands)
            {
                if (kvp.Value < cutoff)
                {
                    toRemove.Add(kvp.Key);
                }
            }

            foreach (var key in toRemove)
            {
                _processedCommands.Remove(key);
            }
        }

        private void DispatchCommand(PendingCommand pending)
        {
            onCommandReceived?.Invoke(pending.Message);
            CommandReceived?.Invoke(pending.Message);

            if (_bindingLookup.TryGetValue(pending.Message.action, out var binding))
            {
                binding.Invoke(pending.Message);
            }
            else
            {
                LogWarning($"UdpCommandListener has no handler for action '{pending.Message.action}'.");
            }

            if (sendAcknowledgement && pending.RemoteEndPoint != null)
            {
                SendAcknowledgement(pending);
            }
        }

        private void SendAcknowledgement(PendingCommand pending)
        {
            if (_ackClient == null)
            {
                return;
            }

            try
            {
                var ack = new UdpCommandAcknowledgement
                {
                    cmdId = pending.Message.cmdId,
                    status = "received",
                    receivedTimestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
                };

                var ackJson = JsonUtility.ToJson(ack);
                var payload = Encoding.UTF8.GetBytes(ackJson);
                _ackClient.Send(payload, payload.Length, pending.RemoteEndPoint);
            }
            catch (Exception ex)
            {
                LogWarning($"UdpCommandListener failed to send ACK: {ex.Message}");
            }
        }

        private void CleanupSockets()
        {
            try
            {
                _receiveClient?.Dispose();
            }
            catch
            {
                // ignored
            }

            try
            {
                _ackClient?.Dispose();
            }
            catch
            {
                // ignored
            }

            _receiveClient = null;
            _ackClient = null;
        }

        private void DrainPendingCommands()
        {
            while (_pendingCommands.TryDequeue(out _))
            {
            }
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

        private static bool CryptographicEquals(string a, string b)
        {
            if (a == null || b == null || a.Length != b.Length)
            {
                return false;
            }

            var result = 0;
            for (var i = 0; i < a.Length; i++)
            {
                result |= a[i] ^ b[i];
            }

            return result == 0;
        }

        [Serializable]
        private sealed class UdpCommandBinding
        {
            [SerializeField] private string action = string.Empty;
            [SerializeField] private UdpCommandUnityEvent onCommand = new UdpCommandUnityEvent();

            private event Action<UdpCommandMessage> RuntimeHandlers;

            public UdpCommandBinding()
            {
            }

            public UdpCommandBinding(string actionName)
            {
                action = actionName;
            }

            public string Action => action;

            public void Invoke(UdpCommandMessage message)
            {
                onCommand?.Invoke(message);
                RuntimeHandlers?.Invoke(message);
            }

            public void RegisterHandler(Action<UdpCommandMessage> handler)
            {
                RuntimeHandlers += handler;
            }

            public void UnregisterHandler(Action<UdpCommandMessage> handler)
            {
                RuntimeHandlers -= handler;
            }
        }

        private readonly struct PendingCommand
        {
            public readonly UdpCommandMessage Message;
            public readonly IPEndPoint RemoteEndPoint;

            public PendingCommand(UdpCommandMessage message, IPEndPoint remoteEndPoint)
            {
                Message = message;
                RemoteEndPoint = remoteEndPoint;
            }
        }

        [Serializable]
        private sealed class UdpCommandAcknowledgement
        {
            public string cmdId;
            public string status;
            public long receivedTimestamp;
        }
    }

    [Serializable]
    public sealed class UdpCommandMessage
    {
        public string cmdId;
        public string action;
        public string payload;
        public long timestamp;
        public string signature;

        public bool TryGetPayload<T>(out T value)
        {
            value = default;
            if (string.IsNullOrEmpty(payload))
            {
                return false;
            }

            try
            {
                value = JsonUtility.FromJson<T>(payload);
                return value != null;
            }
            catch (ArgumentException)
            {
                value = default;
                return false;
            }
        }
    }

    [Serializable]
    public sealed class UdpCommandUnityEvent : UnityEvent<UdpCommandMessage>
    {
    }
}
