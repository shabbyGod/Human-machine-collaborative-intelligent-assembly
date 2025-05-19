using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Diagnostics;

public class Handtracking : MonoBehaviour
{
    public GameObject[] lefthandpoints;
    public GameObject[] righthandpoints;

    public int gesture_label;
    public int left_valid;
    public int right_valid;

    // Start is called before the first frame update
    void Start()
    {
        gesture_label = -1;
    }

    // Update is called once per frame
    void Update()
    {
        string landmarks_list = LoadStereo.landmarks_list;

        // 检查 landmarks_list 是否为空
        if (string.IsNullOrEmpty(landmarks_list))
        {
            UnityEngine.Debug.LogWarning("landmarks_list is empty.");
            return;
        }

        string[] landmarks = landmarks_list.Split(',');
        UnityEngine.Debug.Log(landmarks.Length);

        // 检查 landmarks 数组长度是否足够
        if (landmarks.Length < 126) // 126 = 21 landmarks * 3 coordinates (x, y, z) * 2 hands
        {
            UnityEngine.Debug.LogWarning($"Invalid landmarks length: {landmarks.Length}");
            return;
        }

        float[] left_landmarks = new float[63];
        float[] right_landmarks = new float[63];
        //UnityEngine.Debug.Log("1");
        try
        {
            for (int i = 0; i < landmarks.Length; i++)
            {
                if (landmarks_list != null)
                {
                    if (i < 63)
                    {
                        left_landmarks[i] = float.Parse(landmarks[i]);
                    }
                    else if (i >= 63 && i < 126)
                    {
                        right_landmarks[i - 63] = float.Parse(landmarks[i]);
                    }
                    else if (i == 126)
                    {
                        gesture_label = (int)float.Parse(landmarks[i]);
                    }
                    else if (i == 127)
                    {
                        left_valid = (int)float.Parse(landmarks[i]);
                    }
                    else
                    {
                        right_valid = (int)float.Parse(landmarks[i]);
                    }
                }
            }

            UnityEngine.Debug.Log("gesture_label" + gesture_label);
            //UnityEngine.Debug.Log(left_landmarks[62]);
            //UnityEngine.Debug.Log(right_landmarks[62]);

            //for (int i = 0; i < 21; i++)
            //{
            //    float x_l = left_landmarks[i * 3] / 1000;
            //    float y_l = left_landmarks[i * 3 + 1] / 1000;
            //    float z_l = left_landmarks[i * 3 + 2] / 1000;

            //    float x_r = right_landmarks[i * 3] / 1000;
            //    float y_r = right_landmarks[i * 3 + 1] / 1000;
            //    float z_r = right_landmarks[i * 3 + 2] / 1000;

            //    lefthandpoints[i].transform.localPosition = new Vector3(x_l, -y_l, z_l);
            //    righthandpoints[i].transform.localPosition = new Vector3(x_r, -y_r, z_r);

            //    if (left_valid == 0)
            //    {
            //        lefthandpoints[i].SetActive(false);
            //    }
            //    else
            //    {
            //        lefthandpoints[i].SetActive(true);
            //    }
            //    if (right_valid == 0)
            //    {
            //        righthandpoints[i].SetActive(false);
            //    }
            //    else
            //    {
            //        righthandpoints[i].SetActive(true);
            //    }
            //}
        }
        catch (Exception err)
        {
            UnityEngine.Debug.Log(err.ToString());
        }
    }

    void reshape_to_hands(string[] landmarks)
    {

    }
}
