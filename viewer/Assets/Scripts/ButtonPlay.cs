using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;


public class ButtonPlay : MonoBehaviour, IPointerDownHandler
{
    public void OnPointerDown(PointerEventData eventData) {
        ContextManager.instance.isPlaying = true;
    }
}
