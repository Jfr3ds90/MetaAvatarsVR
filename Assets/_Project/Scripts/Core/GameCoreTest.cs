using UnityEngine;
using HackMonkeys.Core;

public class GameCoreTest : MonoBehaviour
{
    void Start()
    {
        // Suscribirse a eventos
        GameCore.OnStateChanged += OnGameStateChanged;
        GameCore.OnLoadingProgress += OnLoadingProgress;
        GameCore.OnMatchStarted += OnMatchStarted;
        GameCore.OnMatchEnded += OnMatchEnded;
    }
    
    void OnGameStateChanged(GameCore.GameState oldState, GameCore.GameState newState)
    {
        Debug.Log($"[GameCoreTest] State changed: {oldState} → {newState}");
    }
    
    void OnLoadingProgress(float progress)
    {
        Debug.Log($"[GameCoreTest] Loading: {progress:P0}");
    }
    
    void OnMatchStarted()
    {
        Debug.Log("[GameCoreTest] 🎮 Match started!");
    }
    
    void OnMatchEnded()
    {
        Debug.Log("[GameCoreTest] 🏁 Match ended!");
    }
    
    void OnDestroy()
    {
        // Desuscribirse
        GameCore.OnStateChanged -= OnGameStateChanged;
        GameCore.OnLoadingProgress -= OnLoadingProgress;
        GameCore.OnMatchStarted -= OnMatchStarted;
        GameCore.OnMatchEnded -= OnMatchEnded;
    }
}