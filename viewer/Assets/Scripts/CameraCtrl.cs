using System.Collections;
using System.Collections.Generic;
using UnityEngine;
 
public class CameraCtrl : MonoBehaviour
{
    private Vector3 _center;

    public float sensitiveMove = 2.0f;
    public float sensitiveRotate = 5.0f;
    public float sensitiveZoom = 10.0f;
 
    void Start()
    {
    }
 
    void Update()
    {
        rotateCamera();
    }
 
    private void rotateCamera()
    {

        if (Input.GetMouseButton(2))
        {
            // center button / move camera
            float moveX = Input.GetAxis("Mouse X") * sensitiveMove;
            float moveY = Input.GetAxis("Mouse Y") * sensitiveMove * -1;

            Vector3 vecToScreenX = Vector3.Normalize(Vector3.Cross(Camera.main.transform.forward, Camera.main.transform.up));

            Vector3 moveVecX = vecToScreenX * moveX;
            Vector3 moveVecY = Camera.main.transform.up * moveY;

            Camera.main.transform.localPosition += moveVecX + moveVecY;
        }
        else if (Input.GetMouseButton(1))
        {
            // right button / rotate camera
            Vector3 angle = new Vector3(Input.GetAxis("Mouse X") * sensitiveRotate, Input.GetAxis("Mouse Y") * sensitiveRotate * -1, 0);

            Camera.main.transform.RotateAround(center, Vector3.up, angle.x);
            Camera.main.transform.RotateAround(center, transform.right, angle.y);
        }

        // zoom camera
        float moveZ = Input.GetAxis("Mouse ScrollWheel") * sensitiveZoom;
        Camera.main.transform.position += Camera.main.transform.forward * moveZ;
    }

    public Vector3 center {
        set {
            _center = value;
            Camera.main.transform.LookAt(_center);
        }
        get {
            return _center == null ? new Vector3(0, 0, 0) : _center;
        }
    }
}
