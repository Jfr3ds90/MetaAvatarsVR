using System.Collections;
using UnityEngine;

public class Puzzle04 : MonoBehaviour
{
    public Sprite[] Kimbo ;
    public GameObject[] Laberynth;
    public GameObject pos, instrctions,kimbo,actualL;
    public Transform StartKimbo;
    public float movValue;
    public int level;
    bool activation,horizontal,positive;

    private void Update()
    {
        //if (Input.GetKeyDown(KeyCode.LeftArrow)) LMove(true); else LMove(false);
        //if (Input.GetKeyDown(KeyCode.RightArrow)) RMove(true); else RMove(false);
        //if (Input.GetKeyDown(KeyCode.UpArrow)) UMove(true); else UMove(false);
        //if (Input.GetKeyDown(KeyCode.DownArrow)) DMove(true); else DMove(false);
        if (activation == true)
            Movement(horizontal,positive);
    }
    public void LMove(bool mov)
    {
        if (instrctions.activeSelf == false)
        {
            var anim = kimbo.GetComponent<Animator>();
            anim.SetBool("Walk", mov);
            if (mov == true)
            {
                movValue = -1f;
                anim.SetFloat("XValue", movValue);
                activation = true; horizontal = true; positive = false;
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
        else if (instrctions.activeSelf == true)
            instrctions.SetActive(false);
    }
    public void RMove(bool mov)
    {
        if (instrctions.activeSelf == false)
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
        else if (instrctions.activeSelf == true)
            instrctions.SetActive(false);
    }
    public void DMove(bool mov)
    {
        if (instrctions.activeSelf == false)
         {   var anim = kimbo.GetComponent<Animator>();
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
        else if (instrctions.activeSelf == true)
            instrctions.SetActive(false);

    }
    public void UMove(bool mov)
    {
        if (instrctions.activeSelf == false)
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
        else if (instrctions.activeSelf == true)
            instrctions.SetActive(false);
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
    public void ChangeLevel()
    {
        Destroy(actualL);
        actualL = Instantiate(Laberynth[level]);
        actualL.transform.SetParent(transform);
        actualL.transform.localPosition = new Vector3(0,0,0);
        actualL.transform.localScale = new Vector3(0.1f, 0.1f, 0.1f);
    }
}
