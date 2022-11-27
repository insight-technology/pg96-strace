using System.Collections;
using System.Collections.Generic;
using UnityEngine;


public class BeCenterByDoubleClick : MonoBehaviour
{
    private bool isClickedOnce;
    private float sinceLastClicked;

    float DOUBLE_CLICK_THRESHOLD = 0.3f;

    void Start()
    {
        resetStatus();
    }

    void Update()
    {
    }

    void OnMouseUp() {
        if (isClickedOnce) {
            sinceLastClicked += Time.deltaTime;
            if (sinceLastClicked < DOUBLE_CLICK_THRESHOLD) {
                if (Input.GetMouseButtonDown(0)) {
                    resetStatus();
                    Camera.main.GetComponent<CameraCtrl>().center = gameObject.transform.position;
                }
            } else {
                resetStatus();
            }
        } else {
            if (Input.GetMouseButtonDown(0)) {
                isClickedOnce = true;
            }
        }
    }

    void resetStatus() {
        isClickedOnce = false;
        sinceLastClicked = 0.0f;
    }
}
