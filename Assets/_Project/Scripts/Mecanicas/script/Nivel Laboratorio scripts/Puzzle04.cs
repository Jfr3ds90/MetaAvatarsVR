using UnityEngine;

public class Puzzle04 : MonoBehaviour
{
    public Sprite[] Kimbo,Laberynth;
    public GameObject pos, instrctions,kimbo;

    private void Update()
    {
        if (Input.GetKey(KeyCode.LeftArrow)) LMove(true); else LMove(false);
       // if (Input.GetKey(KeyCode.RightArrow)) RMove(true); else RMove(false);
       // if (Input.GetKey(KeyCode.UpArrow)) UMove(true); else UMove(false);
       // if (Input.GetKey(KeyCode.DownArrow)) DMove(true); else DMove(false);
    }
    public void LMove(bool mov)
    {
      var anim = kimbo.GetComponent<Animator>();
        anim.SetFloat("XValue", -1);
        anim.SetBool("Walk", mov);
    }
    public void RMove(bool mov)
    {
        var anim = kimbo.GetComponent<Animator>();
        anim.SetFloat("XValue", 1);
        anim.SetBool("Walk", mov);
    }
    public void DMove(bool mov)
    {
        var anim = kimbo.GetComponent<Animator>();
        anim.SetFloat("YValue", -1);
        anim.SetBool("Walk", mov);
    }
    public void UMove(bool mov)
    {
        var anim = kimbo.GetComponent<Animator>();
        anim.SetFloat("YValue", 1);
        anim.SetBool("Walk", mov);
    }
    public void GButton(bool mov)
    {
        var anim = kimbo.GetComponent<Animator>();
        anim.SetBool("Walk", mov);
    }
}
