using UnityEngine;

public class Puzzle04 : MonoBehaviour
{
    public Sprite[] Kimbo,Laberynth;
    public GameObject pos, instrctions,kimbo;

    private void Update()
    {
        if (Input.GetKeyUp(KeyCode.LeftArrow)) LMove(false);
        if (Input.GetKeyUp(KeyCode.RightArrow)) RMove(false);
        if (Input.GetKeyUp(KeyCode.UpArrow)) UMove(false);
        if (Input.GetKeyUp(KeyCode.DownArrow)) DMove(false);
    }
    public void LMove(bool mov)
    {
      var anim = kimbo.GetComponent<Animator>();
        anim.SetBool("Left", mov);
        anim.SetBool("Walk", mov);
    }
    public void RMove(bool mov)
    {
        var anim = kimbo.GetComponent<Animator>();
        anim.SetBool("Right", mov);
        anim.SetBool("Walk", mov);
    }
    public void DMove(bool mov)
    {
        var anim = kimbo.GetComponent<Animator>();
        anim.SetBool("Down", mov);
        anim.SetBool("Walk", mov);
    }
    public void UMove(bool mov)
    {
        var anim = kimbo.GetComponent<Animator>();
        anim.SetBool("Up", mov);
        anim.SetBool("Walk", mov);
    }
    public void GButton(bool mov)
    {
        var anim = kimbo.GetComponent<Animator>();
        anim.SetBool("Walk", mov);
    }
}
