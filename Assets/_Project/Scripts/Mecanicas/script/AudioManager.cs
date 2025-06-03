using Fusion.Photon.Realtime;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

public class AudioManager : MonoBehaviour
{
    public AudioClip[] Narrador;
    AudioSource source;
    public int FindAnimals = 0,FailLevers=0;
    public float timer;
    //RevealingEffect revealingEffect; 
    bool extra1= false, extra2 = false, extra3= false;
    public bool usedL = false, FindLevers=false,colide=false;
    public int action,moreAction;//reutilizar
    public int ActualPhase = 0;
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
        source.Play(); //1 al 9 fase 1|10 al 14 fase 2|15 al 23 fase 3|24 al 29 fase 4|30 al 35 fase 5
        //fase 4 |24 al ser agarrado una llave| 25 (ya esta) entrar a la salas separadas | 26 tiempo afk| 27 colocarse frente a la pantalla | 28 figura hecha | 29 inmediatamente tras el 28
        //fase 5 |30 al abrirse la caja fuerte| 31 agarrar el pendrive| 32 iniciar ventana de selección (aun no implementado mecanica)| 33 tras hacer el primer envio (aun no implementado mecanica)| 34 tras lograrlo a la primer (aun no implementado mecanica)| 35 tras lograrlo habiendo fallado al menos una vez (aun no implementado mecanica)
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
        switch(ActualPhase)//cada vez que pase mucho tiempo sin interacción...
        {
            case 0:
                timer += Time.deltaTime;
                if (timer >= 30)
                    if (FindAnimals >= 1 && extra2 == false)
                        if (extra1 == false)
                        { StartCoroutine(NarratorLines(4, 2)); extra1 = true; timer = 0; }
                        else if (extra3 == false)
                        { StartCoroutine(NarratorLines(7, 4)); extra3 = true; timer = 0; }
                        else { }
                break;
            case 1:
                timer += Time.deltaTime;
                if (timer >= 30)
                    switch (action)
                    {
                        case 1: break;
                        case 2:
                            StartCoroutine(NarratorLines(4, 12)); timer = 0;//tras ver los libros
                            break;
                        case 3:
                            StartCoroutine(NarratorLines(4, 14)); timer = 0;//Despues de ver el DO
                            break;
                        default: break;
                    }
                break;
            case 2:
                timer += Time.deltaTime;
                if (timer >= 30) 
                {
                    StartCoroutine(NarratorLines(2, 18));//opcion a de mucho tiempo sin hacer nada
                    StartCoroutine(NarratorLines(4, 22));//opcion b de mucho tiempo sin hacer nada
                    StartCoroutine(NarratorLines(5, 23));//opcion c de mucho tiempo sin hacer nada
                    timer = 0;
                }
                    break;
            case 3:
                timer += Time.deltaTime;
                if (timer >= 30) { }
                break;
            case 4:
                timer += Time.deltaTime;
                if (timer >= 30) { }
                break;
            case 5:
                timer += Time.deltaTime;
                if (timer >= 30) { }
                break;
        }    
    }
    public void calls()
    {
        timer = 0;
        switch (ActualPhase)
        {
            case 0: //interior fase 1
                if (FailLevers == 1 && usedL == true)//equivocarse en palancas
                    { StartCoroutine(NarratorLines(7, 6)); }

                else if (FailLevers == 6 && usedL == true)//equivocarse en palancas
                    { StartCoroutine(NarratorLines(5, 7)); }

                if (colide == false)//sin colision
                    {
               
                    }
                else//con colision
                    {
                if (usedL == false)//encuentra palancas
                { StartCoroutine(NarratorLines(6, 5)); usedL = true; }
                    }
                    break;
            case 1://fase una completada e interior fase 2 desde que se abre la puerta
                if (colide == false)//sin colision
                {
                    StartCoroutine(NarratorLines(6, 8)); //se abre la puerta
                }
                else//con colision
                {
                    switch (action)
                    {
                        case 1: StartCoroutine(NarratorLines(4, 10));/*ver arte*/break;
                        case 2: StartCoroutine(NarratorLines(3, 11));/*acercarse a los libros*/break;
                        case 3: StartCoroutine(NarratorLines(4, 13));/*al ver el Do*/break;
                        case 0: StartCoroutine(NarratorLines(6, 9)); /*pasar a la fase 2*/ break;
                        default: break;
                    }
                }
                break;
            case 2://fase 2 completada e interior fase 3 desde que se abre la puerta|15 al 23 | 18 y 19 al transicionar de fase
                if (colide == false)//sin colision
                {
                    switch(moreAction)//REVISAR
                    {
                        case 0:
                            StartCoroutine(NarratorLines(6, 17)); //accionar la palanca
                            Debug.Log("Termino dialogo 17");

                            StartCoroutine(NarratorLines(6, 20)); //una vez terminado el dialogo 17
                            ActualPhase = 3;
                            moreAction = 0;
                            Debug.Log("fase actual " + ActualPhase +" extra "+ moreAction);
                            break;
                    }
           
                }
                else//con colision
                {
                    switch (action)
                    {
                        case 1:
                            StartCoroutine(NarratorLines(4, 15)); //entrar a la sala 
                            break;
                        case 2:
                            StartCoroutine(NarratorLines(4, 16)); //colocarse en frente de la palanca
                            break;
                        case 3:
                            StartCoroutine(NarratorLines(4, 21)); //acercarse a los baños
                            break;
                    }    
                }
                break;
            case 3://fase 3 completada e interior fase 4 desde que se abre la puerta | 24 a 29
                if (colide == false)//sin colision
                {
                    switch(moreAction)
                    {
                        case 0:
                            StartCoroutine(NarratorLines(4, 24));//una vez se agarra una llave
                            break;
                        case 1:
                            StartCoroutine(NarratorLines(4, 26));//inmediatamente depues del dialogo de entrar
                            break;
                        case 2:
                            StartCoroutine(NarratorLines(3, 28));//una vez hecho la figura
                            moreAction += 1;
                            break;
                        case 3:
                            StartCoroutine(NarratorLines(4, 29));//inmediatamente depues del dialogo anterior
                            break;
                    }            
                }
                else//con colision
                {
                    if (action == 0)
                    {
                        StartCoroutine(NarratorLines(4, 25));//en cuanto entran a una de las 2 salas
                        //float timer = 0;
                        //timer += Time.deltaTime;
                        //Debug.Log(timer);
                        //if(timer >= 4)
                            moreAction = 1;
                        
                    }
                }
                break;
            case 4://fase 4 completada e interior fase 5 desde que se abre la puerta | 30 al 35
                if (colide == false)//sin colision
                {
                    switch (moreAction)
                    {
                        case 0:
                            StartCoroutine(NarratorLines(5, 30));//una vez abierto por completo la caja fuerte
                            break;
                        case 1:
                            StartCoroutine(NarratorLines(3, 31));//ya agarrado el USB
                            break;
                        case 2:
                            StartCoroutine(NarratorLines(4, 33));//al enviar el archivo por primera vez (luego toca esperar que cargue para ver si se logro o no)
                            break;
                        case 3:
                            StartCoroutine(NarratorLines(5, 34));//tras lograrlo al primer intento
                            break;
                        case 4:
                            StartCoroutine(NarratorLines(3, 35));//tras lograrlo habiendo fallado por lo menos 1 vez
                            break;
                    }             
                }
                else//con colision
                {
                    if(action == 0)
                    StartCoroutine(NarratorLines(3, 32));//al acercarse a los computadores de nuevo (ya encendidos)
                }
                break;
            case 5://fase 5 completada
                if (colide == false)//sin colision
                {

                }
                else//con colision
                {

                }
                break;
        }   
    
    }
}
