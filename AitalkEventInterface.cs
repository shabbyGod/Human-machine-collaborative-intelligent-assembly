using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using aitalk.utils;
using System;
public class AitalkEventInterface : MonoBehaviour
{
    // 常量定义
    private const string ENGINE_TYPE = "local";
    private const string LOCAL_BNF = "#BNF+IAT 1.0 UTF-8;\n"
    + "!grammar word;\n"
    + "!slot <words>;\n"
    + "!start <words>;\n"
    + "<words>:打开界面!id(999)|关闭界面!id(1000)|返回!id(1001)|前进!id(1002)|后退!id(1003)|左转!id(1004)|右转!id(1005)|上转!id(1006)|下转!id(1007)|调高亮度!id(1008)|调低亮度!id(1009)|音量增大!id(1010)|音量减小!id(1011)|开始装配!id(1012)|停止装配!id(1013)|拍摄舱内图像!id(1014);\n";
    private const string LOCAL_GRAMMAR = "word";
    private const string LOCAL_THRESHOLD = "60";
    // 公共变量
    public TextMesh statusText = null;
    public TextMesh resultTxt = null;
    // 私有变量
    private bool isRecognizing = false; // 是否正在识别
    private AndroidJavaObject interfaceObject;
    // Start 方法
    void Start()
    {
#if UNITY_EDITOR
        return;
#endif


        // 延迟 3 秒启动语音识别
        // Invoke("StartARS", 3);   // Invoke 会阻塞当前线程，可能导致语音识别引擎无法及时响应新的语音输入。
        StartCoroutine(DelayedStartARS(3));
    }
    // 启动语音识别
    public void StartARS()
    {
        try
        {
            if (isRecognizing)
            {
                return; // 如果正在识别，则不再启动
            }
            isRecognizing = true;
            // 调用 Android 方法初始化语音识别引擎
            AndroidHelper.CallObjectMethod(InterfaceObject, "init", new object[] { });
            AndroidHelper.CallObjectMethod(InterfaceObject, "buildGrammar", new object[] { ENGINE_TYPE, LOCAL_BNF });
            AndroidHelper.CallObjectMethod(InterfaceObject, "setParam", new object[] { ENGINE_TYPE, LOCAL_GRAMMAR, LOCAL_THRESHOLD });
            AndroidHelper.CallObjectMethod(InterfaceObject, "startASR", new object[] { });
        }
        catch (Exception e)
        {
            Debug.LogError("Failed to start speech recognition: " + e.Message);
            isRecognizing = false; // 重置状态
        }
    }
    // 获取 Android 接口对象
    private AndroidJavaObject InterfaceObject
    {
        get
        {
            if (interfaceObject == null)
            {
                AndroidJavaClass activityClass = AndroidHelper.GetClass("com.unity3d.player.UnityPlayer");
                AndroidJavaObject activityObject = activityClass.GetStatic<AndroidJavaObject>("currentActivity");
                if (activityObject != null)
                {
                    interfaceObject = AndroidHelper.Create("com.xv.aitalk.UnityInterface", new object[] { activityObject });
                }
            }
            return interfaceObject;
        }
    }
    // 更新状态文本
    private void UpdateText(string text)
    {
        if (statusText != null)
        {
            statusText.text = text;
        }
        else
        {
            Debug.Log("statusText = null");
        }
    }
    // 初始化完成回调
    public void onInit(string result)
    {
        UpdateText("init" + result);
    }
    // 语法构建完成回调
    public void onBuildFinish(string result)
    {
        string[] strArray = result.Split('|');
        if (strArray.Length > 1)
        {
            UpdateText("onBuildFinish:" + strArray[1]);
        }
        else
        {
            UpdateText("onBuildFinish:false");
        }
    }
    // 开始语音识别回调
    public void onBeginOfSpeech(string nullstr)
    {
        UpdateText("识别中....");
    }
    // 结束语音识别回调
    public void onEndOfSpeech(string nullstr)
    {
        UpdateText("识别结束");
        isRecognizing = false; // 重置状态
        StartCoroutine(DelayedStartARS(0.5f)); // 异步延迟 1 秒后重新启动语音识别
    }
    // 错误回调
    public void onError(string error)
    {
        UpdateText(error);
        isRecognizing = false; // 重置状态
        StartCoroutine(DelayedStartARS(0.5f)); // 异步延迟 1 秒后重新启动语音识别
    }
    // 识别结果回调
    public void onResult(string result)
    {
        string[] strArray = result.Split('|');
        if (strArray.Length > 1)
        {
            if ("true" == strArray[1])
            {
                try
                {
                    AitalkModels.result data = SimpleJson.SimpleJson.DeserializeObject<AitalkModels.result>(result, new JsonSerializerStrategy());
                    if (data != null)
                    {
                        if (data.sc > 20)
                        {
                            if (data.ws[0].cw[0].id == 999)
                            {
                                this.GetComponent<TalkControl>().talkAction(1);
                            }
                            if (data.ws[0].cw[0].id == 1000)
                            {
                                this.GetComponent<TalkControl>().talkAction(2);
                            }
                            if (data.ws[0].cw[0].id == 1001)
                            {
                                this.GetComponent<TalkControl>().talkAction(3);
                            }
                            if (data.ws[0].cw[0].id == 1002)
                            {
                                this.GetComponent<TalkControl>().talkAction(4);
                            }
                            if (data.ws[0].cw[0].id == 1003)
                            {
                                this.GetComponent<TalkControl>().talkAction(5);
                            }
                            if (data.ws[0].cw[0].id == 1004)
                            {
                                this.GetComponent<TalkControl>().talkAction(6);
                            }
                            if (data.ws[0].cw[0].id == 1005)
                            {
                                this.GetComponent<TalkControl>().talkAction(7);
                            }
                            if (data.ws[0].cw[0].id == 1006)
                            {
                                this.GetComponent<TalkControl>().talkAction(8);
                            }
                            if (data.ws[0].cw[0].id == 1007)
                            {
                                this.GetComponent<TalkControl>().talkAction(9);
                            }
                            if (data.ws[0].cw[0].id == 1008)
                            {
                                this.GetComponent<TalkControl>().talkAction(10);
                            }
                            if (data.ws[0].cw[0].id == 1009)
                            {
                                this.GetComponent<TalkControl>().talkAction(11);
                            }
                            if (data.ws[0].cw[0].id == 1010)
                            {
                                this.GetComponent<TalkControl>().talkAction(12);
                            }
                            if (data.ws[0].cw[0].id == 1011)
                            {
                                this.GetComponent<TalkControl>().talkAction(13);
                            }
                            if (data.ws[0].cw[0].id == 1012)
                            {
                                this.GetComponent<TalkControl>().talkAction(14);
                            }
                            if (data.ws[0].cw[0].id == 1013)
                            {
                                this.GetComponent<TalkControl>().talkAction(15);
                            }
                            if (data.ws[0].cw[0].id == 1014)
                            {
                                this.GetComponent<TalkControl>().talkAction(16);
                            }
                        }
                        resultTxt.text = "onResult:true" + data.sc + "," + data.ws[0].cw[0].w + "," + data.ws[0].cw[0].id;
                    }
                }
                catch (Exception e)
                {
                    UpdateText("onResult:false");
                }
            }
            else
            {
                UpdateText("onResult:false");
            }
        }
        else
        {
            UpdateText("onResult:false");
        }
    }
    // 自定义 JSON 反序列化策略
    private class JsonSerializerStrategy : SimpleJson.PocoJsonSerializerStrategy
    {
        public override object DeserializeObject(object value, Type type)
        {
            if (type == typeof(Int32) && value.GetType() == typeof(string))
            {
                return Int32.Parse(value.ToString());
            }
            return base.DeserializeObject(value, type);
        }
    }
    // 延迟启动语音识别（协程）
    private IEnumerator DelayedStartARS(float delay)
    {
        yield return new WaitForSeconds(delay); // 等待指定时间
        StartARS(); // 启动语音识别
    }
}
