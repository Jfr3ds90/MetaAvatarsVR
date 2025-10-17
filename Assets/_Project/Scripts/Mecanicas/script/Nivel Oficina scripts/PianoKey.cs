using UnityEngine;
using HackMonkeys.Debugging;

public class PianoKey : MonoBehaviour
{
    public int KeyNote;
    Piano piano;
    private void OnEnable()
    {
       piano = FindAnyObjectByType<Piano>();
    }
    private void OnTriggerEnter(Collider other)
    {
        AdvancedDebugSystem.Log(other.transform.name, LogCategory.Avatar, LogLevel.Debug);
        piano.partiture(KeyNote);
       // GetComponentInParent<Transform>().rotation = Quaternion.Euler(GetComponentInParent<Transform>().rotation.x +3, 0f, 0f);
    }
    private void OnTriggerExit(Collider other)
    {
       // GetComponentInParent<Transform>().rotation = Quaternion.Euler(0, 0f, 0f);
    }
}

