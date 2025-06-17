using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using FadeSystem;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using Oculus.Platform.Models;

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

    [Header("Debug Text")]
    [SerializeField] private TMP_Text _Text;
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
    
    public void OnChangeScene()
    {
        _Text.text += "Cambio de escena ";
        _Text.text += (" "+Console.Error+" ");
        SceneManager.LoadScene("Level_Oficina");
        _Text.text += (" " + Console.Error + " ");
        _Text.text += " Cambio de escena ejecutado";
    }
    //private void Update()
    //{
    //    _Text.text += Console.Error;
    //}
}