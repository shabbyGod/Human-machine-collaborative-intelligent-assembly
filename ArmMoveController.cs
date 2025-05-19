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

    // 记录上一次更新的时间
    private float lastUpdateTime;
    // 累积的位置变化
    private Vector3 accumulatedPositionDelta = Vector3.zero;
    // 累积的旋转变化
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
        // 计算自上一帧以来经过的时间
        float deltaTime = Time.time - lastUpdateTime;
        if (deltaTime >= time)
        {
            // 如果经过了 1 秒，检查并发送累积的数据
            CheckAndSendAccumulatedTransformChanges();
            // 重置计时器
            lastUpdateTime = Time.time;
            // 重置累积的变化
            accumulatedPositionDelta = Vector3.zero;
            accumulatedRotationDelta = 0f;
        }
        else
        {
            // 否则，继续累积变化
            AccumulateTransformChanges();
        }
    }

    void AccumulateTransformChanges()
    {
        float currentYRotation = transform.localRotation.eulerAngles.y;
        if (currentYRotation > 180) currentYRotation -= 360;

        float lastYRotation = lastRotation.eulerAngles.y;
        if (lastYRotation > 180) lastYRotation -= 360;

        // 检查位置边界
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
            // 累积位置变化
            accumulatedPositionDelta += positionDelta;
            // 累积旋转变化
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
                // 转换为毫米并准备数据
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