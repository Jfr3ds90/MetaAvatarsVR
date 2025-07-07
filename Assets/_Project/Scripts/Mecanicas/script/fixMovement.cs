using UnityEngine;

public class fixMovement : MonoBehaviour
{
    [SerializeField] GameObject locomotor;
    private void OnEnable()
    {
        locomotor.SetActive(false);
        locomotor.SetActive(true);
    }
}
