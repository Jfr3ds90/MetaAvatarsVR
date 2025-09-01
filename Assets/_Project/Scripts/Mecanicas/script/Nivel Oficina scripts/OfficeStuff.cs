using NUnit.Framework;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Video;

public class OfficeStaff : MonoBehaviour
{
    bool videoEnd=false;
    public GameObject CanvasPc,ButtonsCanvas,pendrive,creditsEnd,AmbientSound;
    public MeshRenderer MRpc;
    public Material rtVideo;
    public AudioClip OnLight, OffLight;
    private void OnEnable()
    {
        CanvasTasksShow.level = "oficina";
    }
    private void Update()
    {
        if (MRpc.GetComponent<VideoPlayer>().frame== (long)MRpc.GetComponent<VideoPlayer>().frameCount-1 && videoEnd==false)
        {            
            CanvasPc.GetComponent<AudioSource>().Play();
            videoEnd = true;
        }
    }
    public void lightBox()
    {
        if (AmbientSound.GetComponent<AudioSource>().clip == OnLight)
            AmbientSound.GetComponent<AudioSource>().clip = OffLight;
        else if(AmbientSound.GetComponent<AudioSource>().clip==OffLight)
            AmbientSound.GetComponent<AudioSource>().clip = OnLight;

    }
    public void activationPc()
    {
        CanvasPc.SetActive(true);
    }
    public void CorrectOption()
    {
        List<Material> lm = new List<Material>();
        lm.Add(MRpc.materials[0]);
        lm.Add(rtVideo);
        MRpc.SetMaterials(lm);
        MRpc.GetComponent<VideoPlayer>().Play(); 
        ButtonsCanvas.SetActive(false);
        creditsEnd.SetActive(true);
        Debug.Log("opcion correcta");
    }
    public void IncorrectOption()
    {
        Debug.Log("opcion incorrecta");
    }
}
