using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class Computer : MonoBehaviour
{
    public TMP_Text text;
    int chances = 0;
    public void mouseSelection(int value)
    {
        switch(value)
        {
            case 1:text.text = "dise�o_v2_FINAL_FINAL_REAL.entregable.zip";
                 new WaitForSeconds(5);
                text.text = "Archivo correcto. Proceso de entrega iniciado.\r\n Productividad alcanzada. Felicitaciones, esclavos del caf�."; 
                break;
            case 2:
                text.text = "PROYECTO_definitivo_DE_VERDAD_esta_es_la_buena_FINALv3(ahora s�).zip";
                new WaitForSeconds(5);
                text.text = "Proyecto definitivo... versi�n 3. Cl�sico. �Desea tambi�n enviar la versi�n 4.0 beta no aprobada por nadie?";
                chances += 1;
                break;
            case 3:
                text.text = "render_entrega_OK_FINAL_FINALAHORA_S�_editado_noche_�ltima_version_FINAL_FINAL.pdf";
                new WaitForSeconds(5);
                text.text = "Detecto signos de desesperaci�n en el nombre del archivo. �Seguro que no falta un �FINAL� m�s?";
                chances += 1;
                break;
            case 4:text.text = "Dise�oFinal2021_borrador.pdf";
                new WaitForSeconds(5);
                text.text = "Un archivo de hace cuatro a�os, en baja resoluci�n. Visionario� o perezoso.";
                chances += 1;
                break;
            case 5:text.text = "NO_USAR_ESTO_viejo.png";
                new WaitForSeconds(5);
                text.text = "�NO_USAR_ESTO_viejo.png? Literalmente lo dice el nombre.";
                chances += 1;
                break;
            case 6:text.text = "final-final-FINAL(backup)_ok.jpg";
                new WaitForSeconds(5);
                text.text = "�Un .jpg para una entrega oficial? Por favor�";
                chances += 1;
                break;
            case 7:text.text = "Dise�o_karen_corregido_copia_copia.ai";
                new WaitForSeconds(5);
                text.text = "Karen ya no trabaja aqu�.";
                chances += 1;
                break;
        }
        if (chances >= 3)
            Debug.Log("se debe de colocar indicativo de que fallo");
    }
}
