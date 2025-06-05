using UnityEngine;

public class WristMenuActivator : MonoBehaviour
{
    [SerializeField] private Transform head;            
    [SerializeField] private Transform wristMenu;       
    [SerializeField] private GameObject menuUI;         
    [SerializeField, Range(-1f, 1f)] private float activationThreshold = -0.85f;
    [SerializeField] private float checkInterval = 0.1f;

    private float timer;

    private void Update()
    {
        timer += Time.deltaTime;
        if (timer >= checkInterval)
        {
            timer = 0f;
            CheckGazeToWrist();
        }
    }

    private void CheckGazeToWrist()
    {
        Vector3 headForward = head.forward.normalized;
        Vector3 wristForward = wristMenu.forward.normalized;

        float dot = Vector3.Dot(headForward, wristForward);
        

        bool shouldShow = dot >= activationThreshold;

        if (menuUI.activeSelf != shouldShow)
            menuUI.SetActive(shouldShow);
    }
}