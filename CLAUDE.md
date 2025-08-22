# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

This is a Unity-based VR multiplayer project called "MetaAvatarVR" developed by HackMonkeys. It's built for Meta Quest headsets with support for Meta Avatars, multiplayer networking using Photon Fusion, and various VR interaction mechanics.

## Key Technologies and SDKs

- **Unity Version**: 6000.0.23f1 (Unity 2023)
- **VR Platform**: Meta Quest (Android build target)
- **Multiplayer**: Photon Fusion
- **Avatar System**: Meta Avatars SDK v35.0.0
- **XR SDKs**: 
  - Meta XR SDK v74.0.2
  - Unity XR Interaction Toolkit v3.0.7
  - OpenXR with Meta Quest support
- **Rendering**: Universal Render Pipeline (URP) v17.0.3

## Development Commands

### Build Commands
```bash
# Build for Meta Quest (Android)
# Use Unity Editor: File > Build Settings > Android > Build

# To build from command line (example):
Unity.exe -batchmode -quit -projectPath "D:\Unity\HackMonkeys\MetaAvatarsVR" -executeMethod BuildScript.BuildAndroid
```

### Unity-Specific Development
- **Play Mode**: Use Unity Editor's Play button to test in VR simulation
- **Meta Quest Link**: Connect Quest headset via USB/Air Link for testing
- **Build Settings**: Ensure Android platform is selected with IL2CPP backend

### Code Quality Tools
Since this is a Unity project, standard C# tools apply:
- **IDE**: Visual Studio 2022 or JetBrains Rider
- **Code formatting**: Uses Unity's default C# conventions
- **Testing**: Unity Test Framework is included (com.unity.test-framework)

## Project Architecture

### Core Systems

1. **Networking Architecture** (`Assets/_Project/Scripts/Networking/`)
   - `ConnectionManager.cs`: Manages Photon Fusion connection and session lifecycle
   - `NetworkBootstrapper.cs`: Handles initial network setup and scene transitions
   - `LobbyController.cs`: Controls lobby state and player management
   - Uses Photon Fusion for client-server architecture with host migration support

2. **Avatar System** (`Assets/_Project/Scripts/Avatar/`)
   - `AvatarEntityState.cs`: Extends Meta's OvrAvatarEntity for networked avatars
   - `AvatarStateSync.cs`: Handles avatar state synchronization across network
   - `NetworkAvatarSpawner.cs`: Manages avatar spawning for players
   - Integrates Meta Avatars SDK with custom networking layer

3. **UI System** (`Assets/_Project/Scripts/MainMenu/`)
   - Spatial 3D UI designed for VR interaction
   - `MenuPanel.cs`: Base class for VR menu panels
   - `LobbyBrowser.cs`: Browse and join multiplayer sessions
   - `InteractableButton3D.cs`, `InteractableInputField3D.cs`: VR-specific UI components
   - `VirtualKeyboard3D.cs`: Virtual keyboard for text input in VR

4. **Gameplay Systems** (`Assets/_Project/Scripts/Mecanicas/`)
   - Puzzle mechanics for office and laboratory levels
   - `PuzzleManager.cs`: Central puzzle state management
   - Various interactive objects (valves, levers, computers, piano keys)
   - Wrist menu system for in-game UI

### Scene Organization

- **MainMenu**: Main menu scene with lobby browser
- **Desarrollo folder**: Development scenes for office and laboratory environments
- Scenes use additive loading for smooth transitions
- Network scenes loaded via Photon Fusion's scene management

### Key Patterns

1. **Singleton Pattern**: Used for managers (GameCore, ConnectionManager)
2. **Component-based Architecture**: Unity's GameObject/Component system
3. **Event System**: Unity Events and C# events for decoupling
4. **Coroutines & async/await**: For asynchronous operations
5. **Fusion's NetworkBehaviour**: For networked objects and RPCs

## VR-Specific Considerations

- **Input**: Uses Unity's XR Interaction Toolkit for controller input
- **Movement**: Supports teleportation and continuous movement
- **Hand Tracking**: Meta XR Hands SDK integrated
- **Performance**: Optimized for Quest 2/3 (mobile VR)
- **Comfort**: Implements standard VR comfort options

## Multiplayer Architecture

- **Network Framework**: Photon Fusion (client-server model)
- **Session Management**: Custom lobby system with room browser
- **Player Limit**: Configured for small groups (typically 2-4 players)
- **Voice Chat**: Photon Voice integrated for spatial audio
- **State Synchronization**: Uses Fusion's tick-based simulation

## Common Development Tasks

### Adding New VR Interactions
1. Create scripts extending XR Interaction Toolkit components
2. Configure Interactable layers and interaction managers
3. Test with both controllers and hand tracking

### Creating Networked Objects
1. Add NetworkObject component to GameObject
2. Create script extending NetworkBehaviour
3. Use [Networked] properties for synchronized state
4. Implement RPCs for events

### Modifying UI for VR
1. Use world space canvases
2. Implement hover/select states for VR controllers
3. Ensure UI elements are at comfortable distances (1-3 meters)
4. Test readability at various distances

## Important Files and Locations

- **Player Settings**: `ProjectSettings/ProjectSettings.asset`
- **XR Settings**: `Assets/XR/XRGeneralSettingsPerBuildTarget.asset`
- **Network Config**: Photon Fusion settings in Resources folder
- **Avatar Config**: `Assets/Oculus/OculusProjectConfig.asset`
- **Build Output**: `Builds/Android/` directory

## Tips for Development

1. **VR Testing**: Always test with actual VR hardware when possible
2. **Performance**: Monitor frame rate (target 72/90 fps for Quest)
3. **Avatar Loading**: Meta avatars require internet connection and entitlement check
4. **Network Testing**: Use ParrelSync for local multiplayer testing
5. **Input Debugging**: Use `Assets/_Project/Scripts/Debug/MouseRayInteractorDebug.cs` for mouse-based VR testing

## Build Configuration

- **Platform**: Android (Meta Quest)
- **Architecture**: ARM64
- **Scripting Backend**: IL2CPP
- **API Level**: Minimum Android 10 (API 29)
- **Texture Compression**: ASTC
- **Keystore**: Located in `KeyStore/user.keystore`