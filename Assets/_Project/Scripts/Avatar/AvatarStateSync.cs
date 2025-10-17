using System;
using Fusion;
using Oculus.Avatar2;
using UnityEngine;
using HackMonkeys.Debugging;
using LogLevel = HackMonkeys.Debugging.LogLevel;

public class AvatarStateSync : NetworkBehaviour
{
    [SerializeField] private AvatarEntityState _avatarEntityState;
    [Networked] public UInt64 OculusID { get; set; }
    [Networked] private uint AvatarDataCount { get; set; }

    private const int AVATAR_DATA_SIZE = 1200;
    
    [Networked, Capacity(AVATAR_DATA_SIZE)] private NetworkArray<byte> AvatarData { get; }

    // Buffer pre-asignado para reducir la generación de basura
    private byte[] _byteArray = new byte[AVATAR_DATA_SIZE];
    private byte[] _tempBuffer = new byte[AVATAR_DATA_SIZE];
    
    private ChangeDetector _changeDetector;

    public override void Spawned()
    {
        AdvancedDebugSystem.Log($"[AvatarStateSync] Spawned - InputAuthority: {Object.InputAuthority}, LocalPlayer: {Runner.LocalPlayer}", LogCategory.Avatar, LogLevel.Debug);
        
        _changeDetector = GetChangeDetector(ChangeDetector.Source.SimulationState);

        // En Shared Mode, cada jugador con InputAuthority establece su propio OculusID
        if (Object.InputAuthority == Runner.LocalPlayer)
        {
            OculusID = UserEntitlement.OculusID;
            AdvancedDebugSystem.Log($"[AvatarStateSync] ✅ Local avatar - Setting OculusID: {OculusID}", LogCategory.Avatar, LogLevel.Debug);
        }
        else
        {
            AdvancedDebugSystem.Log($"[AvatarStateSync] 👥 Remote avatar - Will wait for OculusID from network", LogCategory.Avatar, LogLevel.Debug);
            ApplyAvatarData();
        }
    }

    public override void Render()
    {
        base.Render();

        // Detección de cambios en propiedades de red
        foreach (var changedProperty in _changeDetector.DetectChanges(this))
        {
            if (changedProperty == nameof(AvatarData))
            {
                ApplyAvatarData();
            }
        }
    }

    // Registra datos del avatar local para sincronización
    public void RecordAvatarState(OvrAvatarEntity.StreamLOD streamLOD)
    {
        // En Shared Mode, solo el jugador local registra sus propios datos
        if (Object.InputAuthority != Runner.LocalPlayer) return;
        
        // Registra los datos del avatar en el buffer
        AvatarDataCount = _avatarEntityState.RecordStreamData_AutoBuffer(streamLOD, ref _byteArray);
        
        // Actualiza los datos en la propiedad de red
        AvatarData.CopyFrom(_byteArray, 0, (int)AvatarDataCount);
    }

    // Procesa datos del avatar recibidos de la red
    private void ApplyAvatarData()
    {
        // En Shared Mode, aplicamos datos de avatares remotos (no locales)
        bool isLocalAvatar = Object.InputAuthority == Runner.LocalPlayer;
        
        if (!isLocalAvatar && AvatarDataCount > 0)
        {
            // Copia datos de la NetworkArray al buffer temporal
            for (int i = 0; i < AvatarDataCount; i++)
            {
                _tempBuffer[i] = AvatarData[i];
            }

            // Añade los datos al sistema de buffer del avatar
            if (_avatarEntityState != null)
            {
                _avatarEntityState.AddToDataBuffer(_tempBuffer, (int)AvatarDataCount);
            }
        }
    }
}