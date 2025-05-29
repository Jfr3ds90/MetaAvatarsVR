using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Cysharp.Threading.Tasks;
using System.Threading;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

[RequireComponent(typeof(AudioSource))]
public class TypewriterEffect : MonoBehaviour
{
    [Header("Configuration")]
    [SerializeField] private TypewriterSettings settings;
    [SerializeField, TextArea] private string[] textArray = new string[0];
    
    [Header("UI References")]
    [SerializeField] private Text legacyText;
    [SerializeField] private TextMeshProUGUI tmpText;
    
    [Header("Events")]
    [SerializeField] private TypewriterEvents events = new TypewriterEvents();
    
    // Private fields
    private AudioSource audioSource;
    private CancellationTokenSource cancellationTokenSource;
    private StringBuilder stringBuilder = new StringBuilder();
    private bool isPlaying = false;
    private bool isSkipped = false;
    private int currentTextIndex = 0;
    private int soundIndex = 0;
    
    // Rich text regex pattern para remover tags al contar caracteres
    private static readonly Regex RichTextPattern = new Regex(@"<[^>]*>", RegexOptions.Compiled);
    
    // Properties
    public bool IsPlaying => isPlaying;
    public int CurrentTextIndex => currentTextIndex;
    public string[] TextArray 
    { 
        get => textArray; 
        set 
        {
            textArray = value;
            currentTextIndex = 0;
        }
    }
    public TypewriterSettings Settings 
    { 
        get => settings; 
        set => settings = value; 
    }
    
    private async void Awake()
    {
        InitializeComponents();
        ValidateSettings();
    }
    
    private void InitializeComponents()
    {
        audioSource = GetComponent<AudioSource>();
        
        // Auto-detect text component si no está asignado
        if (legacyText == null && tmpText == null)
        {
            tmpText = GetComponent<TextMeshProUGUI>();
            if (tmpText == null)
            {
                legacyText = GetComponent<Text>();
            }
        }
        
        if (legacyText == null && tmpText == null)
        {
            Debug.LogError($"TypewriterEffect: No Text or TextMeshProUGUI component found on {gameObject.name}");
        }
    }
    
    private void ValidateSettings()
    {
        if (settings == null)
        {
            Debug.LogWarning($"TypewriterEffect: No settings assigned on {gameObject.name}. Using default values.");
        }
    }
    
    private void Update()
    {
        HandleInput();
    }
    
    private void HandleInput()
    {
        if (!isPlaying || settings == null || !settings.AllowSkip) return;
        
        if (Input.GetKeyDown(settings.SkipKey))
        {
            SkipCurrent();
        }
    }
    
    #region Public API
    
    /// <summary>
    /// Inicia la secuencia de typewriter con el array configurado
    /// </summary>
    public async UniTask StartSequence()
    {
        await StartSequence(textArray);
    }
    
    /// <summary>
    /// Inicia la secuencia de typewriter con un array específico
    /// </summary>
    public async UniTask StartSequence(string[] texts)
    {
        if (texts == null || texts.Length == 0)
        {
            Debug.LogWarning("TypewriterEffect: Text array is null or empty");
            return;
        }
        
        StopSequence();
        
        textArray = texts;
        currentTextIndex = 0;
        isPlaying = true;
        
        cancellationTokenSource = new CancellationTokenSource();
        events.OnSequenceStarted?.Invoke();
        
        try
        {
            for (int i = 0; i < textArray.Length; i++)
            {
                currentTextIndex = i;
                events.OnTextChanged?.Invoke(i);
                
                await PlayText(textArray[i], cancellationTokenSource.Token);
                
                // Pausa entre textos (si hay auto-advance y no es el último)
                if (settings.AutoAdvance && i < textArray.Length - 1)
                {
                    await UniTask.Delay(
                        System.TimeSpan.FromSeconds(settings.ScreenTime), 
                        cancellationToken: cancellationTokenSource.Token
                    );
                }
                else if (!settings.AutoAdvance && i < textArray.Length - 1)
                {
                    // Esperar input del usuario para continuar
                    await WaitForUserInput(cancellationTokenSource.Token);
                }
            }
            
            events.OnSequenceCompleted?.Invoke();
        }
        catch (System.OperationCanceledException)
        {
            // Secuencia cancelada
        }
        finally
        {
            isPlaying = false;
            cancellationTokenSource?.Dispose();
            cancellationTokenSource = null;
        }
    }
    
    /// <summary>
    /// Detiene la secuencia actual
    /// </summary>
    public void StopSequence()
    {
        if (cancellationTokenSource != null)
        {
            cancellationTokenSource.Cancel();
            cancellationTokenSource.Dispose();
            cancellationTokenSource = null;
        }
        
        isPlaying = false;
        isSkipped = false;
    }
    
    /// <summary>
    /// Salta al siguiente texto o completa el actual
    /// </summary>
    public void SkipCurrent()
    {
        if (!isPlaying) return;
        
        isSkipped = true;
        events.OnTextSkipped?.Invoke();
    }
    
    /// <summary>
    /// Salta toda la secuencia
    /// </summary>
    public void SkipSequence()
    {
        if (!isPlaying) return;
        
        events.OnSequenceSkipped?.Invoke();
        StopSequence();
    }
    
