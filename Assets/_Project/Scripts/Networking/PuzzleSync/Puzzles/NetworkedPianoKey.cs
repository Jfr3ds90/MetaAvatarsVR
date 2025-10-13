using System.Collections;
using Fusion;
using UnityEngine;
using UnityEngine.Events;
using Oculus.Interaction;
using Oculus.Interaction.Surfaces;

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
        [SerializeField] private Material _correctMaterial;
        [SerializeField] private Material _errorMaterial;
        
        [Header("Haptics")]
        [SerializeField] private float _hapticAmplitude = 0.5f;
        [SerializeField] private float _hapticDuration = 0.1f;
        
        [Header("Network State")]
        [Networked] public int KeyIndex { get; set; }
        [Networked] public NetworkBool IsPressed { get; set; }
        [Networked] public TickTimer PressedTimer { get; set; }
        
        [Header("Events")]
        public UnityEvent<string> OnKeyPressed = new UnityEvent<string>();
        
        // Components
        private PokeInteractable _pokeInteractable;
        private AudioSource _audioSource;
        private Vector3 _originalPosition;
        private Quaternion _originalRotation;
        private Coroutine _animationCoroutine;
        
        private void Awake()
        {
            SetupPokeInteraction();
            SetupAudio();
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
                    //pokeSurface.NormalLocal = Vector3.up;
                }
                
                //_pokeInteractable.Surface = pokeSurface;
            }
            
            // Configurar visual de poke
            var pokeVisual = GetComponent<PokeInteractableVisual>();
            if (pokeVisual == null)
            {
                pokeVisual = gameObject.AddComponent<PokeInteractableVisual>();
            }
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
            // Resetear tecla automáticamente después del timer
            if (IsPressed && PressedTimer.Expired(Runner))
            {
                IsPressed = false;
                RPC_UpdateKeyVisual(false);
            }
        }
        
        private void OnPokeEvent(PointerEvent evt)
        {
            if (evt.Type == PointerEventType.Select && !IsPressed)
            {
                PressKey(evt.Pose);
            }
        }
        
        private void PressKey(Pose interactionPose)
        {
            // Enviar RPC con información del jugador que presionó
            RPC_OnKeyPress(Runner.LocalPlayer, interactionPose.position);
        }
        
        [Rpc(RpcSources.All, RpcTargets.All)]
        private void RPC_OnKeyPress(PlayerRef player, Vector3 interactionPoint, RpcInfo info = default)
        {
            if (IsPressed) return;
            
            // Actualizar estado networked
            if (HasStateAuthority || Runner.IsSharedModeMasterClient)
            {
                IsPressed = true;
                PressedTimer = TickTimer.CreateFromSeconds(Runner, 0.5f);
            }
            
            // Efectos locales para todos los clientes
            PlayKeyAnimation(true);
            PlaySound();
            TriggerHaptics(player);
            
            // Notificar al piano
            OnKeyPressed?.Invoke(_noteName);
            
            Debug.Log($"[PianoKey] {_noteName} pressed by Player {player}");
        }
        
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
            if (_animationCoroutine != null)
                StopCoroutine(_animationCoroutine);
            _animationCoroutine = StartCoroutine(FlashMaterial(_correctMaterial, 0.5f));
        }
        
        public void ShowErrorFeedback()
        {
            if (_animationCoroutine != null)
                StopCoroutine(_animationCoroutine);
            _animationCoroutine = StartCoroutine(FlashMaterial(_errorMaterial, 0.3f));
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
                
            Material targetMat = pressed ? _pressedMaterial : _defaultMaterial;
            
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
            
            if (_keyRenderer != null && targetMat != null)
                _keyRenderer.material = targetMat;
        }
        
        private IEnumerator FlashMaterial(Material flashMaterial, float duration)
        {
            if (_keyRenderer == null || flashMaterial == null) yield break;
            
            Material originalMat = _keyRenderer.material;
            _keyRenderer.material = flashMaterial;
            
            yield return new WaitForSeconds(duration);
            
            _keyRenderer.material = _defaultMaterial ?? originalMat;
        }
        
        private void PlaySound()
        {
            if (_noteSound != null && _audioSource != null)
            {
                _audioSource.PlayOneShot(_noteSound);
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
        }
    }
}