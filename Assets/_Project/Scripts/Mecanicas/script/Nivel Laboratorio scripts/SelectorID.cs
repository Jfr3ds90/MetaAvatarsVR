using UnityEngine;

public class SelectorID : MonoBehaviour
{
    public int pos;
    public bool selected,righObjects;
    private void Awake()
    {
      //  FindAnyObjectByType<Puzzle01>().objectItem.Add(pos,this.gameObject);
        //gameObject.SetActive(false);
    }
    private void OnDestroy()
    {
      //  FindAnyObjectByType<Puzzle01>().objectItem.Remove(pos);
    }
}
