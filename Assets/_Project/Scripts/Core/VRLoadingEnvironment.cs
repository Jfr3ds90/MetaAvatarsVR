using UnityEngine;
using TMPro;
using DG.Tweening;
using System.Collections;

namespace HackMonkeys.Core
{
    /// <summary>
    /// Ambiente de carga 3D inmersivo para VR
    /// Reemplaza las pantallas de carga negras con un entorno interactivo
    /// </summary>
    public class VRLoadingEnvironment : MonoBehaviour
    {
        [Header("Environment Components")]
        [SerializeField] private Transform environmentRoot;
        [SerializeField] private Transform playerPosition;
        [SerializeField] private GameObject[] decorativeElements;
        
        [Header("Progress Display")]
        [SerializeField] private Transform progressRing;
        [SerializeField] private TextMeshProUGUI progressText;
        [SerializeField] private TextMeshProUGUI loadingMessageText;
        [SerializeField] private Transform progressFillMesh;
        
        [Header("Animation Settings")]
        [SerializeField] private float rotationSpeed = 10f;
        [SerializeField] private float pulseSpeed = 2f;
        [SerializeField] private AnimationCurve pulseCurve = AnimationCurve.EaseInOut(0, 0.9f, 1, 1.1f);
        
        [Header("Tips System")]
        [SerializeField] private TextMeshProUGUI tipsText;
        [SerializeField] private string[] loadingTips = new string[]
        {
            "Look around to explore the environment",
            "Voice chat uses spatial audio - get closer to hear better",
            "Hold grip button to grab objects",
            "Press B to open the menu anytime"
        };
        [SerializeField] private float tipChangeInterval = 4f;
        
        [Header("Particle Effects")]
        [SerializeField] private ParticleSystem ambientParticles;
        [SerializeField] private ParticleSystem progressParticles;
        
        private float _currentProgress = 0f;
        private Coroutine _tipsCoroutine;
        private Coroutine _animationCoroutine;
        
        private void Awake()
        {
            // Posicionar en el origen del jugador VR
            if (playerPosition != null && Camera.main != null)
            {
                transform.position = Camera.main.transform.parent.position;
            }
        }
        
        /// <summary>
        /// Mostrar el ambiente de carga con animación
        /// </summary>
        public void Show()
        {
            gameObject.SetActive(true);
            
            // Resetear estado
            _currentProgress = 0f;
            UpdateProgressDisplay();
            
            // Animar entrada
            if (environmentRoot != null)
            {
                environmentRoot.localScale = Vector3.zero;
                environmentRoot.DOScale(Vector3.one, 0.8f)
                    .SetEase(Ease.OutBack);
            }
            
            // Iniciar animaciones
            _animationCoroutine = StartCoroutine(AnimateEnvironment());
            _tipsCoroutine = StartCoroutine(CycleTips());
            
            // Iniciar partículas
            if (ambientParticles != null)
                ambientParticles.Play();
            
            // Mensaje inicial
            SetLoadingMessage("Preparing your adventure...");
        }
        
        /// <summary>
        /// Ocultar el ambiente de carga
        /// </summary>
        public void Hide()
        {
            // Detener corrutinas
            if (_tipsCoroutine != null)
            {
                StopCoroutine(_tipsCoroutine);
                _tipsCoroutine = null;
            }
            
            if (_animationCoroutine != null)
            {
                StopCoroutine(_animationCoroutine);
                _animationCoroutine = null;
            }
            
            // Animar salida
            if (environmentRoot != null)
            {
                environmentRoot.DOScale(Vector3.zero, 0.5f)
                    .SetEase(Ease.InBack)
                    .OnComplete(() => gameObject.SetActive(false));
            }
            else
            {
                gameObject.SetActive(false);
            }
        }
        
