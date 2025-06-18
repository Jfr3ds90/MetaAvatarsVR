using System;
using System.Threading;
using System.IO;
using System.Text;
using Cysharp.Threading.Tasks;
using FadeSystem;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

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
        _Text.text += (" "+ LogType.Assert + " la cantidad de escenas total es "+SceneManager.sceneCount);
        SceneManager.LoadScene("Level_Oficina");
        //SceneManager.LoadSceneAsync("Level_Oficina");
        _Text.text += (" " + LogType.Assert + " y la cantidad de escenas cargadas es "+SceneManager.loadedSceneCount+" tambien como extra escenas en build "+SceneManager.sceneCountInBuildSettings);
        _Text.text += " Cambio de escena ejecutado";
        SceneManager.activeSceneChanged += SceneManager_activeSceneChanged; ;
    }

    private void SceneManager_activeSceneChanged(Scene arg0, Scene arg1)
    {
        Debug.Log(arg0.name +" paso a la escena "+ arg1.name);
    }

    private void OnSceneUnloaded(Scene current)
    {
        Debug.Log("OnSceneUnloaded: " + current);
    }
    private void Update()
    {
        // Application.logMessageReceived += _Text.text;
    }
}