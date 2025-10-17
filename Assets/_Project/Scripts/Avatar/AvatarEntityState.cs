using System;
using System.Collections;
using System.Collections.Generic;
using Fusion;
using Oculus.Avatar2;
using UnityEngine;
using HackMonkeys.Debugging;
using LogLevel = HackMonkeys.Debugging.LogLevel;

public class AvatarEntityState : OvrAvatarEntity
{
    [SerializeField] private AvatarStateSync _avatarStateSync;

    [SerializeField] private float _captureInterval = 0.5f;  // Intervalo para capturar datos
    [SerializeField] private float _playbackDelay = 0.2f;    // Retraso para compensar latencia
    [SerializeField] private int _maxBufferSize = 6;         // Tamaño máximo del buffer
    [SerializeField] private StreamLOD _streamLOD = StreamLOD.Low;

    [SerializeField] private NetworkObject _networkObject;

    // Buffer para datos recibidos
    private List<byte[]> _dataBuffer = new();
    private float _lastCaptureTime;

    protected override void Awake()
    {
        // Mantener vacío para sobrescribir el Awake del padre
    }

    private void Start()
    {
        ConfigureAvatarEntity();
        base.Awake();
        
        // En Shared Mode, comparamos InputAuthority con LocalPlayer
        NetworkRunner runner = NetworkRunner.GetRunnerForGameObject(gameObject);
        bool isLocalAvatar = runner != null && _networkObject.InputAuthority == runner.LocalPlayer;
        
        SetActiveView(isLocalAvatar
            ? CAPI.ovrAvatar2EntityViewFlags.FirstPerson
            : CAPI.ovrAvatar2EntityViewFlags.ThirdPerson);
            
        AdvancedDebugSystem.Log($"[AvatarEntityState] View set to: {(isLocalAvatar ? "FirstPerson" : "ThirdPerson")}", LogCategory.Avatar, LogLevel.Debug);
        StartCoroutine(LoadAvatarID());
    }

    private void ConfigureAvatarEntity()
    {
        // En Shared Mode, verificamos InputAuthority comparando con LocalPlayer
        NetworkRunner runner = NetworkRunner.GetRunnerForGameObject(gameObject);
        bool isLocalAvatar = runner != null && _networkObject.InputAuthority == runner.LocalPlayer;
        
        if (isLocalAvatar)
        {
            SetIsLocal(true);
            _creationInfo.features = CAPI.ovrAvatar2EntityFeatures.Preset_Default;
            
            var sampleInputManager = OvrAvatarManager.Instance?.gameObject.GetComponent<SampleInputManager>();
            if (sampleInputManager != null)
            {
                SetBodyTracking(sampleInputManager);
            }
            else
            {
                AdvancedDebugSystem.LogWarning("[AvatarEntityState] SampleInputManager not found for local avatar", LogCategory.Avatar);
            }
            
            gameObject.name = $"Local Avatar ({_networkObject.InputAuthority})";
            AdvancedDebugSystem.Log($"[AvatarEntityState] ✅ Configured as LOCAL avatar", LogCategory.Avatar, LogLevel.Debug);
        }
        else
        {
            SetIsLocal(false);
            _creationInfo.features = CAPI.ovrAvatar2EntityFeatures.Preset_Remote;
            gameObject.name = $"Remote Avatar ({_networkObject.InputAuthority})";
            AdvancedDebugSystem.Log($"[AvatarEntityState] 👥 Configured as REMOTE avatar", LogCategory.Avatar, LogLevel.Debug);
        }
    }

    private IEnumerator LoadAvatarID()
    {
        while (_avatarStateSync.OculusID == 0) yield return null;
        
        _userId = _avatarStateSync.OculusID;
        var avatarRequest = OvrAvatarManager.Instance.UserHasAvatarAsync(_userId);
        while (!avatarRequest.IsCompleted) yield return null;
        LoadUser();
    }

    private void LateUpdate()
    {
        // Solo captura datos para el avatar local y si está cargado
        if (!IsLocal || CurrentState != AvatarState.UserAvatar) return;

        // Controla la frecuencia de captura
        if (_captureInterval > Time.time - _lastCaptureTime) return;

        // Captura y registra el estado del avatar
        _avatarStateSync.RecordAvatarState(_streamLOD);
        _lastCaptureTime = Time.time;
    }

    // Método para añadir datos al buffer con copia eficiente
    public void AddToDataBuffer(byte[] data, int length)
    {
        // Si el buffer está lleno, elimina el elemento más antiguo
        if (_dataBuffer.Count >= _maxBufferSize)
        {
            _dataBuffer.RemoveAt(_dataBuffer.Count - 1);
        }
        
        // Crea una copia del buffer para almacenar
        byte[] dataCopy = new byte[length];
        Array.Copy(data, dataCopy, length);
        
        // Añade al buffer
        _dataBuffer.Add(dataCopy);
    }

    private void Update()
    {
        // Solo procesa datos para avatares remotos y si hay datos disponibles
        if (IsLocal || CurrentState != AvatarState.UserAvatar || _dataBuffer.Count <= 0) return;

        // Aplica el primer conjunto de datos del buffer
        ApplyStreamData(_dataBuffer[0]);
        
        // Configura el retraso de reproducción para compensar latencia
        SetPlaybackTimeDelay(_playbackDelay);
        
        // Elimina los datos procesados
        _dataBuffer.RemoveAt(0);
    }
}