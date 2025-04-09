using UnityEngine;

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
        Debug.Log(other.transform.name);
        piano.partiture(KeyNote);
    }
}

