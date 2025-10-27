# UDP Command Listener

Attach `UdpCommandListener` to a persistent scene object (e.g. a networking bootstrapper) on each headset. When the component is enabled it can listen for UDP broadcast or multicast traffic and dispatch commands on the Unity main thread.

## Command Envelope

Incoming datagrams must contain UTF-8 JSON that maps onto `UdpCommandMessage`:

```json
{
  "cmdId": "8f8d2d86",
  "action": "ToggleCredits",
  "payload": "{\"enabled\":true}",
  "timestamp": 1731457023123,
  "signature": "base64-hmac-of-action|payload|timestamp"
}
```

- `cmdId` is optional but enables de-duplication and acknowledgements.
- `timestamp` is treated as Unix milliseconds (values under `10_000_000_000` are interpreted as seconds). Set the `Stale Command Window Seconds` Inspector field to drop old packets.
- `payload` is an arbitrary JSON string. Deserialize it downstream via `JsonUtility.FromJson<T>(message.payload)`.
- `signature` is optional unless a shared secret is configured. When `Shared Secret` is populated, the sender must include `base64(HMACSHA256(action|payload|timestamp, secret))`.

The listener will optionally reply with an acknowledgement JSON envelope:

```json
{
  "cmdId": "8f8d2d86",
  "status": "received",
  "receivedTimestamp": 1731457023781
}
```

## Hooking Up Handlers

Use the `Bindings` list on the component to map an `action` string to a UnityEvent callback, or register handlers in code:

```csharp
void Start()
{
    listener.RegisterHandler("ToggleCredits", OnToggleCredits);
}

private void OnToggleCredits(UdpCommandMessage command)
{
    var payload = JsonUtility.FromJson<TogglePayload>(command.payload);
    creditsCanvas.enabled = payload.enabled;
}
```

The `On Command Received` UnityEvent (or the runtime `CommandReceived` event) fires for every valid command, regardless of matching bindings.

## Runtime Notes

- 组件继承自 `SingletonBehaviour<UdpCommandListener>`，首个激活实例会自动标记 `DontDestroyOnLoad` 并成为全局单例，后续重复挂载会在 `Awake` 阶段被销毁。
- The component spins a background thread that pushes work onto the main thread, so command handlers are always safe to interact with Unity APIs.
- `Auto Start` begins listening in `OnEnable`. Call `StartListening()` manually if you need tighter lifecycle control.
- `Send Acknowledgement` sends a UDP reply to the origin endpoint for simple reliability loops.
- `Enable Logging` silences routine UDP warnings in production builds while continuing to surface errors.
- Heartbeats or registrations can be implemented by sending a JSON command from the host and binding it to a handler that responds via your own transport (or extend this component as needed).

## Client Registration & Heartbeats

Attach `UdpClientReporter` alongside the listener if the headset should announce itself to a host dashboard:

1. Leave `Host Address` set to `auto` (or supply a static IP) so the device knows where to send registration packets; `Host Port` defaults to `4949`.
2. When auto discovery is enabled, the reporter broadcasts a `DiscoverHost` action (retrying every few seconds) and applies any `HostAnnouncement` payloads emitted by the Electron host tool, automatically filling in the IP/port.
3. Optionally set a `Shared Secret` to align with the listener's HMAC configuration.
4. On enable, the reporter sends a `RegisterClient` action containing device ID, name, app version, IPv4, and the active scene.
5. A heartbeat (default 30 seconds) keeps the host informed of the current scene; scene transitions trigger an immediate update. Command payloads also include the gameplay command port (default 3939) so the host can rebroadcast commands directly.

The reporter emits standard `UdpCommandMessage` envelopes, so the Electron host tool can display them without additional parsing. Manual host configuration is still honoured if auto discovery is disabled or a static IP is provided.
