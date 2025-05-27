using System;
using UnityEngine;
using UnityEngine.Video;


[RequireComponent(typeof(VideoPlayer))]
public class BaseVideoController : MonoBehaviour
{
    [SerializeField] protected VideoPlayer _videoPlayer;
    [SerializeField] protected Material _videoMaterial;
    [SerializeField] protected Texture2D _staticImage;
    [SerializeField] protected RenderTexture _videoTexture;
    [SerializeField] protected string _videoID;
    [SerializeField] protected string _videoPath;
    //[SerializeField] protected const string URL_BASE = "https://drive.google.com/uc?export=download&id=";
    

    protected void Awake()
    {
        _videoPlayer = GetComponent<VideoPlayer>();
        //_videoPlayer.url = URL_BASE+_videoID;
        _videoPlayer.url = Application.streamingAssetsPath + "/Video/" + _videoPath;
    }

    protected virtual void Start()
    {
        ShowStaticImage();
    }

    protected void PlayVideo()
    {
        _videoMaterial.SetTexture("_BaseMap", _videoTexture);
        _videoPlayer.Play();
    }
    
    protected void StopVideo()
    {
        _videoPlayer.Stop();
        ShowStaticImage();
    }

    protected void ShowStaticImage()
    {
        _videoMaterial.SetTexture("_BaseMap", _staticImage);
    }
}
