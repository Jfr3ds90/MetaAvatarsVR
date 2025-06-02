using UnityEngine;
using UnityEngine.Events;
using TMPro;
using System;
using System.Collections.Generic;

public class TimerController : MonoBehaviour
{
    [Header("Instance")]
    public static TimerController Instance;
    
    [Header("Timer Settings")]
    [SerializeField] private float startTimeSeconds = 300f; // 5:00
    private float currentTime;
    [SerializeField] private bool _autoStartTimer = false;

    [Header("UI")]
    [SerializeField] private List<TextMeshProUGUI> textDisplays;

    [Header("Events")]
    [SerializeField] private List<CountdownEvent> timedEvents;
    
    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }

        if (_autoStartTimer)
        {
            currentTime = startTimeSeconds;
        }
    }

    public void OnStartTimer()
    {
        currentTime = startTimeSeconds;
    }

    private void Update()
    {
        if (currentTime <= 0f) return;

        currentTime -= Time.deltaTime;
        currentTime = Mathf.Max(currentTime, 0f);

        UpdateTextDisplays();
        CheckEvents();
    }

    private void UpdateTextDisplays()
    {
        TimeSpan time = TimeSpan.FromSeconds(currentTime);
        string formatted = string.Format("{0:00}:{1:00}", time.Minutes, time.Seconds);

        foreach (var tmp in textDisplays)
        {
            if (tmp != null)
                tmp.text = formatted;
        }
    }

    private void CheckEvents()
    {
        foreach (var e in timedEvents)
        {
            if (!e.hasFired && currentTime <= e.triggerTimeSeconds)
            {
                e.onTrigger?.Invoke();
                e.hasFired = true;
            }
        }
    }

    public void ResetTimer()
    {
        currentTime = startTimeSeconds;
        foreach (var e in timedEvents)
            e.hasFired = false;
    }

    public float GetCurrentTimeSeconds() => currentTime;
}

[Serializable]
public class CountdownEvent
{
    public string label;               
    public float triggerTimeSeconds;  
    public UnityEvent onTrigger;
    [HideInInspector] public bool hasFired = false;
}