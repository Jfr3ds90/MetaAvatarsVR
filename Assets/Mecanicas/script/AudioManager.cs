using System.Collections;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

public class AudioManager : MonoBehaviour
{
    public AudioClip[] Narrador;
    AudioSource source;
    public int FindAnimals = 0, FindLevers = 0, FailLevers=0;
    public float timer;
    RevealingEffect revealingEffect; 
    public bool usedA= false, usedL=false, levers = false;
    public bool Phase1=false;
    private void OnEnable()
    {
        source = GetComponent<AudioSource>();
        revealingEffect = FindAnyObjectByType<RevealingEffect>();
    }
    IEnumerator NarratorLines(int tiempo,int objetos)
    {
        timer = 0; source.clip = Narrador[objetos - 1]; source.Play(); 
        yield return new WaitForSeconds(tiempo);
    }
    public void NarratorLines()
    {  
        switch (FindAnimals)
        {
            case 0: break;
            case 1: StartCoroutine(NarratorLines(8,1)); break;
            case 2: break;
            case 3: StartCoroutine(NarratorLines(8, 3)); break;
            case 4: break;
            case 5: break;
            default: break;
        }
        switch(FindLevers)
        {
            case 0: break;
            case 1: StartCoroutine(NarratorLines(8, 5)); break;
            case 2: break;
            case 3: break;
            case 4: break;
            case 5: break;
            default: break;
        }
        switch (FailLevers)
        {
            case 0: break;
            case 1: StartCoroutine(NarratorLines(7, 6)); break;
            case 2: break;
            case 3: break;
            case 4: break;
            case 5: break;
            case 6: StartCoroutine(NarratorLines(5, 7)); break;
            default: break;
        }
    }
    private void Update()
    {       
        timer += Time.deltaTime;
        if (timer >= 30)
            if (FindAnimals >= 1 && levers == false)
                if (usedA == false)
                { StartCoroutine(NarratorLines(5, 2)); usedA = true; }
                else
                { StartCoroutine(NarratorLines(7, 4)); }
            else { }
            
        if (FindLevers == 1 && usedL == false)
            { StartCoroutine(NarratorLines(8, 5)); usedL = true; }
            
        else if (FailLevers == 1 && usedL == true)
            { StartCoroutine(NarratorLines(7, 6)); }
            
        else if (FailLevers == 6 && usedL == true)
            { StartCoroutine(NarratorLines(5, 7)); }
            
        if (Phase1 == true)
            { StartCoroutine(NarratorLines(6, 8)); StartCoroutine(NarratorLines(6, 9)); }
    }
}
