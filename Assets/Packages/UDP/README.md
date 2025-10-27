# UDP Command Toolkit

Modern UDP command infrastructure extracted from Mnemosyne VR and adapted for Hankun Unity packages. Provides a resilient command listener, optional headset/client reporter, and documentation for host-side tooling.

## Features
- Background UDP listener that dispatches strongly typed `UdpCommandMessage` events on the Unity main thread
- Optional acknowledgement replies with deduplication, authorization via shared secret, and stale command filtering
- `UdpClientReporter` component for automatic host discovery, client registration, and heartbeat updates
- Inspector-driven bindings with support for code-based handler registration
- Shared singleton base to keep networking behaviours persistent between scenes

Refer to `Documentation~/UDPCommandToolkit.md` for JSON envelope details and configuration guidance.
