using UnityEngine;
using System;

public class PianoKey : MonoBehaviour
{
    public int KeyNote;
    Piano piano;
    private void OnEnable()
    {
       piano = FindAnyObjectByType<Piano>();
    }
    private void OnCollisionEnter(Collision collision)
    {
        piano.partiture(KeyNote);
    }
}

