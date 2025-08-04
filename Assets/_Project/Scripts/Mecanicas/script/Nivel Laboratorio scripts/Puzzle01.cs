using UnityEngine;

public class Puzzle01 : MonoBehaviour
{
    public Sprite[] laberynth;//scale 1.6 pos 0
    public GameObject liquid,pos;
    void Start()
    {
        
    }


    void Update()
    {
       if (Input.GetKeyUp(KeyCode.V))
            Instantiate (liquid,pos.transform.position,pos.transform.rotation);
    }
}
