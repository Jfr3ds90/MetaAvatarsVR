using UnityEngine;

public class FallFix : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        if (transform.position.y < -1f)
        {
            transform.position = new Vector3(transform.position.x, 3, transform.position.z);
        }
    }
}
