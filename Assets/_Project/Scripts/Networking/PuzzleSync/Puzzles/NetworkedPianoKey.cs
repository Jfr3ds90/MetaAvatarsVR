using System.Collections;
using Fusion;
using UnityEngine;
using UnityEngine.Events;
using Oculus.Interaction;
using Oculus.Interaction.Surfaces;
using Cysharp.Threading.Tasks;
using System.Threading;

namespace MetaAvatarsVR.Networking.PuzzleSync.Puzzles
{
    public class NetworkedPianoKey : NetworkBehaviour
    {
        [Header("Key Configuration")]
        [SerializeField] private int _keyIndex = 0;
        [SerializeField] private string _noteName = "Do";
        [SerializeField] private AudioClip _noteSound;
        [SerializeField] private float _keyDepth = 0.02f;
        
        [Header("Visual")]
        [SerializeField] private Transform _keyTransform;
        [SerializeField] private MeshRenderer _keyRenderer;
        [SerializeField] private Material _defaultMaterial;
        [SerializeField] private Material _pressedMaterial;
        [SerializeField] private Material _luminousMaterial; // Nuevo material luminoso
        [SerializeField] private Material _correctMaterial;
        [SerializeField] private Material _errorMaterial;
        
        [Header("Haptics")]
        [SerializeField] private float _hapticAmplitude = 0.5f;
        [SerializeField] private float _hapticDuration = 0.1f;
        
        [Header("Network State")]
        [Networked] public int KeyIndex { get; set; }
        [Networked] public NetworkBool IsPressed { get; set; }
        [Networked] public TickTimer PressedTimer { get; set; }
        
        [Header("Particle Effects")]
        [SerializeField] private float _particleDuration = 2f;
        [SerializeField] private bool _useParticleEffects = true;
        
        [Header("Events")]
        public UnityEvent<string> OnKeyPressed = new UnityEvent<string>();
        
        // Components
        private PokeInteractable _pokeInteractable;
        private AudioSource _audioSource;
        private ParticleSystem _noteParticleSystem;
        private Vector3 _originalPosition;
        private Quaternion _originalRotation;
        private Coroutine _animationCoroutine;
        private CancellationTokenSource _materialChangeCts;
        
        private void Awake()
        {
            SetupPokeInteraction();
            SetupAudio();
            SetupParticleSystem();
            CacheTransformData();
        }
        
        private void SetupPokeInteraction()
        {
            _pokeInteractable = GetComponent<PokeInteractable>();
            if (_pokeInteractable == null)
            {
                _pokeInteractable = gameObject.AddComponent<PokeInteractable>();
        
                // Configurar superficie de poke para piano
                var pokeSurface = GetComponent<PlaneSurface>();
                if (pokeSurface == null)
                {
                    pokeSurface = gameObject.AddComponent<PlaneSurface>();
                    // Usar Facing en lugar de NormalLocal
                    pokeSurface.Facing = PlaneSurface.NormalFacing.Forward; // Normal apunta hacia +Z
                    pokeSurface.DoubleSided = true; // Permitir interacción desde ambos lados
                }
        
                // Crear un SurfacePatch que envuelva la superficie
                var surfacePatch = GetComponent<BoundsClipper>();
                if (surfacePatch == null)
                {
                    surfacePatch = gameObject.AddComponent<BoundsClipper>();
                    // El BoundsClipper actúa como ISurfacePatch
                }
        
                // Usar InjectSurfacePatch en lugar de asignar Surface directamente
                //_pokeInteractable.InjectSurfacePatch(surfacePatch);
            }
    
            // Configurar parámetros de interacción
            _pokeInteractable.EnterHoverNormal = 0.05f;  // 5cm para empezar hover
            _pokeInteractable.ExitHoverNormal = 0.08f;   // 8cm para salir de hover
            _pokeInteractable.CancelSelectNormal = 0.02f; // 2cm de profundidad máxima
        }
        
