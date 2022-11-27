using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class ButtonDecrement : MonoBehaviour, IPointerDownHandler
{
    public void OnPointerDown(PointerEventData eventData) {
        bool isPlayingOrigin = ContextManager.instance.isPlaying;

        ContextManager.instance.isPlaying = false;

        ContextManager.instance.Decrement();

        ContextManager.instance.isPlaying = isPlayingOrigin;
    }
}