        /// <summary>
        /// Actualizar el progreso de carga (0-1)
        /// </summary>
        public void SetProgress(float progress)
        {
            _currentProgress = Mathf.Clamp01(progress);
            UpdateProgressDisplay();
            
            // Efectos especiales en hitos
            if (_currentProgress >= 0.25f && _currentProgress < 0.26f)
            {
                TriggerMilestoneEffect();
                SetLoadingMessage("Loading game assets...");
            }
            else if (_currentProgress >= 0.5f && _currentProgress < 0.51f)
            {
                TriggerMilestoneEffect();
                SetLoadingMessage("Connecting to match...");
            }
            else if (_currentProgress >= 0.75f && _currentProgress < 0.76f)
            {
                TriggerMilestoneEffect();
                SetLoadingMessage("Initializing world...");
            }
            else if (_currentProgress >= 0.95f)
            {
                SetLoadingMessage("Ready!");
            }
        }
        
        /// <summary>
        /// Establecer mensaje de carga personalizado
        /// </summary>
        public void SetLoadingMessage(string message)
        {
            if (loadingMessageText != null)
            {
                loadingMessageText.text = message;
                
                // Animación de aparición
                loadingMessageText.transform.DOKill();
                loadingMessageText.transform.localScale = Vector3.one * 0.8f;
                loadingMessageText.transform.DOScale(Vector3.one, 0.3f)
                    .SetEase(Ease.OutBack);
            }
        }
        
        private void UpdateProgressDisplay()
        {
            // Actualizar texto
            if (progressText != null)
            {
                progressText.text = $"{Mathf.RoundToInt(_currentProgress * 100)}%";
            }
            
            // Actualizar anillo de progreso
            if (progressRing != null)
            {
                progressRing.DORotate(new Vector3(0, 0, -360 * _currentProgress), 0.5f, RotateMode.FastBeyond360);
            }
            
            // Actualizar mesh de relleno
            if (progressFillMesh != null)
            {
                progressFillMesh.localScale = new Vector3(_currentProgress, 1, 1);
            }
        }
        
        private IEnumerator AnimateEnvironment()
        {
            float time = 0;
            
            while (true)
            {
                time += Time.deltaTime;
                
                // Rotar elementos decorativos
                foreach (var element in decorativeElements)
                {
                    if (element != null)
                    {
                        element.transform.Rotate(Vector3.up, rotationSpeed * Time.deltaTime);
                    }
                }
                
                // Pulso del anillo de progreso
                if (progressRing != null)
                {
                    float scale = pulseCurve.Evaluate(Mathf.PingPong(time * pulseSpeed, 1f));
                    progressRing.localScale = Vector3.one * scale;
                }
                
                yield return null;
            }
        }
        
        private IEnumerator CycleTips()
        {
            if (tipsText == null || loadingTips.Length == 0)
                yield break;
            
            int currentTipIndex = 0;
            
            while (true)
            {
                // Mostrar tip
                ShowTip(loadingTips[currentTipIndex]);
                
                // Esperar
                yield return new WaitForSeconds(tipChangeInterval);
                
                // Siguiente tip
                currentTipIndex = (currentTipIndex + 1) % loadingTips.Length;
            }
        }
        
        private void ShowTip(string tip)
        {
            if (tipsText == null) return;
            
            // Fade out
            tipsText.DOFade(0, 0.3f).OnComplete(() =>
            {
                tipsText.text = $"{tip}";
                // Fade in
                tipsText.DOFade(1, 0.3f);
            });
        }
        
        private void TriggerMilestoneEffect()
        {
            // Efecto visual al alcanzar hitos
            if (progressParticles != null)
            {
                progressParticles.Emit(20);
            }
            
            // Haptic feedback
            OVRInput.SetControllerVibration(1, 0.3f, OVRInput.Controller.Touch);
            DOVirtual.DelayedCall(0.1f, () => 
                OVRInput.SetControllerVibration(0, 0, OVRInput.Controller.Touch));
            
            // Animación de celebración
            if (progressRing != null)
            {
                progressRing.DOPunchScale(Vector3.one * 0.2f, 0.5f, 5, 0.5f);
            }
        }
        
        private void OnDestroy()
        {
            // Limpiar tweens
            DOTween.Kill(transform);
            DOTween.Kill(progressRing);
            DOTween.Kill(loadingMessageText);
            DOTween.Kill(tipsText);
        }
    }
}