# Sistema de Sincronización de Puzzles Multijugador

Este sistema proporciona sincronización completa de puzzles para el proyecto MetaAvatarsVR usando Photon Fusion.

## 📋 Características

- **Host-Authoritative**: Todas las validaciones ocurren en el host
- **Modular**: Componentes reutilizables para diferentes tipos de puzzles
- **Anti-Cheat**: Sistema de validación integrado
- **Randomización Sincronizada**: Objetos aleatorios idénticos para todos los jugadores
- **Estado Persistente**: Progreso compartido entre jugadores
- **VR Optimizado**: Compatible con XR Interaction Toolkit

## 🏗️ Arquitectura

### Capa 1: Foundation
- **NetworkedPuzzleManager**: Gestor central de estado de puzzles
- **NetworkedPuzzleValidator**: Sistema de validación anti-cheat
- **NetworkedRandomizer**: Sincronización de elementos aleatorios

### Capa 2: Componentes Base
- **NetworkedInteractable**: Base para objetos interactuables
- **NetworkedDoor**: Puertas que se abren al completar puzzles

### Capa 3: Puzzles Específicos
- **NetworkedLever** + **NetworkedLeverPuzzle**: Puzzle de palancas
- **NetworkedPiano** + **NetworkedPianoKey**: Puzzle de piano

## 🚀 Instalación Rápida

1. **Agregar el Setup Script** a tu escena:
   ```csharp
   // Crea un GameObject vacío y agrégale PuzzleSyncSetup
   GameObject setupGO = new GameObject("PuzzleSync Setup");
   setupGO.AddComponent<PuzzleSyncSetup>();
   ```

2. **Ejecutar Setup Automático**:
   - Selecciona el GameObject con `PuzzleSyncSetup`
   - En el Inspector, haz clic en "Setup Puzzle Sync System"
   - O usa el menú contextual: Right-click → "Setup Puzzle Sync System"

3. **Convertir Puzzles Existentes**:
   - En el Inspector de `PuzzleSyncSetup`, haz clic en "Convert Existing Puzzles"
   - Esto convertirá automáticamente `Levers.cs`, `Piano.cs`, y `spawnRandomLogic.cs`

## 🎮 Uso Manual

### Configurar NetworkedPuzzleManager

```csharp
// En tu escena, crea un GameObject con:
GameObject managerGO = new GameObject("PuzzleManager");
managerGO.AddComponent<NetworkObject>();
var puzzleManager = managerGO.AddComponent<NetworkedPuzzleManager>();
```

### Configurar Puzzle de Palancas

```csharp
// GameObject para el puzzle completo
GameObject puzzleGO = new GameObject("LeverPuzzle");
puzzleGO.AddComponent<NetworkObject>();
var leverPuzzle = puzzleGO.AddComponent<NetworkedLeverPuzzle>();

// Para cada palanca
GameObject leverGO = new GameObject("Lever");
leverGO.AddComponent<NetworkObject>();
leverGO.AddComponent<XRSimpleInteractable>();
var lever = leverGO.AddComponent<NetworkedLever>();
```

### Configurar Puzzle de Piano

```csharp
// GameObject para el piano
GameObject pianoGO = new GameObject("Piano");
pianoGO.AddComponent<NetworkObject>();
var piano = pianoGO.AddComponent<NetworkedPiano>();

// Para cada tecla
GameObject keyGO = new GameObject("PianoKey");
keyGO.AddComponent<NetworkObject>();
keyGO.AddComponent<XRSimpleInteractable>();
var key = keyGO.AddComponent<NetworkedPianoKey>();
```

### Configurar Puertas

```csharp
GameObject doorGO = new GameObject("Door");
doorGO.AddComponent<NetworkObject>();
var door = doorGO.AddComponent<NetworkedDoor>();

// Configurar qué puzzle desbloquea la puerta
door._requiredPuzzleId = 0; // ID del puzzle
door._requiresPuzzleCompletion = true;
```

### Configurar Randomización

```csharp
GameObject randomizerGO = new GameObject("Randomizer");
randomizerGO.AddComponent<NetworkObject>();
var randomizer = randomizerGO.AddComponent<NetworkedRandomizer>();

// Configurar objetos y posiciones
randomizer._objectPrefabs = myPrefabs;
randomizer._spawnPositions = myPositions;
```

## ⚙️ Configuración

### NetworkedPuzzleManager Settings
- `_totalPuzzles`: Número total de puzzles en la escena
- `_requireSequentialCompletion`: Si requiere completar puzzles en orden
- `_randomizationSeed`: Seed para sincronización aleatoria

