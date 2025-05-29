using UnityEngine;
using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Manager para controlar múltiples TypewriterEffect y secuencias complejas
/// </summary>
public class TypewriterManager : MonoBehaviour
{
    [Header("Global Settings")]
    [SerializeField] private TypewriterSettings defaultSettings;
    [SerializeField] private bool pauseGameDuringSequences = false;
    
    [Header("Registered Typewriters")]
    [SerializeField] private List<TypewriterEffect> registeredTypewriters = new List<TypewriterEffect>();
    
    // Events
    public System.Action OnAllSequencesCompleted;
    public System.Action<TypewriterEffect> OnTypewriterStarted;
    public System.Action<TypewriterEffect> OnTypewriterCompleted;
    
    // Private fields
    private readonly Dictionary<string, TypewriterEffect> namedTypewriters = new Dictionary<string, TypewriterEffect>();
    private readonly List<TypewriterEffect> activeTypewriters = new List<TypewriterEffect>();
    private float originalTimeScale = 1f;
    
    #region Singleton Pattern
    private static TypewriterManager instance;
    public static TypewriterManager Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindObjectOfType<TypewriterManager>();
                if (instance == null)
                {
                    GameObject go = new GameObject("TypewriterManager");
                    instance = go.AddComponent<TypewriterManager>();
                }
            }
            return instance;
        }
    }
    #endregion
    
    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
            originalTimeScale = Time.timeScale;
        }
        else if (instance != this)
        {
            Destroy(gameObject);
        }
    }
    
    private void Start()
    {
        RegisterExistingTypewriters();
    }
    
    #region Registration Methods
    
    /// <summary>
    /// Registra automáticamente todos los TypewriterEffect en la escena
    /// </summary>
    public void RegisterExistingTypewriters()
    {
        TypewriterEffect[] effects = FindObjectsOfType<TypewriterEffect>();
        foreach (var effect in effects)
        {
            RegisterTypewriter(effect);
        }
    }
    
    /// <summary>
    /// Registra un TypewriterEffect específico
    /// </summary>
    public void RegisterTypewriter(TypewriterEffect typewriter, string name = null)
    {
        if (typewriter == null) return;
        
        if (!registeredTypewriters.Contains(typewriter))
        {
            registeredTypewriters.Add(typewriter);
            
            // Asignar settings por defecto si no tiene
            if (typewriter.Settings == null && defaultSettings != null)
            {
                typewriter.Settings = defaultSettings;
            }
            
            // Registrar con nombre si se proporciona
            if (!string.IsNullOrEmpty(name))
            {
                namedTypewriters[name] = typewriter;
            }
        }
    }
    
    /// <summary>
    /// Desregistra un TypewriterEffect
    /// </summary>
    public void UnregisterTypewriter(TypewriterEffect typewriter)
    {
        if (typewriter == null) return;
        
        registeredTypewriters.Remove(typewriter);
        activeTypewriters.Remove(typewriter);
        
        // Remover de named typewriters
        var toRemove = namedTypewriters.Where(kvp => kvp.Value == typewriter).ToList();
        foreach (var kvp in toRemove)
        {
            namedTypewriters.Remove(kvp.Key);
        }
    }
    
    #endregion
    
    #region Playback Control
    
    /// <summary>
    /// Reproduce una secuencia en un typewriter específico por nombre
    /// </summary>
    public async UniTask PlaySequence(string typewriterName, string[] texts)
    {
        if (namedTypewriters.TryGetValue(typewriterName, out TypewriterEffect typewriter))
        {
            await PlaySequence(typewriter, texts);
        }
        else
        {
            Debug.LogWarning($"TypewriterManager: No typewriter found with name '{typewriterName}'");
        }
    }
    
    /// <summary>
    /// Reproduce una secuencia en un typewriter específico
    /// </summary>
    public async UniTask PlaySequence(TypewriterEffect typewriter, string[] texts)
    {
        if (typewriter == null || texts == null || texts.Length == 0) return;
        
        await StartSequenceInternal(typewriter, texts);
    }
    
    /// <summary>
    /// Reproduce secuencias en paralelo en múltiples typewriters
    /// </summary>
    public async UniTask PlayParallelSequences(Dictionary<TypewriterEffect, string[]> sequences)
    {
        if (sequences == null || sequences.Count == 0) return;
        
        if (pauseGameDuringSequences)
        {
            Time.timeScale = 0f;
        }
        
        List<UniTask> tasks = new List<UniTask>();
        
        foreach (var kvp in sequences)
        {
            if (kvp.Key != null && kvp.Value != null)
            {
                tasks.Add(StartSequenceInternal(kvp.Key, kvp.Value));
            }
        }
        
        await UniTask.WhenAll(tasks);
        
        if (pauseGameDuringSequences)
        {
            Time.timeScale = originalTimeScale;
        }
        
        OnAllSequencesCompleted?.Invoke();
    }
    
    /// <summary>
    /// Reproduce secuencias en secuencia (una después de otra)
    /// </summary>
    public async UniTask PlaySequentialSequences(Dictionary<TypewriterEffect, string[]> sequences)
    {
        if (sequences == null || sequences.Count == 0) return;
        
        if (pauseGameDuringSequences)
        {
            Time.timeScale = 0f;
        }
        
        foreach (var kvp in sequences)
        {
            if (kvp.Key != null && kvp.Value != null)
            {
                await StartSequenceInternal(kvp.Key, kvp.Value);
            }
        }
        
        if (pauseGameDuringSequences)
        {
            Time.timeScale = originalTimeScale;
        }
        
        OnAllSequencesCompleted?.Invoke();
    }
    
    /// <summary>
    /// Detiene todas las secuencias activas
    /// </summary>
    public void StopAllSequences()
    {
        foreach (var typewriter in activeTypewriters.ToList())
        {
            if (typewriter != null)
            {
                typewriter.StopSequence();
            }
        }
        
        activeTypewriters.Clear();
        
        if (pauseGameDuringSequences)
        {
            Time.timeScale = originalTimeScale;
        }
    }
    
    /// <summary>
    /// Salta todas las secuencias activas
    /// </summary>
    public void SkipAllSequences()
    {
        foreach (var typewriter in activeTypewriters.ToList())
        {
            if (typewriter != null)
            {
                typewriter.SkipSequence();
            }
        }
    }
    
    #endregion
    
    #region Utility Methods
    
    /// <summary>
    /// Obtiene un typewriter por nombre
    /// </summary>
    public TypewriterEffect GetTypewriter(string name)
    {
        namedTypewriters.TryGetValue(name, out TypewriterEffect typewriter);
        return typewriter;
    }
    
    /// <summary>
    /// Verifica si hay secuencias activas
    /// </summary>
    public bool HasActiveSequences()
    {
        return activeTypewriters.Any(t => t != null && t.IsPlaying);
    }
    
    /// <summary>
    /// Obtiene el número de secuencias activas
    /// </summary>
    public int GetActiveSequenceCount()
    {
        return activeTypewriters.Count(t => t != null && t.IsPlaying);
    }
    
    #endregion
    
    #region Private Methods
    
    private async UniTask StartSequenceInternal(TypewriterEffect typewriter, string[] texts)
    {
        if (!activeTypewriters.Contains(typewriter))
        {
            activeTypewriters.Add(typewriter);
        }
        
        OnTypewriterStarted?.Invoke(typewriter);
        
        try
        {
            await typewriter.StartSequence(texts);
        }
        finally
        {
            activeTypewriters.Remove(typewriter);
            OnTypewriterCompleted?.Invoke(typewriter);
        }
    }
    
    #endregion
    
    private void OnDestroy()
    {
        StopAllSequences();
    }
}