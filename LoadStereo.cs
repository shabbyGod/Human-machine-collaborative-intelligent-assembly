using System;
using System.Collections;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Threading;
using UnityEngine;
using System.Text;
using System.Collections.Concurrent;
using System.Collections.Generic;

public class LoadStereo : MonoBehaviour
{
    private Texture2D tex = null;
    private Color32[] pixel32;
    private GCHandle pixelHandle;
    private IntPtr pixelPtr;
    public bool isLeft = true;

    // TCP Network Settings
    [Header("Network Settings")]
    public static string serverIP = "192.168.43.1";
    public static int serverPort = 8808;
    private static TcpClient client;
    private static NetworkStream stream;
    private static Thread sendThread;
    private static Thread receiveThread;
    private static bool isSending = false;
    private static bool isReceiving = false;
    private static Queue<byte[]> sendQueue = new Queue<byte[]>();
    private static ConcurrentQueue<string> dataQueue = new ConcurrentQueue<string>();
    private static object queueLock = new object();
    private const int TARGET_WIDTH = 640;
    private const int TARGET_HEIGHT = 480;
    private static DateTime lastReconnectAttempt = DateTime.MinValue;
    private const int RECONNECT_INTERVAL = 3000; // ms

    // Image processing
    private static byte[] leftEyeData = null;
    private static byte[] rightEyeData = null;
    private static object dataLock = new object();
    private static double leftTimestamp = 0;
    private static double rightTimestamp = 0;
    private static bool connectionEstablished = false;
    private static int framesProcessed = 0;
    private static int framesSent = 0;
    private static DateTime lastDebugOutputTime = DateTime.MinValue;

    // Landmarks data
    private static TcpListener pythonListener;
    private static TcpClient pythonClient;
    private static NetworkStream pythonStream;
    private static Thread pythonServerThread;
    private static bool isPythonServerRunning = false;
    private static int pythonPort = 8888;

    public static string landmarks_list = "";
    private static StringBuilder dataBuilder = new StringBuilder();

    private float sendInterval = 1f/30f; // 30 FPS
    private float lastSendTime = 0f;


