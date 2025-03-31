using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Oculus.Interaction;
using System.Collections.Generic;
using UnityEngine.SceneManagement;

public class SceneLoadingManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GameObject networkManager;
    [SerializeField] private SceneData[] scenes;
    [SerializeField] private TextMeshProUGUI sceneName;
    [SerializeField] private Image sceneImage;

    [Header("Interactables")]
    [SerializeField] private PokeInteractable previousPokeInteractable;
    [SerializeField] private PokeInteractable loadPokeInteractable;
    [SerializeField] private PokeInteractable nextPokeInteractable;

    private int currentIndex = 0;

    private void OnEnable()
    {
        ValidateScenes();
        UpdateUI();

        nextPokeInteractable.WhenPointerEventRaised += NextScene;
        previousPokeInteractable.WhenPointerEventRaised += PreviousScene;
        loadPokeInteractable.WhenPointerEventRaised += LoadCurrentScene;
    }

    private void OnDisable()
    {
        nextPokeInteractable.WhenPointerEventRaised -= NextScene;
        previousPokeInteractable.WhenPointerEventRaised -= PreviousScene;
        loadPokeInteractable.WhenPointerEventRaised -= LoadCurrentScene;
    }

    private void ValidateScenes()
    {
        List<SceneData> validScenes = new();

        foreach (SceneData sceneData in scenes)
        {
            if (SceneUtility.GetBuildIndexByScenePath(sceneData.GetSceneName) != -1)
                validScenes.Add(sceneData);
            else
                Debug.LogWarning($"Scene '{sceneData.GetSceneName}' is not in build settings and will be removed.");
        }

        scenes = validScenes.ToArray();
    }

    private void UpdateUI()
    {
        sceneImage.sprite = scenes[currentIndex].SceneSprite;
        sceneName.text = scenes[currentIndex].GetSceneName;

        nextPokeInteractable.enabled = currentIndex < scenes.Length - 1;
        previousPokeInteractable.enabled = currentIndex > 0;
    }

    private void NextScene(PointerEvent events)
    {
        if (events.Type != PointerEventType.Select) return;

        if (currentIndex < scenes.Length - 1)
        {
            currentIndex++;
            UpdateUI();
        }
    }

    public void PreviousScene(PointerEvent events)
    {
        if (events.Type != PointerEventType.Select) return;

        if (currentIndex > 0)
        {
            currentIndex--;
            UpdateUI();
        }
    }

    public void LoadCurrentScene(PointerEvent events)
    {
        if (events.Type != PointerEventType.Select) return;

        Destroy(networkManager);
        SceneManager.LoadScene(scenes[currentIndex].GetSceneName);
    }
}