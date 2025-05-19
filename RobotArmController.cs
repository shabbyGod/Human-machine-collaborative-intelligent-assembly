using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using System.Net.Sockets;
using System.Text;
using System;

public class RobotArmController : MonoBehaviour
{
    private TcpClient client;
    private NetworkStream stream;
    private readonly string serverIP = "192.168.43.1";
    private readonly int serverPort = 8000;
    private bool isConnected = false;
    private readonly object threadLock = new object();

    void Start()
    {
        Debug.Log("RobotArmController: Start() called");
        InitializeConnection();
    }

    void InitializeConnection()
    {
        Debug.Log("RobotArmController: Attempting to initialize connection...");
        lock (threadLock)
        {
            if (client != null)
            {
                Debug.Log("RobotArmController: Existing client found, closing it...");
                try { client.Close(); } catch (Exception e) { Debug.LogWarning($"Error closing client: {e.Message}"); }
            }

            client = new TcpClient()
            {
                SendTimeout = 1000,
                ReceiveTimeout = 1000
            };

            try
            {
                Debug.Log($"RobotArmController: Trying to connect to {serverIP}:{serverPort}");
                client.Connect(serverIP, serverPort);
                stream = client.GetStream();
                isConnected = true;
                Debug.Log($"RobotArmController: Successfully connected to {serverIP}:{serverPort}");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"RobotArmController: Connection failed: {e.Message}");
                isConnected = false;
            }
        }
    }

    public void OnButtonClick(GameObject btn)
    {
        Debug.Log($"RobotArmController: Button clicked - {btn.name}");

        int commandCode = btn.name switch
        {
            "Start" => 100,
            "Stop" => 101,
            "Photo" => 102,
            _ => -1
        };

        if (commandCode == -1)
        {
            Debug.LogWarning($"RobotArmController: Unknown button: {btn.name}");
            return;
        }

        Debug.Log($"RobotArmController: Preparing to send command code {commandCode} for button {btn.name}");
        SendCommand(commandCode);
    }

    private void SendCommand(int commandCode)
    {
        Debug.Log($"RobotArmController: SendCommand({commandCode}) called");
        try
        {
            lock (threadLock)
            {
                if (!isConnected || stream == null)
                {
                    Debug.LogWarning("RobotArmController: Not connected or stream is null, attempting reconnection...");
                    InitializeConnection();
                    if (!isConnected)
                    {
                        Debug.LogError("RobotArmController: Reconnection failed, cannot send command");
                        return;
                    }
                }

                long timestamp = (long)(DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalMilliseconds;
                string msg = $"2_2,{timestamp},{commandCode}\n";
                byte[] data = Encoding.UTF8.GetBytes(msg);

                Debug.Log($"RobotArmController: Sending message: {msg.Trim()}");
                stream.Write(data, 0, data.Length);
                Debug.Log($"RobotArmController: Successfully sent: {msg.Trim()}");
            }
        }
        catch (SocketException se)
        {
            Debug.LogError($"RobotArmController: Network error (SocketException): {se.SocketErrorCode}");
            Debug.LogError($"Full error details: {se}");
            HandleDisconnection();
        }
        catch (Exception e)
        {
            Debug.LogError($"RobotArmController: Send error: {e.Message}");
            Debug.LogError($"Full error details: {e}");
            HandleDisconnection();
        }
    }

    private void HandleDisconnection()
    {
        Debug.LogWarning("RobotArmController: Handling disconnection...");
        lock (threadLock)
        {
            isConnected = false;
            try
            {
                stream?.Close();
                Debug.Log("RobotArmController: Stream closed successfully");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"RobotArmController: Error closing stream: {e.Message}");
            }
            try
            {
                client?.Close();
                Debug.Log("RobotArmController: Client closed successfully");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"RobotArmController: Error closing client: {e.Message}");
            }
        }
    }

    void OnDestroy()
    {
        Debug.Log("RobotArmController: OnDestroy() called, cleaning up resources");
        lock (threadLock)
        {
            try
            {
                stream?.Close();
                Debug.Log("RobotArmController: Stream closed successfully in OnDestroy");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"RobotArmController: Error closing stream in OnDestroy: {e.Message}");
            }
            try
            {
                client?.Close();
                Debug.Log("RobotArmController: Client closed successfully in OnDestroy");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"RobotArmController: Error closing client in OnDestroy: {e.Message}");
            }
        }
    }
}