    #endregion
    
    #region Private Methods
    
    private async UniTask PlayText(string text, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(text)) return;
        
        isSkipped = false;
        string processedText = settings.SupportRichText ? text : RichTextPattern.Replace(text, "");
        string displayText = settings.SupportRichText ? text : processedText;
        
        // Preparar StringBuilder
        stringBuilder.Clear();
        stringBuilder.Capacity = Mathf.Max(stringBuilder.Capacity, displayText.Length);
        
        SetDisplayText("");
        
        int visibleCharCount = 0;
        int totalVisibleChars = processedText.Length;
        
        for (int i = 0; i < displayText.Length && !isSkipped; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            
            char currentChar = displayText[i];
            
            // Manejar rich text tags
            if (settings.SupportRichText && currentChar == '<')
            {
                // Encontrar el final del tag
                int tagEnd = displayText.IndexOf('>', i);
                if (tagEnd != -1)
                {
                    // Agregar todo el tag de una vez
                    stringBuilder.Append(displayText.Substring(i, tagEnd - i + 1));
                    i = tagEnd;
                    SetDisplayText(stringBuilder.ToString());
                    continue;
                }
            }
            
            stringBuilder.Append(currentChar);
            SetDisplayText(stringBuilder.ToString());
            
            // Solo contar caracteres visibles para eventos y delays
            if (!IsRichTextTag(currentChar))
            {
                visibleCharCount++;
                events.OnCharacterTyped?.Invoke(currentChar);
                
                // Reproducir sonido
                if ((settings.LetterSound != null || settings.LetterSounds.Length >= 1) && !char.IsWhiteSpace(currentChar))
                {
                    PlayLetterSound(currentChar);
                }
 
                // Aplicar delay basado en el tipo de carácter
                float delay = CalculateCharacterDelay(currentChar, visibleCharCount, totalVisibleChars);
                
                if (delay > 0)
                {
                    await UniTask.Delay(
                        System.TimeSpan.FromSeconds(delay), 
                        cancellationToken: cancellationToken
                    );
                }
                
                // Evento de palabra completada (espacio o puntuación)
                if (char.IsWhiteSpace(currentChar) || char.IsPunctuation(currentChar))
                {
                    events.OnWordCompleted?.Invoke();
                }
            }
        }
        
        // Si fue skipped, mostrar el texto completo inmediatamente
        if (isSkipped)
        {
            SetDisplayText(displayText);
        }
        
        events.OnTextCompleted?.Invoke();
    }
    
    private float CalculateCharacterDelay(char character, int currentIndex, int totalChars)
    {
        // Caracteres instantáneos
        if (settings.InstantSpaces && char.IsWhiteSpace(character))
        {
            return 0f;
        }
        
        // Delay base modificado por la curva de velocidad
        float normalizedProgress = (float)currentIndex / totalChars;
        float speedMultiplier = settings.SpeedCurve.Evaluate(normalizedProgress);
        float baseDelay = settings.LetterDelay / speedMultiplier;
        
        // Delay adicional para puntuación
        float punctuationDelay = settings.GetPunctuationDelay(character);
        
        // Delay para espacios (pausa entre palabras)
        float wordDelay = char.IsWhiteSpace(character) ? settings.WordPause : 0f;
        
        return baseDelay + punctuationDelay + wordDelay;
    }
    
    private void PlayLetterSound(char character)
    {
        if (audioSource == null || (settings.LetterSound == null && settings.LetterSounds.Length <= 0)) return;
        
        // Variación de pitch para hacer el sonido más dinámico
        float basePitch = 1f;
        if (settings.HasVariation)
        {
            float pitchVariation = Random.Range(-settings.SoundPitchVariation, settings.SoundPitchVariation);
            audioSource.pitch = basePitch + pitchVariation;
        }
        audioSource.volume = settings.SoundVolume;

        if (settings.LetterSound != null)
        {
            audioSource.PlayOneShot(settings.LetterSound);
        }
        else
        {
            audioSource.PlayOneShot(settings.LetterSounds[soundIndex]);
            soundIndex++;

            if (soundIndex > settings.LetterSounds.Length - 1)
            {
                soundIndex = 0;
            }
        }
        events.OnLetterSoundRequested?.Invoke(character);
    }
    
    private void SetDisplayText(string text)
    {
        if (tmpText != null)
        {
            tmpText.text = text;
        }
        else if (legacyText != null)
        {
            legacyText.text = text;
        }
    }
    
    private bool IsRichTextTag(char character)
    {
        return character == '<' || character == '>';
    }
    
    private async UniTask WaitForUserInput(CancellationToken cancellationToken)
    {
        bool waitingForInput = true;
        
        while (waitingForInput && !cancellationToken.IsCancellationRequested)
        {
            if (Input.GetKeyDown(settings.SkipKey))
            {
                waitingForInput = false;
            }
            
            await UniTask.Yield(cancellationToken);
        }
    }
    
    #endregion
    
    private void OnDestroy()
    {
        StopSequence();
    }
    
    private void OnDisable()
    {
        StopSequence();
    }
}