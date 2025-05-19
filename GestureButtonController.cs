using System;
using System.Collections.Generic;
using UnityEngine;

public class GestureButtonController : MonoBehaviour
{
    [Header("����ʶ�����")]
    public Handtracking handstrack;

    [Header("�������")]
    public GameObject mainInterface;      // ������
    public GameObject secondaryInterface; // ������
    private bool isMainInterfaceActive = true;

    [Header("��Ƶ����")]
    public AudioSource bgm;
    [Range(0, 1)] public float initialVolume = 0f;

    [Header("����ʶ�����")]
    [Tooltip("����ȷ����Ҫ������֡��")]
    public int stabilityWindowSize = 7;
    [Tooltip("ͬһ�������������������")]
    public int maxGestureRepeat = 3;
    [Tooltip("������С�������(��)")]
    public float minGestureInterval = 0.5f;

    [Header("�����ƶ�������")]
    public GestureSettings[] gestureSettings;

    // ������ʷ����
    private Queue<int> gestureQueue = new Queue<int>();
    private Queue<int> gestureHistory = new Queue<int>();
    private DateTime lastGestureTime;
    private ViewerControl viewerControl;

    // ˫��������
    private DateTime lastDoubleClickTime;
    private const float doubleClickThreshold = 0.3f; // ˫�������ֵ���룩

    [System.Serializable]
    public class GestureSettings
    {
        public int gestureID;
        public int maxRepeatCount = 3;
        public float coolDownTime = 0.5f;
    }

    void Start()
    {
        // ��ʼ�����
        viewerControl = gameObject.GetComponent<ViewerControl>();
        if (!viewerControl) Debug.LogError("Missing ViewerControl component");

        // ��Ƶ��ʼ��
        bgm.volume = initialVolume;

        // Ĭ�Ͻ���״̬
        SetInterfaceState(true);
    }

    void Update()
    {
        int currentGesture = handstrack.gesture_label;
        UpdateGestureQueue(currentGesture);

        if (IsGestureStable(currentGesture) && CanTriggerGesture(currentGesture))
        {
            ProcessGesture(currentGesture);
            UpdateGestureHistory(currentGesture);
            lastGestureTime = DateTime.Now;
        }
    }

    #region ���������߼�
    void UpdateGestureQueue(int gesture)
    {
        if (gestureQueue.Count >= 32) gestureQueue.Dequeue();
        if (gesture != -1) gestureQueue.Enqueue(gesture);
    }

    bool IsGestureStable(int gesture)
    {
        if (gestureQueue.Count < stabilityWindowSize) return false;

        int[] lastGestures = gestureQueue.ToArray();
        int startIndex = Mathf.Max(lastGestures.Length - stabilityWindowSize, 0);

        for (int i = startIndex; i < lastGestures.Length; i++)
        {
            if (lastGestures[i] != gesture) return false;
        }
        return true;
    }

    bool CanTriggerGesture(int gestureID)
    {
        // ��ȡ�����Ƶ�ר������
        GestureSettings settings = GetGestureSettings(gestureID);

        // ��ȴʱ����
        if ((DateTime.Now - lastGestureTime).TotalSeconds < settings.coolDownTime)
            return false;

        // �����������
        int consecutiveCount = 0;
        foreach (int g in gestureHistory)
        {
            if (g == gestureID) consecutiveCount++;
            else consecutiveCount = 0;
        }

        return consecutiveCount < settings.maxRepeatCount;
    }

    void UpdateGestureHistory(int gesture)
    {
        if (gestureHistory.Count >= 5) gestureHistory.Dequeue();
        gestureHistory.Enqueue(gesture);
    }
    #endregion

    #region ���ƹ���ʵ��
    void ProcessGesture(int gesture)
    {
        switch (gesture)
        {
            case 11: // ˫��PitchĴ+ʳ - ˫�����
                ToggleMainInterface();
                break;

            case 0: // ��ɨ - �л���������
                HandleLeftSwipe();
                break;

            case 1: // ��ɨ - �л���������
                HandleRightSwipe();
                break;

            case 24: // ����˳ʱ�� - ����+
                AdjustBrightness(1);
                break;

            case 25: // ������ʱ�� - ����-
                AdjustBrightness(-1);
                break;

            case 15: // ʳָ˳ʱ�� - ����+
                AdjustVolume(0.2f);
                break;

            case 16: // ʳָ��ʱ�� - ����-
                AdjustVolume(-0.2f);
                break;
        }
    }

    void HandleLeftSwipe()
    {
        if (secondaryInterface.activeSelf && !mainInterface.activeSelf)
        {
            Debug.Log("����ʾ������");
            return;
        }

        if (mainInterface.activeSelf && secondaryInterface.activeSelf)
        {
            mainInterface.SetActive(false);
            Debug.Log("�л�������������");
        }
        else
        {
            mainInterface.SetActive(false);
            secondaryInterface.SetActive(true);
            Debug.Log("�л���������");
        }
    }

    void HandleRightSwipe()
    {
        if (mainInterface.activeSelf && !secondaryInterface.activeSelf)
        {
            Debug.Log("����ʾ������");
            return;
        }

        if (mainInterface.activeSelf && secondaryInterface.activeSelf)
        {
            secondaryInterface.SetActive(false);
            Debug.Log("�л��󸱽�������");
        }
        else
        {
            secondaryInterface.SetActive(false);
            mainInterface.SetActive(true);
            Debug.Log("�л���������");
        }
    }

    void ToggleMainInterface()
    {
        mainInterface.SetActive(!mainInterface.activeSelf);
        Debug.Log($"MainUI {(mainInterface.activeSelf ? "open" : "close")}");
    }

    void SetInterfaceState(bool showMain)
    {
        mainInterface.SetActive(showMain);
        secondaryInterface.SetActive(!showMain);
        isMainInterfaceActive = showMain;
    }

    void AdjustBrightness(float delta)
    {
        if (viewerControl)
        {
            viewerControl.lightChange(delta > 0 ? 1 : -1);
            Debug.Log($"lightChange {(delta > 0 ? "+" : "-")}");
        }
    }

    void AdjustVolume(float delta)
    {
        bgm.volume = Mathf.Clamp01(bgm.volume + delta / 10f); // ����Ϊ��С�������仯����
        Debug.Log($"volume: {bgm.volume:F1}");
    }
    #endregion

    #region ��������
    GestureSettings GetGestureSettings(int gestureID)
    {
        foreach (var setting in gestureSettings)
        {
            if (setting.gestureID == gestureID)
                return setting;
        }
        return new GestureSettings()
        {
            gestureID = gestureID,
            maxRepeatCount = this.maxGestureRepeat,
            coolDownTime = this.minGestureInterval
        };
    }
    #endregion
}