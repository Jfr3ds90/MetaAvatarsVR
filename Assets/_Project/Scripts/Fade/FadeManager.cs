using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.UI;
using Cysharp.Threading.Tasks;

namespace FadeSystem
{
    // Enums para configuraciones de fade
    public enum EaseType
    {
        Linear,
        EaseIn,
        EaseOut,
        EaseInOut,
        EaseInQuad,
        EaseOutQuad,
        EaseInOutQuad,
        EaseInCubic,
        EaseOutCubic,
        EaseInOutCubic,
        EaseInSine,
        EaseOutSine,
        EaseInOutSine,
        EaseInExpo,
        EaseOutExpo,
        EaseInOutExpo,
        Custom
    }

    public enum FadeDirection
    {
        In,
        Out,
        InOut,
        OutIn
    }

    // Configuración de fade
    [System.Serializable]
    public class FadeConfig
    {
        public float duration = 1f;
        public EaseType easeType = EaseType.Linear;
        public FadeDirection direction = FadeDirection.In;
        public float delay = 0f;
        public AnimationCurve customCurve = AnimationCurve.Linear(0, 0, 1, 1);
        public bool ignoreTimeScale = false;
        public float targetAlpha = 1f;
        public float startAlpha = 0f;
        
        public FadeConfig() { }
        
        public FadeConfig(float duration, EaseType easeType = EaseType.Linear, FadeDirection direction = FadeDirection.In)
        {
            this.duration = duration;
            this.easeType = easeType;
            this.direction = direction;
        }
    }

    // Clase para encapsular callbacks y acciones encadenadas
    [System.Serializable]
    public class FadeCallbacks
    {
        public Action OnStart;
        public Action OnComplete;
        public Action<float> OnUpdate;
        public Action OnCancel;
        
        // Para encadenar métodos específicos
        public Action ChainedMethod;
        
        public FadeCallbacks() { }
        
        public FadeCallbacks(Action onComplete = null, Action<float> onUpdate = null, Action chainedMethod = null)
        {
            OnComplete = onComplete;
            OnUpdate = onUpdate;
            ChainedMethod = chainedMethod;
        }
        
        // Método para encadenar múltiples acciones
        public FadeCallbacks Chain(Action method)
        {
            ChainedMethod += method;
            return this;
        }
        
        // Método para encadenar con delay
        public FadeCallbacks ChainWithDelay(Action method, float delay)
        {
            ChainedMethod += () => DelayedChain(method, delay).Forget();
            return this;
        }
        
        private async UniTaskVoid DelayedChain(Action method, float delay)
        {
            await UniTask.Delay(TimeSpan.FromSeconds(delay));
            method?.Invoke();
        }
    }

    // Interface para diferentes tipos de objetos que pueden hacer fade
    public interface IFadeable
    {
        void SetAlpha(float alpha);
        float GetAlpha();
        bool IsValid();
    }

    // Implementaciones específicas para diferentes componentes
    public class ImageFadeable : IFadeable
    {
        private Image image;
        
        public ImageFadeable(Image image) => this.image = image;
        
        public void SetAlpha(float alpha)
        {
            if (image != null)
            {
                var color = image.color;
                color.a = alpha;
                image.color = color;
            }
        }
        
        public float GetAlpha() => image?.color.a ?? 0f;
        public bool IsValid() => image != null;
    }

    public class SpriteRendererFadeable : IFadeable
    {
        private SpriteRenderer spriteRenderer;
        
        public SpriteRendererFadeable(SpriteRenderer spriteRenderer) => this.spriteRenderer = spriteRenderer;
        
        public void SetAlpha(float alpha)
        {
            if (spriteRenderer != null)
            {
                var color = spriteRenderer.color;
                color.a = alpha;
                spriteRenderer.color = color;
            }
        }
        
        public float GetAlpha() => spriteRenderer?.color.a ?? 0f;
        public bool IsValid() => spriteRenderer != null;
    }

    public class CanvasGroupFadeable : IFadeable
    {
        private CanvasGroup canvasGroup;
        
        public CanvasGroupFadeable(CanvasGroup canvasGroup) => this.canvasGroup = canvasGroup;
        
