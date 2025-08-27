using NUnit.Framework;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Video;

public class OfficeStaff : MonoBehaviour
{
    bool lightsOn = false, videoEnd=false;
    public GameObject lightsObjects, emergencyLights,CanvasPc,ButtonsCanvas,pendrive,creditsEnd;
    public MeshRenderer MRpc;
    public Material rtVideo;

    private void OnEnable()
    {
        CanvasTasksShow.level = "oficina";
    }
    private void Update()
    {
        if (MRpc.GetComponent<VideoPlayer>().length<= MRpc.GetComponent<VideoPlayer>().time && videoEnd==false)
        {
            videoEnd=true;
            MRpc.GetComponent<AudioSource>().Play();
        }
    }
    public void CoffeMachine()
    {
        lightsOn = false;
        lightsObjects.SetActive(false);
        emergencyLights.SetActive(true);

    }
    public void lightBox()
    {
        lightsOn = true;
        lightsObjects.SetActive(true);
        emergencyLights.SetActive(true);
    }
    public void activationPc()
    {
        CanvasPc.SetActive(true);
    }
    public void CorrectOption()
    {
        //creditsEnd.SetActive(true);//sacar cuando el video funcione
        List<Material> lm = new List<Material>();
        lm.Add(MRpc.materials[0]);
        lm.Add(rtVideo);
        MRpc.SetMaterials(lm);
        MRpc.GetComponent<VideoPlayer>().Play();
      //  pendrive.GetComponent<simpleKey>().videoCorrect = true;       
        ButtonsCanvas.SetActive(false);
        creditsEnd.SetActive(true);
        Debug.Log("opcion correcta");
    }
    public void IncorrectOption()
    {
        Debug.Log("opcion incorrecta");
    }
}
