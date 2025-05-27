using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Video;

public class TriggerVideoController : BaseVideoController
{
    private bool _isIntrigger = false;
    private float _timeToWait = 5f;

    private void Start()
    {
        _videoPlayer.loopPointReached += OnVideoFinished;
    }

    private void OnVideoFinished(VideoPlayer source)
    {
        if (_isIntrigger)
        {
            StartCoroutine(ResetVideo());
        }
    }

    private IEnumerator ResetVideo()
    {
        StopVideo();
        yield return new WaitForSeconds(_timeToWait);
        PlayVideo();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            _isIntrigger = true;
            PlayVideo();
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            _isIntrigger = false;
            StopVideo();
        }
    }
}