        private void SetupAudio()
        {
            _audioSource = GetComponent<AudioSource>();
            if (_audioSource == null)
            {
                _audioSource = gameObject.AddComponent<AudioSource>();
                _audioSource.spatialBlend = 1f; // 3D sound
                _audioSource.rolloffMode = AudioRolloffMode.Linear;
                _audioSource.maxDistance = 10f;
            }
        }
        
        private void SetupParticleSystem()
        {
            if (!_useParticleEffects) return;
            
            // Buscar el sistema de partículas en los hijos (PS_NotaMusical)
            _noteParticleSystem = GetComponentInChildren<ParticleSystem>();
            
            if (_noteParticleSystem == null)
            {
                Debug.LogWarning($"[NetworkedPianoKey] No ParticleSystem found in children of {gameObject.name}. Particle effects will be disabled.");
                _useParticleEffects = false;
            }
            else
            {
                // Configurar el sistema de partículas
                var main = _noteParticleSystem.main;
                main.playOnAwake = false;
                main.loop = false;
                main.duration = _particleDuration;
                
                // Asegurarse de que esté detenido al inicio
                _noteParticleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                
                Debug.Log($"[NetworkedPianoKey] ParticleSystem found and configured for {_noteName}");
            }
        }
        
        private void CacheTransformData()
        {
            if (_keyTransform == null)
                _keyTransform = transform;
                
            _originalPosition = _keyTransform.localPosition;
            _originalRotation = _keyTransform.localRotation;
            
            if (_keyRenderer == null)
                _keyRenderer = GetComponent<MeshRenderer>();
        }
        
        private void Start()
        {
            if (_pokeInteractable != null)
            {
                _pokeInteractable.WhenPointerEventRaised += OnPokeEvent;
            }
        }
        
        public override void Spawned()
        {
            if (HasStateAuthority)
            {
                KeyIndex = _keyIndex;
                IsPressed = false;
            }
        }
        
        public override void FixedUpdateNetwork()
        {
            // Resetear tecla automáticamente después del timer (failsafe)
            if (IsPressed && PressedTimer.Expired(Runner))
            {
                if (HasStateAuthority || Runner.IsSharedModeMasterClient)
                {
                    IsPressed = false;
                }
            }
        }
        
        private void OnPokeEvent(PointerEvent evt)
        {
            if (evt.Type == PointerEventType.Select && !IsPressed)
            {
                PressKey(evt.Pose);
            }
            // No necesitamos detectar Unselect ya que la tecla regresará automáticamente
        }
        
        private void PressKey(Pose interactionPose)
        {
            // Enviar RPC con información del jugador que presionó
            RPC_OnKeyPress(Runner.LocalPlayer, interactionPose.position);
        }
        
        private void ReleaseKey()
        {
            // Ya no es necesario, la liberación se maneja en AnimateKeyPressAsync
        }
        
        [Rpc(RpcSources.All, RpcTargets.All)]
        private void RPC_OnKeyPress(PlayerRef player, Vector3 interactionPoint, RpcInfo info = default)
        {
            if (IsPressed) return;
            
            // Actualizar estado networked
            if (HasStateAuthority || Runner.IsSharedModeMasterClient)
            {
                IsPressed = true;
                PressedTimer = TickTimer.CreateFromSeconds(Runner, 0.5f); // Timer corto para auto-release
            }
            
            // Ejecutar animación completa de presionar y soltar con cambio de material
            AnimateKeyPressAsync().Forget();
            
            // Efectos de sonido y partículas
            PlaySound();
            PlayParticleEffect();
            TriggerHaptics(player);
            
            // IMPORTANTE: Solo el cliente que presionó la tecla debe notificar al piano
            // Esto evita que se procese múltiples veces
            if (player == Runner.LocalPlayer)
            {
                // Notificar al piano solo desde el cliente que presionó la tecla
                OnKeyPressed?.Invoke(_noteName);
                Debug.Log($"[PianoKey] {_noteName} pressed by Player {player} (local)");
            }
            else
            {
                // Los demás clientes solo muestran el log
                Debug.Log($"[PianoKey] {_noteName} pressed by Player {player} (remote)");
            }
        }
        
