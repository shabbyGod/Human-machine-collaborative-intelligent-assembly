using System;
using System.Collections.Generic;
using UnityEngine;

public class GestureButtonController : MonoBehaviour
{
    [Header("手势识别组件")]
    public Handtracking handstrack;

    [Header("界面控制")]
    public GameObject mainInterface;      // 主界面
    public GameObject secondaryInterface; // 副界面
    private bool isMainInterfaceActive = true;

    [Header("音频控制")]
    public AudioSource bgm;
    [Range(0, 1)] public float initialVolume = 0f;

    [Header("手势识别参数")]
    [Tooltip("手势确认需要的连续帧数")]
    public int stabilityWindowSize = 7;
    [Tooltip("同一手势最大连续触发次数")]
    public int maxGestureRepeat = 3;
    [Tooltip("手势最小触发间隔(秒)")]
    public float minGestureInterval = 0.5f;

    [Header("各手势独立设置")]
    public GestureSettings[] gestureSettings;

    // 手势历史数据
    private Queue<int> gestureQueue = new Queue<int>();
    private Queue<int> gestureHistory = new Queue<int>();
    private DateTime lastGestureTime;
    private ViewerControl viewerControl;

    // 双击检测相关
    private DateTime lastDoubleClickTime;
    private const float doubleClickThreshold = 0.3f; // 双击间隔阈值（秒）

    [System.Serializable]
    public class GestureSettings
    {
        public int gestureID;
        public int maxRepeatCount = 3;
        public float coolDownTime = 0.5f;
    }

    void Start()
    {
        // 初始化组件
        viewerControl = gameObject.GetComponent<ViewerControl>();
        if (!viewerControl) Debug.LogError("Missing ViewerControl component");

        // 音频初始化
        bgm.volume = initialVolume;

        // 默认界面状态
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

    #region 核心手势逻辑
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
        // 获取该手势的专属设置
        GestureSettings settings = GetGestureSettings(gestureID);

        // 冷却时间检查
        if ((DateTime.Now - lastGestureTime).TotalSeconds < settings.coolDownTime)
            return false;

        // 连续次数检查
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

    #region 手势功能实现
    void ProcessGesture(int gesture)
    {
        switch (gesture)
        {
            case 11: // 双击Pitch拇+食 - 双击检测
                ToggleMainInterface();
                break;

            case 0: // 左扫 - 切换到副界面
                HandleLeftSwipe();
                break;

            case 1: // 右扫 - 切换到主界面
                HandleRightSwipe();
                break;

            case 24: // 手掌顺时针 - 亮度+
                AdjustBrightness(1);
                break;

            case 25: // 手掌逆时针 - 亮度-
                AdjustBrightness(-1);
                break;

            case 15: // 食指顺时针 - 音量+
                AdjustVolume(0.2f);
                break;

            case 16: // 食指逆时针 - 音量-
                AdjustVolume(-0.2f);
                break;
        }
    }

    void HandleLeftSwipe()
    {
        if (secondaryInterface.activeSelf && !mainInterface.activeSelf)
        {
            Debug.Log("已显示副界面");
            return;
        }

        if (mainInterface.activeSelf && secondaryInterface.activeSelf)
        {
            mainInterface.SetActive(false);
            Debug.Log("切换后主界面隐藏");
        }
        else
        {
            mainInterface.SetActive(false);
            secondaryInterface.SetActive(true);
            Debug.Log("切换到副界面");
        }
    }

    void HandleRightSwipe()
    {
        if (mainInterface.activeSelf && !secondaryInterface.activeSelf)
        {
            Debug.Log("已显示主界面");
            return;
        }

        if (mainInterface.activeSelf && secondaryInterface.activeSelf)
        {
            secondaryInterface.SetActive(false);
            Debug.Log("切换后副界面隐藏");
        }
        else
        {
            secondaryInterface.SetActive(false);
            mainInterface.SetActive(true);
            Debug.Log("切换到主界面");
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
        bgm.volume = Mathf.Clamp01(bgm.volume + delta / 10f); // 调整为更小的音量变化步长
        Debug.Log($"volume: {bgm.volume:F1}");
    }
    #endregion

    #region 辅助方法
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