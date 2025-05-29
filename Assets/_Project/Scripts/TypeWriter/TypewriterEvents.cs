using System;
using UnityEngine;
using UnityEngine.Events;

[System.Serializable]
public class TypewriterEvents
{
    [Header("Sequence Events")]
    public UnityEvent OnSequenceStarted = new UnityEvent();
    public UnityEvent OnSequenceCompleted = new UnityEvent();
    public UnityEvent<int> OnTextChanged = new UnityEvent<int>(); // Index del texto actual
    
    [Header("Character Events")]
    public UnityEvent<char> OnCharacterTyped = new UnityEvent<char>();
    public UnityEvent OnWordCompleted = new UnityEvent();
    public UnityEvent OnTextCompleted = new UnityEvent();
    
    [Header("User Interaction")]
    public UnityEvent OnSequenceSkipped = new UnityEvent();
    public UnityEvent OnTextSkipped = new UnityEvent();
    
    [Header("Audio Events")]
    public UnityEvent<char> OnLetterSoundRequested = new UnityEvent<char>();
}

// Estructura para datos del texto actual
[System.Serializable]
public struct TypewriterTextData
{
    public string fullText;
    public string currentText;
    public int currentIndex;
    public int totalCharacters;
    public bool isCompleted;
    public float progress;
    
    public TypewriterTextData(string full, string current, int index, int total)
    {
        fullText = full;
        currentText = current;
        currentIndex = index;
        totalCharacters = total;
        isCompleted = index >= total;
        progress = total > 0 ? (float)index / total : 0f;
    }
}