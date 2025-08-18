using System.Collections;
using UnityEngine;

public class Puzzle04 : MonoBehaviour
{
    public Sprite[] Kimbo,Laberynth;
    public GameObject pos, instrctions,kimbo;
    public float movValue;
    bool activation,horizontal,positive;

    private void Update()
    {
        //if (Input.GetKeyDown(KeyCode.LeftArrow)) StartCoroutine(Movement(true, false, true));// else LMove(false);
        //if (Input.GetKeyDown(KeyCode.RightArrow)) StartCoroutine(Movement(true, true, true));// else RMove(false);
        //if (Input.GetKeyDown(KeyCode.UpArrow)) StartCoroutine(Movement(false, true, true)); //else UMove(false);
        //if (Input.GetKeyDown(KeyCode.DownArrow)) StartCoroutine(Movement(false, false, true));// else DMove(false);
        if (activation == true)
            Movement(horizontal,positive);
    }
    public void LMove(bool mov)
    {
      var anim = kimbo.GetComponent<Animator>();
        anim.SetBool("Walk", mov);
        if(mov==true)
        {
            movValue = -1f;
            anim.SetFloat("XValue", movValue);
            activation = true;horizontal = true;positive=false;
          //  StartCoroutine(Movement(true, false,mov));            
        }
        else if (mov == false)
        {
            movValue = 0f;
            anim.SetFloat("XValue", movValue);
            activation = false; 
            //  StopCoroutine(Movement(true,false,mov));
        }
    }
    public void RMove(bool mov)
    {
        var anim = kimbo.GetComponent<Animator>();
        anim.SetBool("Walk", mov);
        if (mov == true)
        {
            movValue = 1f;
            anim.SetFloat("XValue", movValue);
            activation = true; horizontal = true; positive = true;
            //  StartCoroutine(Movement(true, true, mov));
        }
        else if (mov == false)
        {
            movValue = 0f;
            anim.SetFloat("XValue", movValue);
            activation = false;
            //  StopCoroutine(Movement(true, true, mov));
        }
    }
    public void DMove(bool mov)
    {
        var anim = kimbo.GetComponent<Animator>();
        anim.SetBool("Walk", mov);
        if (mov == true)
        {
            movValue = -1f;
            anim.SetFloat("YValue", movValue);
            activation = true; horizontal = false; positive = false;
            //  StartCoroutine(Movement(false, false, mov));
        }
        else if (mov == false)
        {
            movValue = 0;
            anim.SetFloat("YValue", movValue);
            activation = false;
            // StopCoroutine(Movement(false, false, mov));
        }
    }
    public void UMove(bool mov)
    {
        var anim = kimbo.GetComponent<Animator>();
        anim.SetBool("Walk", mov);
        if (mov == true)
        {
            movValue = 1f;
            anim.SetFloat("YValue", movValue);
            activation = true; horizontal = false; positive = true;
            // StartCoroutine(Movement(false, true, mov));
        }
        else if (mov == false)
        {
            movValue = 0;
            anim.SetFloat("YValue", movValue);
            activation = false;
            // StopCoroutine(Movement(false, true, mov));
        }
    }
    public void GButton(bool mov)
    {
        var anim = kimbo.GetComponent<Animator>();
        anim.SetBool("Walk", mov);
    }
    void Movement(bool x,bool p)
    {
       
        {
            if (x == true)
               { if (p == true)
                    kimbo.transform.position += new Vector3(Time.deltaTime * 0.1f, 0);
                else if (p == false)
                    kimbo.transform.position -= new Vector3(Time.deltaTime * 0.1f, 0);
            }

                else if (x == false)
                   { if (p == true)
                    kimbo.transform.position += new Vector3(0, Time.deltaTime * 0.1f);
                else if (p == false)
                    kimbo.transform.position -= new Vector3(0, Time.deltaTime * 0.1f);
            }
        }
    }
}