        // RPC_OnKeyRelease ya no es necesario porque la animación completa se maneja en AnimateKeyPressAsync
        
        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RPC_UpdateKeyVisual(NetworkBool pressed)
        {
            PlayKeyAnimation(pressed);
        }
        
        public void SetKeyData(int index, string note)
        {
            _keyIndex = index;
            _noteName = note;
            KeyIndex = index;
        }
        
        public void ShowCorrectFeedback()
        {
            FlashMaterialAsync(_correctMaterial, 500).Forget();
            
            // También reproducir partículas para feedback correcto
            PlayParticleEffect();
        }
        
        public void ShowErrorFeedback()
        {
            FlashMaterialAsync(_errorMaterial, 300).Forget();
            
            // Opcionalmente, reproducir partículas con color diferente para error
            if (_useParticleEffects && _noteParticleSystem != null)
            {
                // Temporalmente cambiar el color de las partículas a rojo
                var main = _noteParticleSystem.main;
                var originalColor = main.startColor;
                main.startColor = new Color(1f, 0.2f, 0.2f, 1f);
                
                PlayParticleEffect();
                
                // Restaurar color original después
                RestoreParticleColorAsync(originalColor, 500).Forget();
            }
        }
        
        private async UniTaskVoid RestoreParticleColorAsync(ParticleSystem.MinMaxGradient originalColor, int delayMs)
        {
            await UniTask.Delay(delayMs);
            
            if (_noteParticleSystem != null)
            {
                var main = _noteParticleSystem.main;
                main.startColor = originalColor;
            }
        }
        
        private async UniTaskVoid FlashMaterialAsync(Material flashMaterial, int durationMs)
        {
            if (_keyRenderer == null || flashMaterial == null) return;
            
            Material originalMat = _keyRenderer.material;
            _keyRenderer.material = flashMaterial;
            
            await UniTask.Delay(durationMs);
            
            _keyRenderer.material = _defaultMaterial ?? originalMat;
        }
        
        private async UniTaskVoid AnimateKeyPressAsync()
        {
            // Cancelar animación anterior si existe
            _materialChangeCts?.Cancel();
            _materialChangeCts = new CancellationTokenSource();
            
            if (_keyRenderer == null || _keyTransform == null) return;
            
            try
            {
                // 1. Cambiar al material luminoso inmediatamente
                if (_luminousMaterial != null)
                    _keyRenderer.material = _luminousMaterial;
                
                // 2. Animar la tecla hacia abajo
                Vector3 targetPosDown = _originalPosition + Vector3.down * _keyDepth;
                float duration = 0.1f;
                float elapsed = 0f;
                Vector3 startPos = _keyTransform.localPosition;
                
                while (elapsed < duration)
                {
                    elapsed += Time.deltaTime;
                    float t = elapsed / duration;
                    _keyTransform.localPosition = Vector3.Lerp(startPos, targetPosDown, t);
                    await UniTask.Yield(cancellationToken: _materialChangeCts.Token);
                }
                
                _keyTransform.localPosition = targetPosDown;
                
                // 3. Mantener presionado con material luminoso por un momento
                await UniTask.Delay(200, cancellationToken: _materialChangeCts.Token);
                
                // 4. Animar la tecla de vuelta a su posición original
                elapsed = 0f;
                startPos = _keyTransform.localPosition;
                
                while (elapsed < duration)
                {
                    elapsed += Time.deltaTime;
                    float t = elapsed / duration;
                    _keyTransform.localPosition = Vector3.Lerp(startPos, _originalPosition, t);
                    await UniTask.Yield(cancellationToken: _materialChangeCts.Token);
                }
                
                _keyTransform.localPosition = _originalPosition;
                
                // 5. Cambiar de vuelta al material default
                if (_defaultMaterial != null)
                    _keyRenderer.material = _defaultMaterial;
                
                // 6. Resetear el estado IsPressed
                if (HasStateAuthority || Runner.IsSharedModeMasterClient)
                {
                    IsPressed = false;
                }
            }
            catch (System.OperationCanceledException)
            {
                // Animación cancelada, asegurar que vuelva al estado original
                _keyTransform.localPosition = _originalPosition;
                if (_defaultMaterial != null)
                    _keyRenderer.material = _defaultMaterial;
            }
        }
        
