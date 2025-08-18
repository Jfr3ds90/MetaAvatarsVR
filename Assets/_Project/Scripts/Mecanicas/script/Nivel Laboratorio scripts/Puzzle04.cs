using UnityEngine;

public class Puzzle04 : MonoBehaviour
{
    public Sprite[] Kimbo,Laberynth;
    public GameObject pos, instrctions,kimbo;

    private void Update()
    {
        //if (Input.GetKey(KeyCode.LeftArrow)) LMove(true); else LMove(false);
        //if (Input.GetKey(KeyCode.RightArrow)) RMove(true); else RMove(false);
        //if (Input.GetKey(KeyCode.UpArrow)) UMove(true); else UMove(false);
        //if (Input.GetKey(KeyCode.DownArrow)) DMove(true); else DMove(false);
    }
    public void LMove(bool mov)
    {
      var anim = kimbo.GetComponent<Animator>();
        anim.SetBool("Walk", mov);
        if(mov==false)
        {
            anim.SetFloat("XValue", -1);
            kimbo.transform.position += new Vector3(Time.deltaTime * 0.1f, 0);
        }
    }
    public void RMove(bool mov)
    {
        var anim = kimbo.GetComponent<Animator>();
        anim.SetBool("Walk", mov);
        if (mov == false)
        { 
            anim.SetFloat("XValue", 1);
            kimbo.transform.position -= new Vector3(Time.deltaTime * 0.1f, 0);
        }
        
    }
    public void DMove(bool mov)
    {
        var anim = kimbo.GetComponent<Animator>();
        anim.SetBool("Walk", mov);
        if (mov == false)
        {
            anim.SetFloat("YValue", -1);
            kimbo.transform.position += new Vector3(0, Time.deltaTime * 0.1f);
        }
    }
    public void UMove(bool mov)
    {
        var anim = kimbo.GetComponent<Animator>();
        anim.SetBool("Walk", mov);
        if (mov == false)
        {
            anim.SetFloat("YValue", 1);
            kimbo.transform.position -= new Vector3(0, Time.deltaTime * 0.1f);
        }
    }
    public void GButton(bool mov)
    {
        var anim = kimbo.GetComponent<Animator>();
        anim.SetBool("Walk", mov);
    }
}
