using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public class ButtonFrameJump : MonoBehaviour, IPointerDownHandler
{
    public void OnPointerDown(PointerEventData eventData)
    {
        string text = GameObject.Find("InputFrameJump").GetComponent<TMPro.TMP_InputField>().text;

        int val;
        if (int.TryParse(text, out val))
        {
            ContextManager.instance.Jump(val);
        }
    }
}
