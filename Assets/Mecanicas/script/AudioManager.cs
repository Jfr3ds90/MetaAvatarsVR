using System.Collections;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

public class AudioManager : MonoBehaviour
{
    public AudioClip[] Narrador;
    AudioSource source;
    public int FindAnimals = 0, /*FindLevers = 0, */FailLevers=0;
    public float timer;
    //RevealingEffect revealingEffect; 
    bool usedA= false, usedL=false, levers = false, extraNarrator= false;
    public bool Phase1=false,Phase2=false,Phase3=false, FindLevers=false,colide=false;
    //int contador=0;
    float temp = 0;
    private void OnEnable()
    {
        source = GetComponent<AudioSource>();
        //revealingEffect = FindAnyObjectByType<RevealingEffect>();
    }
    IEnumerator NarratorLines(int tiempo,int objetos)
    {
        
        timer = 0;
        source.clip = Narrador[objetos - 1];
        source.Play(); //1 al 9 fase 1|10 al 15 fase 2

        yield return new WaitForSeconds(tiempo);
    }
    public void NarratorLinesActivation()
    {  
        switch (FindAnimals)
        {
            case 0: break;
            case 1: StartCoroutine(NarratorLines(7,1)); break;
            case 2: break;
            case 3: StartCoroutine(NarratorLines(8, 3)); break;
            case 4: break;
            case 5: break;
            default: break;
        }
        /*//switch(FindLevers)
        //{
        //    case 0: break;
        //    case 1: StartCoroutine(NarratorLines(8, 5)); break;
        //    case 2: break;
        //    case 3: break;
        //    case 4: break;
        //    case 5: break;
        //    default: break;
        //}*/
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
       if (Phase1 == false)//cada vez que pase mucho tiempo sin interacción...
        {
            timer += Time.deltaTime;
            if (timer >= 30)
                if (FindAnimals >= 1 && levers == false)
                    if (usedA == false)
                    { StartCoroutine(NarratorLines(4, 2)); usedA = true; }
                    else if (extraNarrator == false)
                    { StartCoroutine(NarratorLines(7, 4)); extraNarrator = true; }
                    else { }
                else { }

           
        }
    }
    public void calls()
    {
        if (Phase1 == false)//interior fase 1
        {
            //linea 1 y 3 los hace la linterna
            //linea 2 y 4 los hace por inactividad

            if (FailLevers == 1 && usedL == true)//equivocarse en palancas
            { StartCoroutine(NarratorLines(7, 6)); }

            else if (FailLevers == 6 && usedL == true)//equivocarse en palancas
            { StartCoroutine(NarratorLines(5, 7)); }

            if (colide == false)//sin colision
            {
               
            }
            else//con colision
            {
                if (FindLevers == true && usedL == false)//encuentra palancas
                { StartCoroutine(NarratorLines(6, 5)); usedL = true; }
            }
        }
        else if (Phase1 == true && Phase2==false)//interior fase 2 desde que se abre la puerta
        {
            if (colide == false)//sin colision
            { 
                StartCoroutine(NarratorLines(6, 8)); //se abre la puerta
            }
            else//con colision
            {
                StartCoroutine(NarratorLines(6, 9)); // pasar a la fase 2
            }
        }
        else if (Phase1 == true && Phase2 == true && Phase3 == false)//interior fase 3 desde que se abre la puerta
        {
            if (colide == false)//sin colision
            {

            }
            else//con colision
            {

            }
        }
    }
}
