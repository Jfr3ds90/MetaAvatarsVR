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
        /*if (Input.GetKeyUp(KeyCode.L))
        {
            mainFigure.AddCoord(xValue,yValue,zValue, scannedTimes);mainFigure.CheckR();
           for (int i = 0; i < mainFigure.sizeExample.Count; i++)
                Debug.Log("coordenada guardada " + mainFigure.sizeExample[i]);
        }
        if (Input.GetKeyUp(KeyCode.R))
            Check();*/
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
            Debug.Log("esta en: "+i);
        }
        Debug.Log("Al final es "+existed+" tambien el ultimo valor es "+Lastvalue);
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
                GetComponent<Rigidbody>().isKinematic = true;
                location = new Vector3( xValue,yValue,zValue);
                mainFigure.CheckR();
            }
        }
        Debug.DrawRay(transform.position, transform.up, Color.green, 1f);
        Debug.DrawRay(transform.position, -transform.up, Color.red, 1f);
        Debug.DrawRay(transform.position, transform.right, Color.blue, 1f);
        Debug.DrawRay(transform.position, -transform.right, Color.yellow, 1f);
        Debug.DrawRay(transform.position, transform.forward, Color.magenta, 1f);
        Debug.DrawRay(transform.position, -transform.forward, Color.gray, 1f);

        if (Physics.Raycast(transform.position, transform.up, out hit, 0.3f) && hit.collider.GetComponent<CubeFigure>() != null)
        {   
            hit.collider.GetComponent<Transform>().rotation = transform.rotation;
            hit.collider.GetComponent<Transform>().position = transform.position + new Vector3(0f, 0.2f, 0f);
            hit.collider.GetComponent<CubeFigure>().yValue++;
            
            location += new Vector3(0, hit.collider.GetComponent<CubeFigure>().location.y, 0);
            hit.collider.GetComponent<CubeFigure>().scannedTimes++;//para saber las veces que fue tocado por el raycast
            Debug.Log("Deteccion arriba");
        }
        if (Physics.Raycast(transform.position, transform.right, out hit, 0.3f) && hit.collider.GetComponent<CubeFigure>() != null)
        {           
            hit.collider.GetComponent<Transform>().rotation = transform.rotation;
            hit.collider.GetComponent<Transform>().position = transform.position + new Vector3(0.2f, 0f, 0f);
            hit.collider.GetComponent<CubeFigure>().xValue++;
            
             location+= new Vector3(hit.collider.GetComponent<CubeFigure>().location.x, 0, 0);
            hit.collider.GetComponent<CubeFigure>().scannedTimes++;//para saber las veces que fue tocado por el raycast
            Debug.Log("Deteccion derecha");
        }

        if (Physics.Raycast(transform.position, transform.forward, out hit, 0.3f) && hit.collider.GetComponent<CubeFigure>() != null)
        {
            hit.collider.GetComponent<Transform>().rotation = transform.rotation;
            hit.collider.GetComponent<Transform>().position = transform.position + new Vector3(0f, 0f, 0.2f);
            hit.collider.GetComponent<CubeFigure>().zValue++;
            
            location += new Vector3(0, 0, hit.collider.GetComponent<CubeFigure>().location.z);
            hit.collider.GetComponent<CubeFigure>().scannedTimes++;//para saber las veces que fue tocado por el raycast
            Debug.Log("Deteccion frente");
        }

        if (Physics.Raycast(transform.position, -transform.up, out hit, 0.3f) && hit.collider.GetComponent<CubeFigure>() != null)
        {
            hit.collider.GetComponent<Transform>().rotation = transform.rotation;
            hit.collider.GetComponent<Transform>().position = transform.position + new Vector3(0f, -0.2f, 0f);
            hit.collider.GetComponent<CubeFigure>().yValue--;
            
            hit.collider.GetComponent<CubeFigure>().location += new Vector3(0, location.y, 0);
            location += new Vector3(0, hit.collider.GetComponent<CubeFigure>().location.y, 0);
            hit.collider.GetComponent<CubeFigure>().scannedTimes++;//para saber las veces que fue tocado por el raycast
            Debug.Log("Deteccion abajo");
        }

        if (Physics.Raycast(transform.position, -transform.right, out hit, 0.3f) && hit.collider.GetComponent<CubeFigure>() != null)
        {          
            hit.collider.GetComponent<Transform>().rotation = transform.rotation;
            hit.collider.GetComponent<Transform>().position = transform.position + new Vector3(-0.2f, 0f, 0f);
            hit.collider.GetComponent<CubeFigure>().xValue--;
           
            hit.collider.GetComponent<CubeFigure>().location += new Vector3(location.x, 0, 0);
            location += new Vector3(hit.collider.GetComponent<CubeFigure>().location.x,0, 0);
            hit.collider.GetComponent<CubeFigure>().scannedTimes++;//para saber las veces que fue tocado por el raycast
            Debug.Log("Deteccion izquierda");
        }
        if (Physics.Raycast(transform.position, -transform.forward, out hit, 0.3f) && hit.collider.GetComponent<CubeFigure>() != null)
        {
            hit.collider.GetComponent<Transform>().rotation = transform.rotation;
            hit.collider.GetComponent<Transform>().position = transform.position + new Vector3(0f, 0f, -0.2f);
            hit.collider.GetComponent<CubeFigure>().zValue--;
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
        {   
            hit.collider.GetComponent<Transform>().rotation = transform.rotation ;
            hit.collider.GetComponent<Transform>().position = transform.position + new Vector3(0f,0.2f,0f) ;
            hit.collider.GetComponent<CubeFigure>().yValue++;
            hit.collider.GetComponent<CubeFigure>().location += new Vector3(0,location.y,0);
            hit.collider.GetComponent<CubeFigure>().scannedTimes++;//para saber las veces que fue tocado por el raycast
        }

        if (Physics.Raycast(transform.position, transform.right, out hit, 0.3f) && hit.collider.GetComponent<CubeFigure>() != null)
        {           
            hit.collider.GetComponent<Transform>().rotation = transform.rotation;
            hit.collider.GetComponent<Transform>().position = transform.position + new Vector3(0.2f, 0f, 0f);
            hit.collider.GetComponent<CubeFigure>().xValue++;
            hit.collider.GetComponent<CubeFigure>().location += new Vector3(location.x, 0, 0);
            hit.collider.GetComponent<CubeFigure>().scannedTimes++;//para saber las veces que fue tocado por el raycast

        }

        if (Physics.Raycast(transform.position, transform.forward, out hit, 0.3f) && hit.collider.GetComponent<CubeFigure>() != null)
        {
            hit.collider.GetComponent<Transform>().rotation = transform.rotation;
            hit.collider.GetComponent<Transform>().position = transform.position + new Vector3(0f, 0f, 0.2f);
            hit.collider.GetComponent<CubeFigure>().zValue++;
            hit.collider.GetComponent<CubeFigure>().location += new Vector3(0, 0, location.z);
            hit.collider.GetComponent<CubeFigure>().scannedTimes++;//para saber las veces que fue tocado por el raycast
        }


        if (Physics.Raycast(transform.position, -transform.up, out hit, 0.3f) && hit.collider.GetComponent<CubeFigure>() != null)
        {
            hit.collider.GetComponent<Transform>().rotation = transform.rotation;
            hit.collider.GetComponent<Transform>().position = transform.position + new Vector3(0f, -0.2f, 0f);
            hit.collider.GetComponent<CubeFigure>().yValue--;
            hit.collider.GetComponent<CubeFigure>().location += new Vector3(0, location.y, 0);
            hit.collider.GetComponent<CubeFigure>().scannedTimes++;//para saber las veces que fue tocado por el raycast
        }

        if (Physics.Raycast(transform.position, -transform.right, out hit, 0.3f) && hit.collider.GetComponent<CubeFigure>() != null)
        {
            hit.collider.GetComponent<Transform>().rotation = transform.rotation;
            hit.collider.GetComponent<Transform>().position = transform.position + new Vector3(-0.2f, 0f, 0f);
            hit.collider.GetComponent<CubeFigure>().xValue--;
            hit.collider.GetComponent<CubeFigure>().location += new Vector3(location.x, 0, 0);
            hit.collider.GetComponent<CubeFigure>().scannedTimes++;//para saber las veces que fue tocado por el raycast
        }

        if (Physics.Raycast(transform.position, -transform.forward, out hit, 0.3f) && hit.collider.GetComponent<CubeFigure>() != null)
        {
            hit.collider.GetComponent<Transform>().rotation = transform.rotation;
            hit.collider.GetComponent<Transform>().position = transform.position + new Vector3(0f, 0f, -0.2f);
            hit.collider.GetComponent<CubeFigure>().zValue--;
            hit.collider.GetComponent<CubeFigure>().location += new Vector3(0, 0, location.z);
            hit.collider.GetComponent<CubeFigure>().scannedTimes++;//para saber las veces que fue tocado por el raycast

        }
        scanned = true;
        Debug.Log(this+" esta ubicacion en "+location);
    }
}
