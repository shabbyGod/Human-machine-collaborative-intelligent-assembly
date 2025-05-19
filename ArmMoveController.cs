using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Net.Sockets;
using System.Text;
using System;

public class ArmMoveController : MonoBehaviour
{
    private TcpClient client;
    private NetworkStream stream;
    private readonly string serverIP = "192.168.43.1";
    private readonly int serverPort = 8000;
    private bool isConnected = false;
    private readonly object threadLock = new object();

    private Vector3 lastPosition;
    private Quaternion lastRotation;
    private float positionThreshold = 0.001f;
    private float rotationThreshold = 0.1f;
    private float maxMovement = 0.2f;

    private float minPosition = -100f / 2;
    private float maxPosition = 100f / 2;
    private bool is_over_area = false;

    // ��¼��һ�θ��µ�ʱ��
    private float lastUpdateTime;
    // �ۻ���λ�ñ仯
    private Vector3 accumulatedPositionDelta = Vector3.zero;
    // �ۻ�����ת�仯
    private float accumulatedRotationDelta = 0f;
    private float time = 1f;

    void Start()
    {
        InitializeConnection();
        lastUpdateTime = Time.time;
        lastPosition = transform.localPosition;
        lastRotation = transform.localRotation;
    }

    void InitializeConnection()
    {
        lock (threadLock)
        {
            if (client != null)
            {
                try { client.Close(); } catch { }
            }

            client = new TcpClient()
            {
                SendTimeout = 1000,
                ReceiveTimeout = 1000
            };

            try
            {
                client.Connect(serverIP, serverPort);
                stream = client.GetStream();
                isConnected = true;
                Debug.Log($"Connected to {serverIP}:{serverPort}");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"Connection failed: {e.Message}");
                isConnected = false;
            }
        }
    }

    void Update()
    {
        // ��������һ֡����������ʱ��
        float deltaTime = Time.time - lastUpdateTime;
        if (deltaTime >= time)
        {
            // ��������� 1 �룬��鲢�����ۻ�������
            CheckAndSendAccumulatedTransformChanges();
            // ���ü�ʱ��
            lastUpdateTime = Time.time;
            // �����ۻ��ı仯
            accumulatedPositionDelta = Vector3.zero;
            accumulatedRotationDelta = 0f;
        }
        else
        {
            // ���򣬼����ۻ��仯
            AccumulateTransformChanges();
        }
    }

    void AccumulateTransformChanges()
    {
        float currentYRotation = transform.localRotation.eulerAngles.y;
        if (currentYRotation > 180) currentYRotation -= 360;

        float lastYRotation = lastRotation.eulerAngles.y;
        if (lastYRotation > 180) lastYRotation -= 360;

        // ���λ�ñ߽�
        if (transform.localPosition.x < minPosition ||
            transform.localPosition.x > maxPosition ||
            transform.localPosition.y < minPosition ||
            transform.localPosition.y > maxPosition ||
            transform.localPosition.z < minPosition ||
            transform.localPosition.z > maxPosition)
        {
            is_over_area = true;
            transform.localPosition = lastPosition;
            transform.localRotation = lastRotation;
        }
        else
        {
            is_over_area = false;
        }

        Vector3 positionDelta = transform.localPosition - lastPosition;

        if (!is_over_area)
        {
            // �ۻ�λ�ñ仯
            accumulatedPositionDelta += positionDelta;
            // �ۻ���ת�仯
            accumulatedRotationDelta += currentYRotation - lastYRotation;
        }

        lastPosition = transform.localPosition;
        lastRotation = transform.localRotation;
    }

    void CheckAndSendAccumulatedTransformChanges()
    {
        if (!is_over_area && (accumulatedPositionDelta.magnitude > positionThreshold ||
                             Mathf.Abs(accumulatedRotationDelta) > rotationThreshold))
        {
            float absX = Mathf.Abs(accumulatedPositionDelta.x);
            float absY = Mathf.Abs(accumulatedPositionDelta.y);
            float absZ = Mathf.Abs(accumulatedPositionDelta.z);
            float rotationY = accumulatedRotationDelta;

            if (absX <= maxMovement && absY <= maxMovement && absZ <= maxMovement)
            {
                // ת��Ϊ���ײ�׼������
                float x_mm = accumulatedPositionDelta.x * 1000f;
                float y_mm = accumulatedPositionDelta.y * 1000f;
                float z_mm = accumulatedPositionDelta.z * 1000f;

                SendMovementData(x_mm, y_mm, z_mm, rotationY);
            }
            else
            {
                Debug.LogWarning($"Movement too large (X: {accumulatedPositionDelta.x}, Y: {accumulatedPositionDelta.y}, Z: {accumulatedPositionDelta.z}), not sending");
            }
        }
    }

    private void SendMovementData(float x, float y, float z, float rotationY)
    {
        try
        {
            lock (threadLock)
            {
                if (!isConnected || stream == null)
                {
                    Debug.Log("Attempting reconnection...");
                    InitializeConnection();
                    if (!isConnected) return;
                }

                long timestamp = (long)(DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalMilliseconds;


                string msg = $"2_1,{timestamp},{x},{y},{z},{rotationY}\n";

                byte[] data = Encoding.UTF8.GetBytes(msg);
                stream.Write(data, 0, data.Length);
                Debug.Log($"Sent: {msg.Trim()}");
            }
        }
        catch (SocketException se)
        {
            Debug.LogWarning($"Network error: {se.SocketErrorCode}");
            HandleDisconnection();
        }
        catch (Exception e)
        {
            Debug.LogError($"Send error: {e.Message}");
            HandleDisconnection();
        }
    }

    private void HandleDisconnection()
    {
        lock (threadLock)
        {
            isConnected = false;
            try { stream?.Close(); } catch { }
            try { client?.Close(); } catch { }
        }
    }

    void OnDestroy()
    {
        lock (threadLock)
        {
            try { stream?.Close(); } catch { }
            try { client?.Close(); } catch { }
        }
    }
}