        private async UniTaskVoid ChangeMaterialAsync(Material targetMaterial)
        {
            // Cancelar cambio de material anterior si existe
            _materialChangeCts?.Cancel();
            _materialChangeCts = new CancellationTokenSource();
            
            if (_keyRenderer == null || targetMaterial == null) return;
            
            try
            {
                // Cambio inmediato del material
                _keyRenderer.material = targetMaterial;
                
                // Opcional: Agregar efecto de transición suave
                if (targetMaterial == _luminousMaterial)
                {
                    // Efecto de brillo al presionar
                    await UniTask.Delay(100, cancellationToken: _materialChangeCts.Token);
                }
            }
            catch (System.OperationCanceledException)
            {
                // Cambio de material cancelado
            }
        }
        
        private void PlayKeyAnimation(bool pressed)
        {
            if (_animationCoroutine != null)
                StopCoroutine(_animationCoroutine);
                
            _animationCoroutine = StartCoroutine(AnimateKey(pressed));
        }
        
        private IEnumerator AnimateKey(bool pressed)
        {
            Vector3 targetPos = pressed ? 
                _originalPosition + Vector3.down * _keyDepth : 
                _originalPosition;
            
            float duration = 0.1f;
            float elapsed = 0f;
            Vector3 startPos = _keyTransform.localPosition;
            
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                
                _keyTransform.localPosition = Vector3.Lerp(startPos, targetPos, t);
                
                yield return null;
            }
            
            _keyTransform.localPosition = targetPos;
            
            // Material change is now handled by ChangeMaterialAsync
        }
        
        
        private void PlaySound()
        {
            if (_noteSound != null && _audioSource != null)
            {
                _audioSource.PlayOneShot(_noteSound);
            }
        }
        
        private void PlayParticleEffect()
        {
            if (!_useParticleEffects || _noteParticleSystem == null) return;
            
            // Detener cualquier emisión anterior y limpiar partículas
            _noteParticleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            
            // Reproducir el sistema de partículas
            _noteParticleSystem.Play();
            
            Debug.Log($"[NetworkedPianoKey] Playing particle effect for {_noteName}");
            
            // Opcional: Detener automáticamente después de la duración
            StartCoroutine(StopParticlesAfterDuration());
        }
        
        private IEnumerator StopParticlesAfterDuration()
        {
            yield return new WaitForSeconds(_particleDuration);
            
            if (_noteParticleSystem != null && _noteParticleSystem.isPlaying)
            {
                _noteParticleSystem.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }
        }
        
        private void TriggerHaptics(PlayerRef player)
        {
            if (player == Runner.LocalPlayer)
            {
                // Obtener el controlador que hizo la interacción
                var controllers = OVRInput.Controller.RTouch | OVRInput.Controller.LTouch;
                OVRInput.SetControllerVibration(1f, _hapticAmplitude, controllers);
                
                StartCoroutine(StopHaptics());
            }
        }
        
        private IEnumerator StopHaptics()
        {
            yield return new WaitForSeconds(_hapticDuration);
            OVRInput.SetControllerVibration(0, 0);
        }
        
        private void OnDestroy()
        {
            if (_pokeInteractable != null)
            {
                _pokeInteractable.WhenPointerEventRaised -= OnPokeEvent;
            }
            
            // Limpiar el CancellationTokenSource
            _materialChangeCts?.Cancel();
            _materialChangeCts?.Dispose();
        }
    }
}