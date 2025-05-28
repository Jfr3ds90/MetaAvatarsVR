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
            case 1:text.text = "diseño_v2_FINAL_FINAL_REAL.entregable.zip";
                 new WaitForSeconds(5);
                text.text = "Archivo correcto. Proceso de entrega iniciado.\r\n Productividad alcanzada. Felicitaciones, esclavos del café."; 
                break;
            case 2:
                text.text = "PROYECTO_definitivo_DE_VERDAD_esta_es_la_buena_FINALv3(ahora sí).zip";
                new WaitForSeconds(5);
                text.text = "Proyecto definitivo... versión 3. Clásico. ¿Desea también enviar la versión 4.0 beta no aprobada por nadie?";
                chances += 1;
                break;
            case 3:
                text.text = "render_entrega_OK_FINAL_FINALAHORA_SÍ_editado_noche_última_version_FINAL_FINAL.pdf";
                new WaitForSeconds(5);
                text.text = "Detecto signos de desesperación en el nombre del archivo. ¿Seguro que no falta un ‘FINAL’ más?";
                chances += 1;
                break;
            case 4:text.text = "DiseñoFinal2021_borrador.pdf";
                new WaitForSeconds(5);
                text.text = "Un archivo de hace cuatro años, en baja resolución. Visionario… o perezoso.";
                chances += 1;
                break;
            case 5:text.text = "NO_USAR_ESTO_viejo.png";
                new WaitForSeconds(5);
                text.text = "¿NO_USAR_ESTO_viejo.png? Literalmente lo dice el nombre.";
                chances += 1;
                break;
            case 6:text.text = "final-final-FINAL(backup)_ok.jpg";
                new WaitForSeconds(5);
                text.text = "¿Un .jpg para una entrega oficial? Por favor…";
                chances += 1;
                break;
            case 7:text.text = "Diseño_karen_corregido_copia_copia.ai";
                new WaitForSeconds(5);
                text.text = "Karen ya no trabaja aquí.";
                chances += 1;
                break;
        }
        if (chances >= 3)
            Debug.Log("se debe de colocar indicativo de que fallo");
    }
}
