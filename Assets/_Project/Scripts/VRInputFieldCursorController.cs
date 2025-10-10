using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;

public class VRInputFieldCursorController : MonoBehaviour, IPointerClickHandler
{
    [SerializeField] private TMP_InputField inputField;
    [SerializeField] private Camera vrCamera; // Set to your VR camera (CenterEyeAnchor)

    private TMP_Text textComponent;
    private RectTransform textViewport;
    public Dictionary<TMP_CharacterInfo, Vector3> CharP = new Dictionary<TMP_CharacterInfo, Vector3>();
    void Start()
    {
        if (inputField == null)
        {
            inputField = GetComponent<TMP_InputField>();
        }

        textComponent = inputField.textComponent;
        textViewport = inputField.textViewport;

        // Determine camera if not set
        if (vrCamera == null)
        {
            Canvas canvas = GetComponentInParent<Canvas>();
            vrCamera = canvas.worldCamera;
        }
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        // Get world position from VR raycast
        Vector3 worldPosition = eventData.pointerCurrentRaycast.worldPosition;

        if (TrySetCaretFromWorldPosition(worldPosition))
        {
            inputField.ActivateInputField();
        }
    }

    private bool TrySetCaretFromWorldPosition(Vector3 worldPosition)
    {
        if (textComponent == null) return false;

        // Force mesh update to get current character positions
        textComponent.ForceMeshUpdate();

        // Convert world position to screen space
        Vector2 screenPosition = RectTransformUtility.WorldToScreenPoint(
            vrCamera,
            worldPosition
        );

        // Try multiple methods with fallback strategy
        int caretIndex = GetCaretIndexWithFallback(screenPosition);

        if (caretIndex >= 0)
        {
            inputField.caretPosition = caretIndex;
            inputField.ForceLabelUpdate(); // Force visual caret update
            Debug.LogWarning("En la posición "+caretIndex+" se ha detectado ");
            return true;
        }

        return false;
    }

    private int GetCaretIndexWithFallback(Vector2 screenPosition)
    {
        ManualFindCharacterIndex(screenPosition);
        
        int o = 0; //hacer que este número cambie correspondiente a la posición
        TMP_CharacterInfo charInfo = textComponent.textInfo.characterInfo[o];
        
        // Method 1: Try GetCursorIndexFromPosition (most intelligent)
        if (HasGetCursorIndexMethod())
        {
            int index = TMP_TextUtilities.GetCursorIndexFromPosition(
                textComponent,
                screenPosition,
                vrCamera,
                out CaretPosition caretPos
            );
            Debug.LogWarning("click en la posición " + screenPosition + " con la letra " + caretPos);

            if (index != -1) return index;
        }

        // Method 2: Try intersecting character (most precise)
        int charIndex = TMP_TextUtilities.FindIntersectingCharacter(
            textComponent,
            screenPosition,
            vrCamera,
            true // visible only
        );

        if (charIndex != -1) return charIndex;

        // Method 3: Find nearest character on nearest line (line-aware)
        int lineIndex = TMP_TextUtilities.FindNearestLine(
            textComponent,
            screenPosition,
            vrCamera
        );

        if (lineIndex != -1)
        {
            charIndex = TMP_TextUtilities.FindNearestCharacterOnLine(
                textComponent,
                screenPosition,
                lineIndex,
                vrCamera,
                false
            );

            if (charIndex != -1) return charIndex;
        }

        // Method 4: Fallback to nearest character overall
        return TMP_TextUtilities.FindNearestCharacter(
            textComponent,
            screenPosition,
            vrCamera,
            false
        );
        
    }

    private bool HasGetCursorIndexMethod()
    {
        return System.Type.GetType("TMPro.TMP_TextUtilities")
            ?.GetMethod("GetCursorIndexFromPosition") != null;
    }
    private int ManualFindCharacterIndex(Vector2 localPoint)
    {
        if (string.IsNullOrEmpty(inputField.text))
            return 0;

        textComponent.ForceMeshUpdate();
        TMP_TextInfo textInfo = textComponent.textInfo;

        // Handle empty characterInfo
        if (textInfo.characterCount == 0)
            return 0;

        // Check if click is before first character
        if (localPoint.x < textInfo.characterInfo[0].bottomLeft.x)
            return 0;

        // Check if click is after last character
        int lastIndex = textInfo.characterCount - 1;
        /*if (localPoint.x > textInfo.characterInfo[lastIndex].topRight.x)
            return textInfo.characterCount;*/

        float minDistance = float.MaxValue;
        int closestIndex = 0;

        CharP.Clear();

        for (int i = 0; i < textInfo.characterCount; i++)
        {
            TMP_CharacterInfo charInfo = textInfo.characterInfo[i];

            // Skip invisible characters (rich text tags, etc.)
            if (!charInfo.isVisible)
                continue;

            Vector3 cPos = new Vector3((charInfo.bottomLeft.x + charInfo.bottomRight.x)*0.001f ,
                 inputField.transform.position.y, 
                inputField.transform.position.z);
            Debug.LogWarning(" info de char " + charInfo.character + " del objeto " + textComponent.text + " con la pos " +
                cPos);
            //buscar forma de cambiar valores de rect transform a transform
            // Calculate character center in local space
            Vector3 charCenter = new Vector2(
                (charInfo.bottomLeft.x + charInfo.topRight.x) * 0.5f,
                (charInfo.bottomLeft.y + charInfo.topRight.y) * 0.5f
            );

            float distance = Vector3.Distance(localPoint, charCenter);

            if (distance < minDistance)
            {
                minDistance = distance;
                closestIndex = i;
            }
            CharP.Add(charInfo,cPos);
        }
        Debug.Log(CharP.Count);
        // Refine: check if we're closer to the character's start or end
        TMP_CharacterInfo closestChar = textInfo.characterInfo[closestIndex];
        float charMidpoint = (closestChar.bottomLeft.x + closestChar.topRight.x) * 0.5f;

        if (localPoint.x > charMidpoint)
        {
            // Cursor should go after this character
            return closestIndex + 1;
        }

        return closestIndex;
    }
}