        public void SetAlpha(float alpha)
        {
            if (canvasGroup != null)
                canvasGroup.alpha = alpha;
        }
        
        public float GetAlpha() => canvasGroup?.alpha ?? 0f;
        public bool IsValid() => canvasGroup != null;
    }

    // Clase para manejar una instancia de fade activa
    public class FadeInstance
    {
        public int Id { get; private set; }
        public IFadeable Target { get; private set; }
        public FadeConfig Config { get; private set; }
        public FadeCallbacks Callbacks { get; private set; }
        public CancellationTokenSource CancellationTokenSource { get; private set; }
        public bool IsActive { get; set; }
        
        private static int nextId = 0;
        
        public FadeInstance(IFadeable target, FadeConfig config, FadeCallbacks callbacks = null)
        {
            Id = ++nextId;
            Target = target;
            Config = config;
            Callbacks = callbacks ?? new FadeCallbacks();
            CancellationTokenSource = new CancellationTokenSource();
            IsActive = true;
        }
        
        public void Cancel()
        {
            IsActive = false;
            CancellationTokenSource?.Cancel();
            Callbacks?.OnCancel?.Invoke();
        }
        
        public void Dispose()
        {
            CancellationTokenSource?.Dispose();
        }
    }

    // Interface del manager
    public interface IFadeManager
    {
        UniTask<int> FadeImageAsync(Image image, FadeConfig config, FadeCallbacks callbacks = null);
        UniTask<int> FadeSpriteRendererAsync(SpriteRenderer sprite, FadeConfig config, FadeCallbacks callbacks = null);
        UniTask<int> FadeCanvasGroupAsync(CanvasGroup canvasGroup, FadeConfig config, FadeCallbacks callbacks = null);
        UniTask<int> FadeAsync(IFadeable fadeable, FadeConfig config, FadeCallbacks callbacks = null);
        
        // Métodos síncronos para compatibilidad
        int FadeImage(Image image, FadeConfig config, FadeCallbacks callbacks = null);
        int FadeSpriteRenderer(SpriteRenderer sprite, FadeConfig config, FadeCallbacks callbacks = null);
        int FadeCanvasGroup(CanvasGroup canvasGroup, FadeConfig config, FadeCallbacks callbacks = null);
        int Fade(IFadeable fadeable, FadeConfig config, FadeCallbacks callbacks = null);
        
        void StopFade(int fadeId);
        void StopAllFades();
        bool IsFading(int fadeId);
        UniTask WaitForFadeComplete(int fadeId);
    }

