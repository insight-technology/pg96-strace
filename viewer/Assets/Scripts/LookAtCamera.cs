using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LookAtCamera : MonoBehaviour
{
    void Start()
    {
        // foward(z軸)の方を向けることで文字が反転するのを修正
        transform.localScale = new Vector3(-1, 1, 1);
    }

    void Update()
    {
        // 自身の向きをカメラに向ける
        transform.LookAt(Camera.main.transform);
    }
}