    void Start()
    {
#if UNITY_EDITOR
        Debug.Log("[LoadStereo] Running in Unity Editor - TCP disabled");
        return;
#endif
        int max = API.xslam_get_stereo_max_points();
        Debug.Log($"[LoadStereo] Max points: {max}");
        Debug.Log($"[LoadStereo] Initializing {(isLeft ? "LEFT" : "RIGHT")} eye instance");

        try
        {
            int width = API.xslam_get_stereo_width();
            int height = API.xslam_get_stereo_height();
            Debug.Log($"[LoadStereo] Original image resolution: {width}x{height}");

            // Initialize texture
            tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            GetComponent<Renderer>().material.mainTexture = tex;

            pixel32 = tex.GetPixels32();
            pixelHandle = GCHandle.Alloc(pixel32, GCHandleType.Pinned);
            pixelPtr = pixelHandle.AddrOfPinnedObject();

            // Initialize TCP connection
            if (!connectionEstablished)
            {
                ConnectToServer();
                connectionEstablished = true;
            }

            // Start Python server if not running
            if (!isPythonServerRunning)
            {
                StartPythonServer();
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[LoadStereo] Initialization failed: {e.Message}");
        }
    }

    void Update()
    {
#if UNITY_EDITOR
        return;
#endif
        try
        {
            // Debug output every 5 seconds
            if ((DateTime.Now - lastDebugOutputTime).TotalSeconds >= 5)
            {
                Debug.Log($"[LoadStereo] Stats - Frames Processed: {framesProcessed}, Frames Sent: {framesSent}, Queue Size: {sendQueue.Count}");
                lastDebugOutputTime = DateTime.Now;
            }

            // Handle reconnection if needed
            if ((client == null || !client.Connected) &&
                (DateTime.Now - lastReconnectAttempt).TotalMilliseconds > RECONNECT_INTERVAL)
            {
                lastReconnectAttempt = DateTime.Now;
                ConnectToServer();
            }

            //if (!API.xslam_ready() || !XvXR.Engine.XvDeviceManager.Manager.isStereoOn())
            //{
            //    Debug.Log("[LoadStereo] API not ready or stereo mode off");
            //    return;
            //}

            if (!API.xslam_ready())
            {
                Debug.Log("[LoadStereo] API not ready or stereo mode off");
                return;
            }


            if (Time.time - lastSendTime >= sendInterval)
            {
                // Process current eye image
                ProcessCurrentEyeImage();

                // Try to send combined image if both eyes are ready
                TrySendCombinedImage();
                lastSendTime = Time.time;
            }

            // Process received landmarks data
            ProcessReceivedLandmarks();
        }
        catch (Exception e)
        {
            Debug.LogError($"[LoadStereo] Update error: {e.Message}");
        }
    }

    private void StartPythonServer()
    {
        pythonServerThread = new Thread(() =>
        {
            try
            {
                pythonListener = new TcpListener(IPAddress.Any, pythonPort);
                pythonListener.Start();
                isPythonServerRunning = true;
                Debug.Log($"[PythonServer] Started listening on port {pythonPort}");

                while (isPythonServerRunning)
                {
                    try
                    {
                        pythonClient = pythonListener.AcceptTcpClient();
                        pythonStream = pythonClient.GetStream();
                        Debug.Log("[PythonServer] Python client connected");

                        byte[] buffer = new byte[4096];
                        while (pythonClient.Connected)
                        {
                            int bytesRead = pythonStream.Read(buffer, 0, buffer.Length);
                            if (bytesRead > 0)
                            {
                                string receivedData = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                                dataQueue.Enqueue(receivedData);
                                Debug.Log($"[PythonServer] Received data: {receivedData}");
                            }
                            else
                            {
                                break;
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        Debug.LogError($"[PythonServer] Error: {e.Message}");
                    }
                    finally
                    {
                        if (pythonClient != null)
                        {
                            pythonClient.Close();
                            pythonClient = null;
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[PythonServer] Server error: {e.Message}");
            }
            finally
            {
                pythonListener?.Stop();
                isPythonServerRunning = false;
            }
        });

        pythonServerThread.IsBackground = true;
        pythonServerThread.Start();
    }

    private void ProcessReceivedLandmarks()
    {
        if (dataQueue.TryDequeue(out string receivedData))
        {
            landmarks_list = receivedData.Trim();

            // Remove brackets if present
            if (landmarks_list.StartsWith("["))
            {
                landmarks_list = landmarks_list.Remove(0, 1);
            }
            if (landmarks_list.EndsWith("]"))
            {
                landmarks_list = landmarks_list.Remove(landmarks_list.Length - 1, 1);
            }

            Debug.Log($"Processed landmarks data: {landmarks_list}");

            // Add your data processing logic here
            // Example: string[] values = landmarks_list.Split(',');
        }
    }

    private void ProcessCurrentEyeImage()
    {
        int width = API.xslam_get_stereo_width();
        int height = API.xslam_get_stereo_height();
        if (width <= 0 || height <= 0) return;

        double ts = 0;
        bool imageUpdated = isLeft ?
            API.xslam_get_left_image(pixelPtr, width, height, ref ts) :
            API.xslam_get_right_image(pixelPtr, width, height, ref ts);

        if (!imageUpdated || double.IsInfinity(ts) || double.IsNaN(ts))
        {
            Debug.LogWarning($"[LoadStereo] Invalid image update (ts:{ts})");
            return;
        }

        if (isLeft && tex != null && XvXR.Engine.XvDeviceManager.Manager.isStereoOn())
        {
            tex.SetPixels32(pixel32);
            tex.Apply();
        }

        byte[] grayData = ProcessImageToGrayscale(pixel32, width, height);
        if (grayData == null) return;

        lock (dataLock)
        {
            if (isLeft)
            {
                leftEyeData = grayData;
                leftTimestamp = ts;
            }
            else
            {
                rightEyeData = grayData;
                rightTimestamp = ts;
            }
            framesProcessed++;
        }
    }

    private byte[] ProcessImageToGrayscale(Color32[] pixels, int srcWidth, int srcHeight)
    {
        try
        {
            byte[] grayData = new byte[TARGET_WIDTH * TARGET_HEIGHT];

            // 判断是否需要进行裁剪（两个维度都大于目标尺寸）
            bool shouldCrop = srcWidth > TARGET_WIDTH && srcHeight > TARGET_HEIGHT;

            if (shouldCrop)
            {
                // 裁剪模式 - 居中裁剪
                int cropX = (srcWidth - TARGET_WIDTH) / 2;
                int cropY = (srcHeight - TARGET_HEIGHT) / 2;

                for (int y = 0; y < TARGET_HEIGHT; y++)
                {
                    int srcY = y + cropY;
                    for (int x = 0; x < TARGET_WIDTH; x++)
                    {
                        int srcX = x + cropX;
                        Color32 color = pixels[srcY * srcWidth + srcX];
                        grayData[y * TARGET_WIDTH + x] = (byte)(0.299f * color.r + 0.587f * color.g + 0.114f * color.b);
                    }
                }
            }
            else
            {
                // 缩放模式 - 拉伸到目标尺寸
                float widthRatio = srcWidth / (float)TARGET_WIDTH;
                float heightRatio = srcHeight / (float)TARGET_HEIGHT;

                for (int y = 0; y < TARGET_HEIGHT; y++)
                {
                    int srcY = Mathf.Min((int)(y * heightRatio), srcHeight - 1);
                    for (int x = 0; x < TARGET_WIDTH; x++)
                    {
                        int srcX = Mathf.Min((int)(x * widthRatio), srcWidth - 1);
                        Color32 color = pixels[srcY * srcWidth + srcX];
                        grayData[y * TARGET_WIDTH + x] = (byte)(0.299f * color.r + 0.587f * color.g + 0.114f * color.b);
                    }
                }
            }

            return grayData;
        }
        catch (Exception e)
        {
            Debug.LogError($"[LoadStereo] Image processing failed: {e.Message}");
            return null;
        }
    }

    private void TrySendCombinedImage()
    {
        lock (dataLock)
        {
            if (leftEyeData == null || rightEyeData == null) return;

            // 验证数据尺寸（确保总是640x480）
            if (leftEyeData.Length != TARGET_WIDTH * TARGET_HEIGHT ||
                rightEyeData.Length != TARGET_WIDTH * TARGET_HEIGHT)
            {
                Debug.LogError($"[LoadStereo] Invalid eye data sizes. " +
                              $"Expected: {TARGET_WIDTH}x{TARGET_HEIGHT}, " +
                              $"Left: {leftEyeData.Length / TARGET_HEIGHT}x{TARGET_HEIGHT}, " +
                              $"Right: {rightEyeData.Length / TARGET_HEIGHT}x{TARGET_HEIGHT}");
                return;
            }

            // 合并左右眼数据（左眼在上，右眼在下）
            byte[] combinedData = new byte[TARGET_WIDTH * TARGET_HEIGHT * 2];
            Buffer.BlockCopy(leftEyeData, 0, combinedData, 0, leftEyeData.Length);
            Buffer.BlockCopy(rightEyeData, 0, combinedData, leftEyeData.Length, rightEyeData.Length);

            // 构建网络数据包
            using (MemoryStream ms = new MemoryStream())
            using (BinaryWriter writer = new BinaryWriter(ms))
            {
                writer.Write(leftTimestamp);          // 时间戳
                writer.Write(TARGET_WIDTH);          // 图像宽度
                writer.Write(TARGET_HEIGHT * 2);     // 图像高度（两倍）
                writer.Write(combinedData);          // 图像数据

                byte[] packet = ms.ToArray();
                lock (queueLock)
                {
                    sendQueue.Enqueue(packet);
                    framesSent++;
                    Debug.Log($"[LoadStereo] Sent combined image. " +
                             $"Size: {TARGET_WIDTH}x{TARGET_HEIGHT * 2}, " +
                             $"Timestamp: {leftTimestamp}");
                }
            }

            leftEyeData = null;
            rightEyeData = null;
        }
    }

    private static void ConnectToServer()
    {
        int maxRetries = 5;
        int retryInterval = 3000; // ms
        int retries = 0;

        while (retries < maxRetries)
        {
            try
            {
                if (client != null)
                {
                    client.Close();
                    client = null;
                }

                Debug.Log($"[LoadStereo] Connecting to {serverIP}:{serverPort}...");
                client = new TcpClient();
                client.Connect(serverIP, serverPort);
                stream = client.GetStream();
                isSending = true;
                isReceiving = true;

                if (sendThread == null || !sendThread.IsAlive)
                {
                    sendThread = new Thread(SendDataThread);
                    sendThread.IsBackground = true;
                    sendThread.Start();
                }

                if (receiveThread == null || !receiveThread.IsAlive)
                {
                    receiveThread = new Thread(ReceiveData);
                    receiveThread.IsBackground = true;
                    receiveThread.Start();
                }

                Debug.Log("[LoadStereo] TCP connection established");
                return; // 连接成功，退出重试循环
            }
            catch (Exception e)
            {
                Debug.LogError($"[LoadStereo] Connection failed (attempt {retries + 1}/{maxRetries}): {e.Message}");
                retries++;
                Thread.Sleep(retryInterval);
            }
        }

        Debug.LogError($"[LoadStereo] Failed to connect after {maxRetries} attempts");
    }


    private static void SendDataThread()
    {
        Debug.Log("[LoadStereo] Send thread started");

        while (isSending)
        {
            try
            {
                if (client == null || !client.Connected)
                {
                    Thread.Sleep(100);
                    continue;
                }

                byte[] packet = null;
                lock (queueLock)
                {
                    if (sendQueue.Count > 0)
                    {
                        packet = sendQueue.Dequeue();
                    }
                }

                if (packet != null)
                {
                    byte[] lengthBytes = BitConverter.GetBytes(packet.Length);
                    if (!BitConverter.IsLittleEndian)
                        Array.Reverse(lengthBytes);

                    stream.Write(lengthBytes, 0, 4);
                    stream.Write(packet, 0, packet.Length);
                }
                else
                {
                    Thread.Sleep(1);
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[LoadStereo] Send error: {e.Message}");
                Thread.Sleep(1000);
            }
        }
    }

    private static void ReceiveData()
    {
        Debug.Log("[LoadStereo] Receive thread started");

        while (isReceiving)
        {
            try
            {
                if (stream == null || !client.Connected)
                {
                    Debug.LogWarning("TCP connection not established.");
                    Thread.Sleep(1000);
                    continue;
                }

                byte[] buffer = new byte[4096];
                dataBuilder.Clear();

                while (true)
                {
                    int bytesRead = stream.Read(buffer, 0, buffer.Length);
                    if (bytesRead > 0)
                    {
                        string receivedData = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                        dataBuilder.Append(receivedData);

                        if (receivedData.EndsWith("\n"))
                        {
                            break;
                        }
                    }
                    else
                    {
                        break;
                    }
                }

                string completeData = dataBuilder.ToString().Trim();
                if (!string.IsNullOrEmpty(completeData))
                {
                    dataQueue.Enqueue(completeData);
                    Debug.Log($"Received raw data: {completeData}");
                }
            }
            catch (Exception err)
            {
                Debug.Log("TCP Receive Error: " + err.ToString());
                Thread.Sleep(1000);
            }
        }
    }

    void OnDestroy()
    {
#if UNITY_EDITOR
        return;
#endif
        Debug.Log($"[LoadStereo] Cleaning up {(isLeft ? "LEFT" : "RIGHT")} eye instance");

        // Stop Python server
        isPythonServerRunning = false;
        pythonListener?.Stop();
        pythonServerThread?.Join(1000);

        if (isLeft)
        {
            isSending = false;
            isReceiving = false;
            sendThread?.Join(1000);
            receiveThread?.Join(1000);

            if (client != null)
            {
                client.Close();
                Debug.Log("[LoadStereo] TCP connection closed");
            }
        }

        if (pixelHandle.IsAllocated)
        {
            pixelHandle.Free();
        }
    }
}
