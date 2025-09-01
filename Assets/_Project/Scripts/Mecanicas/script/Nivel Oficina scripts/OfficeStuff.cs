using NUnit.Framework;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Video;

public class OfficeStaff : MonoBehaviour
{
    bool videoEnd = false;
    public GameObject CanvasPc, ButtonsCanvas, pendrive, creditsEnd, AmbientSound,leverBox;
    public MeshRenderer MRpc;
    public Material rtVideo;
    public AudioClip OnLight, OffLight;
    [SerializeField] private float z;
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
        if(leverBox.transform.rotation.z >= 0.6f)
        {
                AmbientSound.GetComponent<AudioSource>().clip = OffLight;
            AmbientSound.GetComponent<AudioSource>().Play();
        }
        else if(leverBox.transform.rotation.z! <= 0.4f)
        {
            AmbientSound.GetComponent<AudioSource>().clip = OnLight;
            AmbientSound.GetComponent<AudioSource>().Play();
        }
        z = leverBox.transform.rotation.z;
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
