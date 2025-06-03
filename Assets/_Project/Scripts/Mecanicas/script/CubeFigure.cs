using ExitGames.Client.Photon.StructWrapping;
using System;
using System.Collections;
using Unity.VisualScripting;
using UnityEngine;

public class CubeFigure : MonoBehaviour
{
    //public static Transform[,,] exampleTiles;
    MainFigure mainFigure;
    public int xValue, yValue,zValue,mxValue,myValue,mzValue;
    int Lastvalue = 0;
    public Vector3 location;
    public bool scanned;
    public int scannedTimes;
    //public static int upCon, downCon, leftCon, rightCon, frontCon, backCon;
    // tecnicamente por ahora servira con cualquier cosa que se forme pero con la cantidad de conexiones correctas
    private void Awake()
    {
        mainFigure = FindAnyObjectByType<MainFigure>();
    }
    void Start()
    {
       // exampleTiles = new Transform[(int)sizeExample.x, (int)sizeExample.y, (int)sizeExample.z];
    }

    // Update is called once per frame
    void Update()
    {
        //GeneralCheck();
        //Check();
        if (Input.GetKeyUp(KeyCode.L))
        {
            mainFigure.AddCoord(xValue,yValue,zValue, scannedTimes);mainFigure.CheckR();
          /* for (int i = 0; i < mainFigure.sizeExample.Count; i++)
                Debug.Log("coordenada guardada " + mainFigure.sizeExample[i]);*/
        }
        if (Input.GetKeyUp(KeyCode.R))
            Check();
    }
    public void GeneralCheck()
    {
        bool existed= false;
        if (!mainFigure.sizeExample.Contains(new Vector3(xValue, yValue, zValue)))
            mainFigure.sizeExample.Add(new Vector3(xValue, yValue, zValue));

        //Debug.Log(mainFigure.sizeExample.Count+" es la cantidad");
        for (int i = 0; i < mainFigure.sizeExample.Count; i++) //reviza si la coordenada existe
        {
                Debug.Log(mainFigure.sizeExample[i].ToString()+" esta en la lista");
                /*if (mainFigure.sizeExample[i]!=new Vector3(xValue,yValue,zValue))
                {
                    Lastvalue = i;
                }
                else if (mainFigure.sizeExample[i] == new Vector3(xValue, yValue, zValue))
                {
                    existed = true; break;
                }*/
            Debug.Log("esta en: "+i);
        }
        /*if (existed == false)
        { 
            Vector3 coord = new Vector3(xValue, yValue, zValue);
            mainFigure.sizeExample[(Lastvalue)] = coord;
            Debug.Log(coord);
        }*/

        Debug.Log("Al final es "+existed+" tambien el ultimo valor es "+Lastvalue);
        //Debug.Log("cantida de piezas conectadas: "+ upCon+" "+ downCon + " " + leftCon + " " + rightCon + " " + frontCon + " " + backCon);
        //Debug.Log("no debiera de aparecer " + example[5,5,5]);
        //if (example[0, 1, 0] && example[1,1,0]& example[2, 1, 0] && example[3, 1, 0]&& example[4, 1, 0] && example[5, 1, 0] &&
        //    example[0, 2, 0] && example[2, 0, 0] && example[3, 1, 1] && example[3, 1, 2] && example[4, 1, 2])
        //pensar en como hacer que entienda que es lo mismo que el de la imagen
        //if (rightCon == 6 && backCon ==1&& frontCon==1&&upCon==2)//5 a la derecha, 1 tambien a la derecha pero arriba,1 atras y adelante y 2 arriba
        //{
        //    //Debug.Log("esto es correcto "+example);
        //    Debug.Log("FIGURA HECHA");
        //}
    }
    public void Check() //revizar contacto desde el objeto manipulado 
    {
        RaycastHit hit;
        //Plan B

        Debug.DrawRay(transform.position, transform.up, Color.green, 1f);
        Debug.DrawRay(transform.position, -transform.up, Color.red, 1f);
        Debug.DrawRay(transform.position, transform.right, Color.blue, 1f);
        Debug.DrawRay(transform.position, -transform.right, Color.yellow, 1f);
        Debug.DrawRay(transform.position, transform.forward, Color.magenta, 1f);
        Debug.DrawRay(transform.position, -transform.forward, Color.gray, 1f);

        if (Physics.Raycast(transform.position, transform.up, out hit, 0.3f)||
            Physics.Raycast(transform.position, transform.right, out hit, 0.3f)||
            Physics.Raycast(transform.position, transform.forward, out hit, 0.3f)||
            Physics.Raycast(transform.position, -transform.up, out hit, 0.3f)||
            Physics.Raycast(transform.position, -transform.right, out hit, 0.3f)||
            Physics.Raycast(transform.position, -transform.forward, out hit, 0.3f))

        {
            
            //Debug.DrawRay(transform.position, transform.up, Color.red, 1f);


            if (hit.collider.GetComponent<CubeFigure>() != null)
            {
                /*hit.collider.*/GetComponent<Rigidbody>().isKinematic = true;
                //if(hit.collider.GetComponent<CubeFigure>().scanned==false)
                //hit.collider.GetComponent<CubeFigure>().CheckBase();
                location = new Vector3(/*hit.collider.GetComponent<CubeFigure>().location.x +*/ xValue,
/*hit.collider.GetComponent<CubeFigure>().location.y +*/ yValue,
/*hit.collider.GetComponent<CubeFigure>().location.z +*/ zValue);
                mainFigure.fastSolution();
            }
        }
        Debug.DrawRay(transform.position, transform.up, Color.green, 1f);
        Debug.DrawRay(transform.position, -transform.up, Color.red, 1f);
        Debug.DrawRay(transform.position, transform.right, Color.blue, 1f);
        Debug.DrawRay(transform.position, -transform.right, Color.yellow, 1f);
        Debug.DrawRay(transform.position, transform.forward, Color.magenta, 1f);
        Debug.DrawRay(transform.position, -transform.forward, Color.gray, 1f);

        if (Physics.Raycast(transform.position, transform.up, out hit, 0.3f) && hit.collider.GetComponent<CubeFigure>() != null)
        {   //example[0, 0, 1] = hit.collider.GetComponent<GameObject>();
            /* Debug.Log(example[0, 0,1] + " parte de la matris");*/
            hit.collider.GetComponent<Transform>().rotation = transform.rotation;
            hit.collider.GetComponent<Transform>().position = transform.position + new Vector3(0f, 0.2f, 0f);
            hit.collider.GetComponent<CubeFigure>().yValue++;
            //hit.collider.GetComponent<CubeFigure>().location += new Vector3(0, location.y, 0);
            location += new Vector3(0, hit.collider.GetComponent<CubeFigure>().location.y, 0);
            hit.collider.GetComponent<CubeFigure>().scannedTimes++;//para saber las veces que fue tocado por el raycast
            Debug.Log("Deteccion arriba");
        }
        //else
        //{ up = false; upCon--; }

        if (Physics.Raycast(transform.position, transform.right, out hit, 0.3f) && hit.collider.GetComponent<CubeFigure>() != null)
        {
            // example[1, 0, 0] = hit.collider.GetComponent<GameObject>();
            /*Debug.Log(example[1, 0, 0] + " parte de la matris");*/
            hit.collider.GetComponent<Transform>().rotation = transform.rotation;
            hit.collider.GetComponent<Transform>().position = transform.position + new Vector3(0.2f, 0f, 0f);
            hit.collider.GetComponent<CubeFigure>().xValue++;
            //hit.collider.GetComponent<CubeFigure>().location += new Vector3(location.x, 0, 0);
             location+= new Vector3(hit.collider.GetComponent<CubeFigure>().location.x, 0, 0);
            hit.collider.GetComponent<CubeFigure>().scannedTimes++;//para saber las veces que fue tocado por el raycast
            Debug.Log("Deteccion derecha");
        }
        //else
        //{ right = false; rightCon--; }

        if (Physics.Raycast(transform.position, transform.forward, out hit, 0.3f) && hit.collider.GetComponent<CubeFigure>() != null)
        {
            //example[0, 1, 0] = hit.collider.GetComponent<GameObject>();
            /*Debug.Log(example[0, 1, 0] + " parte de la matris");*/
            hit.collider.GetComponent<Transform>().rotation = transform.rotation;
            hit.collider.GetComponent<Transform>().position = transform.position + new Vector3(0f, 0f, 0.2f);
            hit.collider.GetComponent<CubeFigure>().zValue++;
            //hit.collider.GetComponent<CubeFigure>().location += new Vector3(0, 0, location.z);
            location += new Vector3(0, 0, hit.collider.GetComponent<CubeFigure>().location.z);
            hit.collider.GetComponent<CubeFigure>().scannedTimes++;//para saber las veces que fue tocado por el raycast
            Debug.Log("Deteccion frente");
        }
        //else
        //{ front = false; frontCon--; }

        if (Physics.Raycast(transform.position, -transform.up, out hit, 0.3f) && hit.collider.GetComponent<CubeFigure>() != null)
        {
            //example[0, 0, -1] = hit.collider.GetComponent<GameObject>(); 
            /*Debug.Log(example[0, 0, -1] + " parte de la matris")*/
            ;
            hit.collider.GetComponent<Transform>().rotation = transform.rotation;
            hit.collider.GetComponent<Transform>().position = transform.position + new Vector3(0f, -0.2f, 0f);
            hit.collider.GetComponent<CubeFigure>().yValue--;
            //hit.collider.GetComponent<CubeFigure>().myValue++;
            hit.collider.GetComponent<CubeFigure>().location += new Vector3(0, location.y, 0);
            location += new Vector3(0, hit.collider.GetComponent<CubeFigure>().location.y, 0);
            hit.collider.GetComponent<CubeFigure>().scannedTimes++;//para saber las veces que fue tocado por el raycast
            Debug.Log("Deteccion abajo");
        }
        //else
        //{ down = false; downCon--; }

        if (Physics.Raycast(transform.position, -transform.right, out hit, 0.3f) && hit.collider.GetComponent<CubeFigure>() != null)
        {
            //example[-1, 0, 0] = hit.collider.GetComponent<GameObject>();
            /*Debug.Log(example[-1, 0, 0] + " parte de la matris");*/
            hit.collider.GetComponent<Transform>().rotation = transform.rotation;
            hit.collider.GetComponent<Transform>().position = transform.position + new Vector3(-0.2f, 0f, 0f);
            hit.collider.GetComponent<CubeFigure>().xValue--;
            //hit.collider.GetComponent<CubeFigure>().mxValue++;
            hit.collider.GetComponent<CubeFigure>().location += new Vector3(location.x, 0, 0);
            location += new Vector3(hit.collider.GetComponent<CubeFigure>().location.x,0, 0);
            hit.collider.GetComponent<CubeFigure>().scannedTimes++;//para saber las veces que fue tocado por el raycast
            Debug.Log("Deteccion izquierda");
        }
        //else
        //{ left = false; leftCon--; }

        if (Physics.Raycast(transform.position, -transform.forward, out hit, 0.3f) && hit.collider.GetComponent<CubeFigure>() != null)
        {
            //example[0, -1, 0] = hit.collider.GetComponent<GameObject>();
            /*Debug.Log(example[0,-1,0] + " parte de la matris");*/
            hit.collider.GetComponent<Transform>().rotation = transform.rotation;
            hit.collider.GetComponent<Transform>().position = transform.position + new Vector3(0f, 0f, -0.2f);
            hit.collider.GetComponent<CubeFigure>().zValue--;
            //hit.collider.GetComponent<CubeFigure>().mzValue++;
            hit.collider.GetComponent<CubeFigure>().location += new Vector3(0, 0, location.z);
            location += new Vector3(0, 0, hit.collider.GetComponent<CubeFigure>().location.z);
            hit.collider.GetComponent<CubeFigure>().scannedTimes++;//para saber las veces que fue tocado por el raycast
            Debug.Log("Deteccion atras");
        }
    }
    public void CheckBase() //revizar contacto desde el que estaba (es mejor asi para que la posicion dependa de este y no sea manipulado al rotarse)
    {
        RaycastHit hit;
        //Plan B

        Debug.DrawRay(transform.position, transform.up, Color.green, 1f);
        Debug.DrawRay(transform.position, -transform.up, Color.red, 1f);
        Debug.DrawRay(transform.position, transform.right, Color.blue, 1f);
        Debug.DrawRay(transform.position, -transform.right, Color.yellow, 1f);
        Debug.DrawRay(transform.position, transform.forward, Color.magenta, 1f);
        Debug.DrawRay(transform.position, -transform.forward, Color.gray, 1f);

        if (Physics.Raycast(transform.position, transform.up, out hit, 0.3f) && hit.collider.GetComponent<CubeFigure>() != null)
        {   //example[0, 0, 1] = hit.collider.GetComponent<GameObject>();
            /* Debug.Log(example[0, 0,1] + " parte de la matris");*/
            hit.collider.GetComponent<Transform>().rotation = transform.rotation ;
            hit.collider.GetComponent<Transform>().position = transform.position + new Vector3(0f,0.2f,0f) ;
            hit.collider.GetComponent<CubeFigure>().yValue++;
            hit.collider.GetComponent<CubeFigure>().location += new Vector3(0,location.y,0);
            hit.collider.GetComponent<CubeFigure>().scannedTimes++;//para saber las veces que fue tocado por el raycast

        }
        //else
        //{ up = false; upCon--; }

        if (Physics.Raycast(transform.position, transform.right, out hit, 0.3f) && hit.collider.GetComponent<CubeFigure>() != null)
        {
            // example[1, 0, 0] = hit.collider.GetComponent<GameObject>();
            /*Debug.Log(example[1, 0, 0] + " parte de la matris");*/
            hit.collider.GetComponent<Transform>().rotation = transform.rotation;
            hit.collider.GetComponent<Transform>().position = transform.position + new Vector3(0.2f, 0f, 0f);
            hit.collider.GetComponent<CubeFigure>().xValue++;
            hit.collider.GetComponent<CubeFigure>().location += new Vector3(location.x, 0, 0);
            hit.collider.GetComponent<CubeFigure>().scannedTimes++;//para saber las veces que fue tocado por el raycast

        }
        //else
        //{ right = false; rightCon--; }

        if (Physics.Raycast(transform.position, transform.forward, out hit, 0.3f) && hit.collider.GetComponent<CubeFigure>() != null)
        {
             //example[0, 1, 0] = hit.collider.GetComponent<GameObject>();
            /*Debug.Log(example[0, 1, 0] + " parte de la matris");*/
            hit.collider.GetComponent<Transform>().rotation = transform.rotation;
            hit.collider.GetComponent<Transform>().position = transform.position + new Vector3(0f, 0f, 0.2f);
            hit.collider.GetComponent<CubeFigure>().zValue++;
            hit.collider.GetComponent<CubeFigure>().location += new Vector3(0, 0, location.z);
            hit.collider.GetComponent<CubeFigure>().scannedTimes++;//para saber las veces que fue tocado por el raycast

        }
        //else
        //{ front = false; frontCon--; }

        if (Physics.Raycast(transform.position, -transform.up, out hit, 0.3f) && hit.collider.GetComponent<CubeFigure>() != null)
        {
            //example[0, 0, -1] = hit.collider.GetComponent<GameObject>(); 
            /*Debug.Log(example[0, 0, -1] + " parte de la matris")*/
            ;
            hit.collider.GetComponent<Transform>().rotation = transform.rotation;
            hit.collider.GetComponent<Transform>().position = transform.position + new Vector3(0f, -0.2f, 0f);
            hit.collider.GetComponent<CubeFigure>().yValue--;
            hit.collider.GetComponent<CubeFigure>().location += new Vector3(0, location.y, 0);
            hit.collider.GetComponent<CubeFigure>().scannedTimes++;//para saber las veces que fue tocado por el raycast

        }
        //else
        //{ down = false; downCon--; }

        if (Physics.Raycast(transform.position, -transform.right, out hit, 0.3f) && hit.collider.GetComponent<CubeFigure>() != null)
        {
            //example[-1, 0, 0] = hit.collider.GetComponent<GameObject>();
            /*Debug.Log(example[-1, 0, 0] + " parte de la matris");*/
            hit.collider.GetComponent<Transform>().rotation = transform.rotation;
            hit.collider.GetComponent<Transform>().position = transform.position + new Vector3(-0.2f, 0f, 0f);
            hit.collider.GetComponent<CubeFigure>().xValue--;
            hit.collider.GetComponent<CubeFigure>().location += new Vector3(location.x, 0, 0);
            hit.collider.GetComponent<CubeFigure>().scannedTimes++;//para saber las veces que fue tocado por el raycast

        }
        //else
        //{ left = false; leftCon--; }

        if (Physics.Raycast(transform.position, -transform.forward, out hit, 0.3f) && hit.collider.GetComponent<CubeFigure>() != null)
        {
            //example[0, -1, 0] = hit.collider.GetComponent<GameObject>();
            /*Debug.Log(example[0,-1,0] + " parte de la matris");*/
            hit.collider.GetComponent<Transform>().rotation = transform.rotation;
            hit.collider.GetComponent<Transform>().position = transform.position + new Vector3(0f, 0f, -0.2f);
            hit.collider.GetComponent<CubeFigure>().zValue--;
            hit.collider.GetComponent<CubeFigure>().location += new Vector3(0, 0, location.z);
            hit.collider.GetComponent<CubeFigure>().scannedTimes++;//para saber las veces que fue tocado por el raycast

        }
        /*if(xValue- hit.collider.GetComponent<CubeFigure>().location.x==0)
        { if (xValue == -1) xValue = 0;
            else hit.collider.GetComponent<CubeFigure>().location.x = 0;
        }
        if(yValue- hit.collider.GetComponent<CubeFigure>().location.y==0)
        { if (yValue == -1) yValue = 0;
            else hit.collider.GetComponent<CubeFigure>().location.y = 0;
        } 
        if(zValue- hit.collider.GetComponent<CubeFigure>().location.z==0)
        { if (zValue == -1) zValue = 0;
            else hit.collider.GetComponent<CubeFigure>().location.z = 0;
        }*/
       

        scanned = true;
        Debug.Log(this+" esta ubicacion en "+location);

    }

}