    // Manager principal - Singleton
    public class FadeManager : MonoBehaviour, IFadeManager
    {
        private static FadeManager instance;
        public static FadeManager Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = FindObjectOfType<FadeManager>();
                    if (instance == null)
                    {
                        GameObject go = new GameObject("FadeManager");
                        instance = go.AddComponent<FadeManager>();
                        DontDestroyOnLoad(go);
                    }
                }
                return instance;
            }
        }

        private Dictionary<int, FadeInstance> activeFades = new Dictionary<int, FadeInstance>();
        private Dictionary<int, UniTaskCompletionSource> fadeCompletionSources = new Dictionary<int, UniTaskCompletionSource>();

        private void Awake()
        {
            if (instance == null)
            {
                instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else if (instance != this)
            {
                Destroy(gameObject);
            }
        }

        private void OnDestroy()
        {
            StopAllFades();
        }

        // Métodos asíncronos
        public async UniTask<int> FadeImageAsync(Image image, FadeConfig config, FadeCallbacks callbacks = null)
        {
            return await FadeAsync(new ImageFadeable(image), config, callbacks);
        }

        public async UniTask<int> FadeSpriteRendererAsync(SpriteRenderer sprite, FadeConfig config, FadeCallbacks callbacks = null)
        {
            return await FadeAsync(new SpriteRendererFadeable(sprite), config, callbacks);
        }

        public async UniTask<int> FadeCanvasGroupAsync(CanvasGroup canvasGroup, FadeConfig config, FadeCallbacks callbacks = null)
        {
            return await FadeAsync(new CanvasGroupFadeable(canvasGroup), config, callbacks);
        }

        public async UniTask<int> FadeAsync(IFadeable fadeable, FadeConfig config, FadeCallbacks callbacks = null)
        {
            if (fadeable == null || !fadeable.IsValid())
            {
                Debug.LogWarning("FadeManager: Attempted to fade null or invalid object");
                return -1;
            }

            var fadeInstance = new FadeInstance(fadeable, config, callbacks);
            var completionSource = new UniTaskCompletionSource();
            
            activeFades[fadeInstance.Id] = fadeInstance;
            fadeCompletionSources[fadeInstance.Id] = completionSource;

            // Ejecutar el fade sin bloquear
            ExecuteFadeAsync(fadeInstance).Forget();
            
            return fadeInstance.Id;
        }

        // Métodos síncronos para compatibilidad
        public int FadeImage(Image image, FadeConfig config, FadeCallbacks callbacks = null)
        {
            return FadeImageAsync(image, config, callbacks).GetAwaiter().GetResult();
        }

        public int FadeSpriteRenderer(SpriteRenderer sprite, FadeConfig config, FadeCallbacks callbacks = null)
        {
            return FadeSpriteRendererAsync(sprite, config, callbacks).GetAwaiter().GetResult();
        }

        public int FadeCanvasGroup(CanvasGroup canvasGroup, FadeConfig config, FadeCallbacks callbacks = null)
        {
            return FadeCanvasGroupAsync(canvasGroup, config, callbacks).GetAwaiter().GetResult();
        }

        public int Fade(IFadeable fadeable, FadeConfig config, FadeCallbacks callbacks = null)
        {
            return FadeAsync(fadeable, config, callbacks).GetAwaiter().GetResult();
        }

        public void StopFade(int fadeId)
        {
            if (activeFades.TryGetValue(fadeId, out var fadeInstance))
            {
                fadeInstance.Cancel();
                CleanupFade(fadeId);
            }
        }

        public void StopAllFades()
        {
            var fadeIds = new List<int>(activeFades.Keys);
            foreach (var fadeId in fadeIds)
            {
                StopFade(fadeId);
            }
        }

        public bool IsFading(int fadeId)
        {
            return activeFades.ContainsKey(fadeId) && activeFades[fadeId].IsActive;
        }

        public async UniTask WaitForFadeComplete(int fadeId)
        {
            if (fadeCompletionSources.TryGetValue(fadeId, out var completionSource))
            {
                await completionSource.Task;
            }
        }

        private async UniTaskVoid ExecuteFadeAsync(FadeInstance fadeInstance)
        {
            try
            {
                fadeInstance.Callbacks.OnStart?.Invoke();
                
                await PerformFadeAsync(fadeInstance);
                
                if (fadeInstance.IsActive)
                {
                    // Ejecutar callback de completado
                    fadeInstance.Callbacks.OnComplete?.Invoke();
                    
                    // Ejecutar método encadenado
                    fadeInstance.Callbacks.ChainedMethod?.Invoke();
                }
            }
            catch (OperationCanceledException)
            {
                fadeInstance.Callbacks.OnCancel?.Invoke();
            }
            catch (Exception ex)
            {
                Debug.LogError($"FadeManager: Error during fade execution: {ex.Message}");
            }
            finally
            {
                CleanupFade(fadeInstance.Id);
            }
        }

        private async UniTask PerformFadeAsync(FadeInstance fadeInstance)
        {
            var config = fadeInstance.Config;
            var target = fadeInstance.Target;
            var token = fadeInstance.CancellationTokenSource.Token;

            // Delay inicial
            if (config.delay > 0)
            {
                await UniTask.Delay(TimeSpan.FromSeconds(config.delay), cancellationToken: token);
            }

            if (!fadeInstance.IsActive || !target.IsValid())
                return;

            switch (config.direction)
            {
                case FadeDirection.In:
                    await FadeSingleDirectionAsync(fadeInstance, 0f, config.targetAlpha, config.duration, token);
                    break;
                case FadeDirection.Out:
                    await FadeSingleDirectionAsync(fadeInstance, target.GetAlpha(), 0f, config.duration, token);
                    break;
                case FadeDirection.InOut:
                    await FadeInOutAsync(fadeInstance, token);
                    break;
                case FadeDirection.OutIn:
                    await FadeOutInAsync(fadeInstance, token);
                    break;
            }
        }

        private async UniTask FadeSingleDirectionAsync(FadeInstance fadeInstance, float startAlpha, float targetAlpha, float duration, CancellationToken token)
        {
            var target = fadeInstance.Target;
            var config = fadeInstance.Config;
            
            target.SetAlpha(startAlpha);
            
            float elapsedTime = 0f;
            
            while (elapsedTime < duration && fadeInstance.IsActive && target.IsValid())
            {
                token.ThrowIfCancellationRequested();
                
                float progress = elapsedTime / duration;
                float easedProgress = ApplyEasing(progress, config.easeType, config.customCurve);
                float currentAlpha = Mathf.Lerp(startAlpha, targetAlpha, easedProgress);
                
                target.SetAlpha(currentAlpha);
                fadeInstance.Callbacks.OnUpdate?.Invoke(currentAlpha);

                elapsedTime += config.ignoreTimeScale ? Time.unscaledDeltaTime : Time.deltaTime;
                
                await UniTask.Yield(PlayerLoopTiming.Update, token);
            }

            if (fadeInstance.IsActive && target.IsValid())
            {
                target.SetAlpha(targetAlpha);
            }
        }

        private async UniTask FadeInOutAsync(FadeInstance fadeInstance, CancellationToken token)
        {
            var config = fadeInstance.Config;
            var halfDuration = config.duration * 0.5f;
            
            // Fade In
            await FadeSingleDirectionAsync(fadeInstance, 0f, config.targetAlpha, halfDuration, token);
            
            if (!fadeInstance.IsActive) return;
            
            // Fade Out
            await FadeSingleDirectionAsync(fadeInstance, config.targetAlpha, 0f, halfDuration, token);
        }

        private async UniTask FadeOutInAsync(FadeInstance fadeInstance, CancellationToken token)
        {
            var config = fadeInstance.Config;
            var halfDuration = config.duration * 0.5f;
            var currentAlpha = fadeInstance.Target.GetAlpha();
            
            // Fade Out
            await FadeSingleDirectionAsync(fadeInstance, currentAlpha, 0f, halfDuration, token);
            
            if (!fadeInstance.IsActive) return;
            
            // Fade In
            await FadeSingleDirectionAsync(fadeInstance, 0f, config.targetAlpha, halfDuration, token);
        }

        private void CleanupFade(int fadeId)
        {
            if (activeFades.TryGetValue(fadeId, out var fadeInstance))
            {
                fadeInstance.Dispose();
                activeFades.Remove(fadeId);
            }
            
            if (fadeCompletionSources.TryGetValue(fadeId, out var completionSource))
            {
                completionSource.TrySetResult();
                fadeCompletionSources.Remove(fadeId);
            }
        }

        private float ApplyEasing(float t, EaseType easeType, AnimationCurve customCurve = null)
        {
            switch (easeType)
            {
                case EaseType.Linear:
                    return t;
                case EaseType.EaseIn:
                    return t * t;
                case EaseType.EaseOut:
                    return 1f - (1f - t) * (1f - t);
                case EaseType.EaseInOut:
                    return t < 0.5f ? 2f * t * t : 1f - Mathf.Pow(-2f * t + 2f, 2f) / 2f;
                case EaseType.EaseInQuad:
                    return t * t;
                case EaseType.EaseOutQuad:
                    return 1f - (1f - t) * (1f - t);
                case EaseType.EaseInOutQuad:
                    return t < 0.5f ? 2f * t * t : 1f - Mathf.Pow(-2f * t + 2f, 2f) / 2f;
                case EaseType.EaseInCubic:
                    return t * t * t;
                case EaseType.EaseOutCubic:
                    return 1f - Mathf.Pow(1f - t, 3f);
                case EaseType.EaseInOutCubic:
                    return t < 0.5f ? 4f * t * t * t : 1f - Mathf.Pow(-2f * t + 2f, 3f) / 2f;
                case EaseType.EaseInSine:
                    return 1f - Mathf.Cos(t * Mathf.PI / 2f);
                case EaseType.EaseOutSine:
                    return Mathf.Sin(t * Mathf.PI / 2f);
                case EaseType.EaseInOutSine:
                    return -(Mathf.Cos(Mathf.PI * t) - 1f) / 2f;
                case EaseType.EaseInExpo:
                    return t == 0f ? 0f : Mathf.Pow(2f, 10f * (t - 1f));
                case EaseType.EaseOutExpo:
                    return t == 1f ? 1f : 1f - Mathf.Pow(2f, -10f * t);
                case EaseType.EaseInOutExpo:
                    if (t == 0f) return 0f;
                    if (t == 1f) return 1f;
                    return t < 0.5f ? Mathf.Pow(2f, 20f * t - 10f) / 2f : (2f - Mathf.Pow(2f, -20f * t + 10f)) / 2f;
                case EaseType.Custom:
                    return customCurve?.Evaluate(t) ?? t;
                default:
                    return t;
            }
        }
    }

    // Clase estática para acceso rápido con callbacks mejorados
    public static class ImageFader
    {
        public static int FadeIn(Image image, float duration = 1f, EaseType easeType = EaseType.Linear, 
            Action onComplete = null, Action chainedMethod = null, Action<float> onUpdate = null)
        {
            var config = new FadeConfig(duration, easeType, FadeDirection.In);
            var callbacks = new FadeCallbacks(onComplete, onUpdate, chainedMethod);
            return FadeManager.Instance.FadeImage(image, config, callbacks);
        }

        public static int FadeOut(Image image, float duration = 1f, EaseType easeType = EaseType.Linear, 
            Action onComplete = null, Action chainedMethod = null, Action<float> onUpdate = null)
        {
            var config = new FadeConfig(duration, easeType, FadeDirection.Out);
            var callbacks = new FadeCallbacks(onComplete, onUpdate, chainedMethod);
            return FadeManager.Instance.FadeImage(image, config, callbacks);
        }

        public static int FadeInOut(Image image, float duration = 2f, EaseType easeType = EaseType.Linear, 
            Action onComplete = null, Action chainedMethod = null, Action<float> onUpdate = null)
        {
            var config = new FadeConfig(duration, easeType, FadeDirection.InOut);
            var callbacks = new FadeCallbacks(onComplete, onUpdate, chainedMethod);
            return FadeManager.Instance.FadeImage(image, config, callbacks);
        }

        // Métodos para encadenar múltiples fades
        public static async UniTask<int> FadeInAsync(Image image, float duration = 1f, EaseType easeType = EaseType.Linear)
        {
            var config = new FadeConfig(duration, easeType, FadeDirection.In);
            return await FadeManager.Instance.FadeImageAsync(image, config);
        }

        public static async UniTask<int> FadeOutAsync(Image image, float duration = 1f, EaseType easeType = EaseType.Linear)
        {
            var config = new FadeConfig(duration, easeType, FadeDirection.Out);
            return await FadeManager.Instance.FadeImageAsync(image, config);
        }

        // Método para encadenar fades con callback
        public static int FadeWithChain(Image image, FadeConfig config, params Action[] chainedMethods)
        {
            var callbacks = new FadeCallbacks();
            foreach (var method in chainedMethods)
            {
                callbacks.Chain(method);
            }
            return FadeManager.Instance.FadeImage(image, config, callbacks);
        }

        public static void StopFade(int fadeId) => FadeManager.Instance.StopFade(fadeId);
        public static void StopAllFades() => FadeManager.Instance.StopAllFades();
        public static async UniTask WaitForFade(int fadeId) => await FadeManager.Instance.WaitForFadeComplete(fadeId);
    }

    // Extensiones para facilitar el encadenamiento
    public static class FadeExtensions
    {
        public static FadeCallbacks OnComplete(this FadeCallbacks callbacks, Action action)
        {
            callbacks.OnComplete += action;
            return callbacks;
        }

        public static FadeCallbacks OnUpdate(this FadeCallbacks callbacks, Action<float> action)
        {
            callbacks.OnUpdate += action;
            return callbacks;
        }

        public static FadeCallbacks ThenCall(this FadeCallbacks callbacks, Action action)
        {
            callbacks.ChainedMethod += action;
            return callbacks;
        }

        public static FadeCallbacks ThenCallWithDelay(this FadeCallbacks callbacks, Action action, float delay)
        {
            return callbacks.ChainWithDelay(action, delay);
        }
    }
}