using UnityEngine;

public class SelectorID : MonoBehaviour
{
    public Vector2 pos;
    public bool selected,righObjects;
    private void Awake()
    {
        FindAnyObjectByType<Puzzle01>().objectItem.Add(pos,this.gameObject);
    }
    private void OnDestroy()
    {
        FindAnyObjectByType<Puzzle01>().objectItem.Remove(pos);
    }
}
