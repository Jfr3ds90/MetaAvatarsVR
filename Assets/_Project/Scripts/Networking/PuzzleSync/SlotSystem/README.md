# Sistema Genérico de Puzzles con Slots para VR Networking

## Descripción General

Este sistema proporciona una arquitectura modular y reutilizable para crear puzzles basados en colocar objetos en slots específicos. Está diseñado para funcionar con Photon Fusion en arquitectura Shared Mode y es compatible con el sistema FusionVRGrabbable.

## Arquitectura

### Componentes Base

#### 1. **ISlottable** (Interface)
Define el contrato que deben cumplir todos los objetos que pueden ser colocados en slots.

#### 2. **NetworkedSlottableItem** (Clase Base Abstracta)
- Hereda de `NetworkBehaviour`
- Requiere `FusionVRGrabbable` para interacción VR
- Maneja la lógica de detección de slots cercanos
- Sincroniza el estado a través de la red
- Proporciona eventos y callbacks personalizables

#### 3. **NetworkedSlot** (Clase Base)
- Representa un slot donde se pueden colocar items
- Valida si un item es correcto para ese slot
- Proporciona feedback visual y auditivo
- Sincroniza el estado de ocupación

#### 4. **NetworkedSlotPuzzleController** (Controlador Principal)
- Gestiona la lógica general del puzzle
- Controla los estados del puzzle
- Valida condiciones de completitud
- Integración con `NetworkedPuzzleManager`

## Cómo Usar el Sistema

### 1. Crear un Nuevo Tipo de Item

```csharp
using MetaAvatarsVR.Networking.PuzzleSync.SlotSystem;

public class MyCustomItem : NetworkedSlottableItem
{
    [Header("Custom Configuration")]
    [SerializeField] private string _itemType;
    
    protected override void OnPlacedCustom(int slotIndex, bool isCorrect)
    {
        // Lógica personalizada cuando se coloca
        Debug.Log($"Item {_itemType} colocado");
    }
}
```

### 2. Crear un Slot Personalizado (Opcional)

```csharp
public class MyCustomSlot : NetworkedSlot
{
    [Header("Custom Slot")]
    [SerializeField] private string _acceptedType;
    
    protected override void OnItemPlacedCustom(int itemId, bool isCorrect)
    {
        // Efectos especiales, animaciones, etc.
    }
}
```

### 3. Implementar un Controlador de Puzzle

```csharp
public class MyPuzzle : NetworkedSlotPuzzleController
{
    protected override void GenerateExpectedPattern()
    {
        _expectedPattern = new List<int>();
        
        // Define qué item va en cada slot
        for (int i = 0; i < _slots.Length; i++)
        {
            _expectedPattern.Add(i);
        }
    }
    
    protected override bool ValidateFinalConfiguration()
    {
        // Lógica de validación personalizada
        return base.ValidateFinalConfiguration();
    }
}
```

## Configuración en Unity

### Setup Básico

1. **GameObject del Puzzle Controller:**
   - Añadir componente heredado de `NetworkedSlotPuzzleController`
   - Añadir `NetworkObject` con `AllowStateAuthorityOverride = true`
   - Configurar referencias a slots e items

2. **Items (Objetos a colocar):**
   - Añadir `NetworkObject`
   - Añadir `NetworkRigidbody3D`
   - Añadir `FusionVRGrabbable`
   - Añadir componente heredado de `NetworkedSlottableItem`
   - Configurar layer y colliders para detección

3. **Slots:**
   - Añadir `NetworkObject`
   - Añadir componente heredado de `NetworkedSlot`
   - Configurar collider en layer detectable por items
   - Configurar el anchor point para posicionamiento

### Configuración del SlotPuzzleConfig

```csharp
SlotPuzzleConfig:
- puzzleId: ID único del puzzle
- requireAllSlotsFilled: ¿Todos los slots deben llenarse?
- requireCorrectOrder: ¿El orden importa?
- allowPartialCompletion: ¿Permitir completar parcialmente?
- validationDelay: Tiempo antes de validar
- maxAttempts: Intentos máximos (-1 = ilimitado)
- autoReset: ¿Reset automático tras fallo?
- resetDelay: Tiempo antes del reset
```

## Estados del Puzzle

- **NotStarted**: Estado inicial
- **WaitingForItems**: Esperando que se coloquen items
- **InProgress**: Items siendo colocados
- **ValidationPhase**: Validando configuración
- **Completed**: Puzzle completado exitosamente
- **Failed**: Puzzle fallido

## Eventos Disponibles

### En el Controller:
- `OnPuzzleStarted`
- `OnStateChanged<SlotPuzzleState>`
- `OnProgressUpdated<int, int>`
- `OnPuzzleCompleted`
- `OnPuzzleFailed`
- `OnPuzzleReset`

### En Items:
- `OnItemPlaced<int>`
- `OnItemRemoved`
- `OnCorrectPlacement`
- `OnIncorrectPlacement`

### En Slots:
- `OnItemPlaced<int>`
- `OnItemRemoved<int>`
- `OnCorrectPlacement`
- `OnIncorrectPlacement`

## Ejemplos de Implementación

### 1. Puzzle de Secuencia Simple
Ver: `SimpleSequencePuzzle.cs`
- Items deben colocarse en orden específico
- Secuencia puede ser aleatoria o fija

### 2. Puzzle de Notas Musicales
Ver: `NetworkedMusicalNotesPuzzleV2.cs`
- Muestra patrón visual inicial
- Requiere colocar notas en orden correcto
- Fase adicional con piano

### 3. Puzzle de Colores
Ver: `ColorMatchingPuzzle.cs`
- Emparejar items por color
- Sistema de hints visuales

## Mejores Prácticas

1. **Validación de Componentes**: Siempre validar en `Awake()` que todos los componentes necesarios estén presentes.

2. **Feedback Visual**: Proporcionar feedback claro cuando un item es colocado correcta o incorrectamente.

3. **Sincronización de Red**: Usar RPCs para notificar cambios importantes a todos los clientes.

4. **Optimización**: Limitar la frecuencia de validaciones y usar `TickTimer` para delays.

5. **Debugging**: Usar el modo debug en el editor para testing rápido.

## Integración con Sistemas Existentes

- **NetworkedPuzzleManager**: El controller se registra automáticamente
- **NetworkedPuzzleValidator**: Validación anti-cheat integrada
- **FusionVRGrabbable**: Totalmente compatible con el sistema de grab VR

## Troubleshooting

### Items no detectan slots
- Verificar layers y collision matrix
- Ajustar `_snapDistance` en items
- Verificar que slots tengan colliders

### Estado no se sincroniza
- Verificar `HasStateAuthority` en operaciones críticas
- Asegurar que `NetworkObject` tenga `AllowStateAuthorityOverride`

### Items no se colocan correctamente
- Verificar `_itemAnchor` en slots
- Ajustar configuración de snap (`_snapToCenter`, `_lockRotation`)