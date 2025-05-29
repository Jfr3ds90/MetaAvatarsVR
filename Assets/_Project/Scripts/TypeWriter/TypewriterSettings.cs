using UnityEngine;
using System;

[CreateAssetMenu(fileName = "TypewriterSettings", menuName = "UI/Typewriter Settings")]
public class TypewriterSettings : ScriptableObject
{
    [Header("Timing Configuration")]
    [SerializeField, Range(0.001f, 0.5f)]
    private float letterDelay = 0.05f;
    
    [SerializeField, Range(0.001f, 2f)]
    private float wordPause = 0.1f;
    
    [SerializeField, Range(0f, 10f)]
    private float screenTime = 3f;
    
    [SerializeField]
    private bool autoAdvance = true;
    
    [Header("Punctuation Delays")]
    [SerializeField, Range(0f, 1f)]
    private float commaPause = 0.2f;
    
    [SerializeField, Range(0f, 1f)]
    private float periodPause = 0.4f;
    
    [SerializeField, Range(0f, 1f)]
    private float questionExclamationPause = 0.3f;
    
    [Header("Audio Configuration")]
    [SerializeField] private AudioClip letterSound;

    [SerializeField] private AudioClip[] letterSounds;
    
    [SerializeField, Range(0f, 1f)]
    private float soundVolume = 0.5f;
    
    [SerializeField, Range(0.5f, 2f)]
    private float soundPitchVariation = 0.1f;

    [SerializeField] private bool hasVariation;
    
    [Header("Visual Effects")]
    [SerializeField]
    private AnimationCurve speedCurve = AnimationCurve.Linear(0f, 1f, 1f, 1f);
    
    [SerializeField]
    private bool supportRichText = true;
    
    [SerializeField]
    private bool instantSpaces = true;
    
    [Header("Skip Configuration")]
    [SerializeField]
    private bool allowSkip = true;
    
    [SerializeField]
    private KeyCode skipKey = KeyCode.Space;

    // Properties
    public float LetterDelay => letterDelay;
    public float WordPause => wordPause;
    public float ScreenTime => screenTime;
    public bool AutoAdvance => autoAdvance;
    public float CommaPause => commaPause;
    public float PeriodPause => periodPause;
    public float QuestionExclamationPause => questionExclamationPause;
    public AudioClip LetterSound => letterSound;
    public AudioClip[] LetterSounds => letterSounds;
    public float SoundVolume => soundVolume;
    public float SoundPitchVariation => soundPitchVariation;
    public bool HasVariation => hasVariation;
    public AnimationCurve SpeedCurve => speedCurve;
    public bool SupportRichText => supportRichText;
    public bool InstantSpaces => instantSpaces;
    public bool AllowSkip => allowSkip;
    public KeyCode SkipKey => skipKey;
    
    public float GetPunctuationDelay(char character)
    {
        return character switch
        {
            ',' or ';' => commaPause,
            '.' => periodPause,
            '?' or '!' => questionExclamationPause,
            _ => 0f
        };
    }
}