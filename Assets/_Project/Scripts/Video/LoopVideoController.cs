using UnityEngine;

public class LoopVideoController : BaseVideoController
{
   protected override void Start()
   {
      base.Start();
      _videoPlayer.isLooping = true;
      PlayVideo();
   }
}
