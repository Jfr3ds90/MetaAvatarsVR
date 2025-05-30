using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using FadeSystem;
using UnityEngine;
using UnityEngine.UI;

public class CinematicController : MonoBehaviour
{
    [Header("Cinematic Controller")] 
    [Space(5)]
    
    [Header("Delay to Start")] 
    [SerializeField] private float _delayToStart = 1f;

    [Header("Fade")] 
    [SerializeField] private Image _imageFade = null;
    [SerializeField] private float _fadeTime;

    [Header("TypeWriter")] 
    [SerializeField] private TypewriterEffect _typewriterEffect;
    
    [Header("PlayableDirector")]
    [SerializeField] private GameObject _playableDirector;

    [Header("Fade Managre")] 
    [SerializeField] private FadeManager _fadeManager;
    [SerializeField] private FadeConfig _fadeConfig;

    private void Start()
    {
        _fadeManager = FadeManager.Instance;
        if (_typewriterEffect != null)
        {
            WaitTexts();
        }
        else
        {
            Debug.LogWarning("TypewriterEffect is null!");
            return;
        }
    }

    private async UniTask WaitTexts()
    {
        await UniTask.WaitForSeconds(_delayToStart);
        await _typewriterEffect.StartSequence();
        await UniTask.WaitForSeconds(_delayToStart);
        _typewriterEffect.gameObject.SetActive(false);
       
        _fadeManager.FadeImage(_imageFade, _fadeConfig, OnActivePlayable());
    }

    private FadeCallbacks OnActivePlayable()
    {
        _playableDirector.SetActive(true);
        return null;
    }
}