### NetworkedPuzzleValidator Settings
- `_enableValidation`: Habilitar validación anti-cheat
- `_maxInteractionRate`: Máximo de interacciones por segundo
- `_maxFailureAttempts`: Intentos fallidos antes del timeout

### NetworkedRandomizer Settings
- `_randomizeOnStart`: Randomizar al iniciar
- `_allowDuplicates`: Permitir objetos duplicados
- `_maxActiveObjects`: Límite de objetos activos

## 🔄 Flujo de Trabajo

1. **Inicio de Sesión**:
   - El host genera seed de randomización
   - Se inicializan los puzzles
   - Los objetos se randomiza de forma determinística

2. **Interacción del Jugador**:
   - Validación local inmediata (feedback visual)
   - RPC al host para validación autoritativa
   - Sincronización del estado a todos los clientes

3. **Completar Puzzle**:
   - Validación final en el host
   - Actualización del PuzzleManager
   - Activación de eventos (abrir puertas, etc.)

## 🐛 Debugging

### Logs Importantes
- `[NetworkedPuzzleManager]`: Estado de puzzles
- `[NetworkedPuzzleValidator]`: Violaciones de validación
- `[NetworkedRandomizer]`: Estado de randomización
- `[NetworkedDoor]`: Estado de puertas

### Comandos de Debug
```csharp
// Completar puzzle manualmente
NetworkedPuzzleManager.Instance.RPC_CompletePuzzle(puzzleId);

// Resetear puzzle
NetworkedPuzzleManager.Instance.RPC_ResetPuzzle(puzzleId);

// Abrir puerta forzadamente
door.ForceOpen();
```

## 🔒 Seguridad

El sistema incluye validaciones para prevenir:
- **Rate Limiting**: Spam de interacciones
- **Distance Checking**: Interacciones remotas
- **Sequence Validation**: Completar puzzles demasiado rápido
- **Timeout System**: Jugadores problemáticos

## 📊 Eventos Disponibles

### PuzzleManager Events
- `OnPuzzleStarted(int puzzleId)`
- `OnPuzzleCompleted(int puzzleId)`
- `OnPuzzleFailed(int puzzleId)`
- `OnAllPuzzlesCompleted()`

### Door Events
- `OnDoorOpening()`
- `OnDoorOpened()`
- `OnDoorUnlocked()`

### Interactable Events
- `OnNetworkActivate(PlayerRef player)`
- `OnStateChanged(InteractableState state)`

## 🛠️ Personalización

Para crear nuevos tipos de puzzles:

1. Heredar de `NetworkedInteractable` o `NetworkBehaviour`
2. Implementar lógica específica del puzzle
3. Usar RPCs para sincronización
4. Integrar con `NetworkedPuzzleManager`

Ejemplo:
```csharp
public class MyCustomPuzzle : NetworkedInteractable
{
    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_CustomAction(RpcInfo info = default)
    {
        // Lógica del puzzle aquí
        if (NetworkedPuzzleManager.Instance != null)
        {
            NetworkedPuzzleManager.Instance.RPC_UpdatePuzzleProgress(puzzleId, newProgress);
        }
    }
}
```

## 🔧 Troubleshooting

### Problemas Comunes

1. **"NetworkObject component missing"**
   - Asegúrate de que todos los componentes networkeados tengan `NetworkObject`

2. **"Randomization not syncing"**
   - Verifica que el `NetworkedRandomizer` tenga autoridad de estado
   - Confirma que el seed se esté generando correctamente

3. **"Players can't interact"**
   - Revisa que `NetworkedPuzzleValidator` no esté bloqueando
   - Verifica configuración de XR Interaction Toolkit

4. **"Doors not opening"**
   - Confirma que `_requiredPuzzleId` coincida con el ID del puzzle
   - Verifica que `NetworkedDoor` esté registrado con `NetworkedPuzzleManager`

### Performance Tips

- Usa `[Networked, OnChanged]` solo cuando sea necesario
- Limita la frecuencia de RPCs
- Usa `NetworkArray` para datos estructurados
- Considera usar `TickAligned` para eventos críticos

## 📞 Soporte

Para problemas específicos:
1. Revisa los logs de console
2. Verifica configuración de Photon Fusion
3. Confirma que todos los NetworkObjects estén registrados
4. Usa el PuzzleSyncSetup para validación automática

---

**¡El sistema está listo para usar! 🎉**

Integra estos componentes en tu escena y tendrás sincronización completa de puzzles multijugador con todas las características de seguridad y validación incluidas.