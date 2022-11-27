using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;


public class ButtonPause : MonoBehaviour, IPointerDownHandler
{
    public void OnPointerDown(PointerEventData eventData) {
        ContextManager.instance.isPlaying = false;
    }
}
