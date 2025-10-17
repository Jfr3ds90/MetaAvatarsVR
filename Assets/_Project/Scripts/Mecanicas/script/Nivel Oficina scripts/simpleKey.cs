using System;
using UnityEngine;
using UnityEngine.Video;
using HackMonkeys.Debugging;

public class simpleKey : MonoBehaviour
{
    public GameObject gameobjectInteractor,creditsEnd;
    public bool right,pendrive,audioHeared ;
    public int phase, MAction;
    [HideInInspector]public bool videoCorrect = false;
    [SerializeField] Material matVideo;

    bool canvasActivated = false;
    public void actionKey()
    {
      var animator = gameobjectInteractor.GetComponent<Animator>();
        //var sound = door.GetComponent<AudioSource>();
        if(right == true)
        {
         animator.SetBool("Close_", false);
        animator.SetBool("Right_", right);
        animator.SetTrigger("Activation_");
        }
    }
    private void OnCollisionEnter(Collision other)
    {
        AdvancedDebugSystem.Log("detecto", LogCategory.Avatar, LogLevel.Debug);
        if (pendrive == false)        
            if(other.gameObject.name == "Mesh_Door_02 (7)"&& right == other.gameObject.GetComponent<Switch>().orientation 
            || other.gameObject.name == "Mesh_Door_02 (1)" && right == other.gameObject.GetComponent<Switch>().orientation)
            {
            other.gameObject.GetComponent<Switch>().OpenDoorAct();
            AdvancedDebugSystem.Log(other.gameObject+" detectado", LogCategory.Avatar, LogLevel.Debug);
                {
                AdvancedDebugSystem.Log("funciona!!", LogCategory.Avatar, LogLevel.Debug);
              actionKey();
                }
            }
              
    }
    private void OnTriggerStay(Collider other)
    {           
        if (pendrive == true&&other.GetComponent<AreaDetectorAudio>().phase==4 && other.GetComponent<AreaDetectorAudio>().extra== 2)//arreglar        
            if (other.GetComponent<AreaDetectorAudio>().phase==4)
            {
                AdvancedDebugSystem.Log(name+" detecto al objeto "+other.name, LogCategory.Avatar, LogLevel.Debug);
                if(canvasActivated==false)
                { FindAnyObjectByType<OfficeStaff>().activationPc(); canvasActivated = true; }
                gameobjectInteractor.GetComponent<MeshRenderer>().materials[1].mainTexture = matVideo.mainTexture;

                if (videoCorrect==true)
                videoActivation(other);

                if (gameobjectInteractor.GetComponent<VideoPlayer>().clip .frameRate>= gameobjectInteractor.GetComponent<VideoPlayer>().clip.length)//revisar
                {  FindAnyObjectByType<AudioManager>().moreAction = 3; FindAnyObjectByType<AudioManager>().calls();FindAnyObjectByType<OfficeStaff>().creditsEnd.SetActive(true); }

                // if (gameobjectInteractor.GetComponent<VideoPlayer>().frame >= Convert.ToInt64(gameobjectInteractor.GetComponent<VideoPlayer>().frameCount))
                // creditsEnd.SetActive(true);
            }      
    }
    public void pickUpAudio()
    {
        if(audioHeared==false)
        {
            FindAnyObjectByType<AudioManager>().moreAction = MAction;
            FindAnyObjectByType<AudioManager>().ActualPhase = phase;
            FindAnyObjectByType<AudioManager>().calls();
                audioHeared = true;
        }
    }
    void videoActivation(Collider other)
    {
        FindAnyObjectByType<AudioManager>().ActualPhase = 5;
        FindAnyObjectByType<AudioManager>().moreAction = 2;
        FindAnyObjectByType<AudioManager>().calls();
        other.GetComponent<AreaDetectorAudio>().activated1 = false;
        //gameobjectInteractor.GetComponent<VideoPlayer>().Play();
        
    }
}
