using UnityEngine;
using TMPro;
public class TalkControl : MonoBehaviour
{
    public GameObject[] list; // UI 对象列表
    //public TextMeshProUGUI textMeshProUGUI; // 文字显示组件
    private ViewerControl viewerControlWithVoice; // 亮度控制组件
    void Start()
    {
        // 初始化亮度控制组件
        viewerControlWithVoice = gameObject.GetComponent<ViewerControl>();
        if (viewerControlWithVoice == null)
        {
            Debug.LogError("未找到 ViewerControl 组件");
        }
    }
    public void talkAction(int state = 0)
    {
        switch (state)
        {
            case 1: // 打开界面
                SetUIActive(0, true);
                break;
            case 2: // 关闭界面
                SetUIActive(0, false);
                break;
            case 3: // 返回
                // 预留逻辑
                break;
            case 4: // 前进
                MoveObject(1, 0, 0, 0.2f);
                break;
            case 5: // 后退
                MoveObject(1, 0, 0, -0.2f);
                break;
            case 6: // 左转
                RotateObject(1, 0, 45f, 0);
                break;
            case 7: // 右转
                RotateObject(1, 0, -45f, 0);
                break;
            case 8: // 上转
                RotateObject(1, 45f, 0, 0);
                break;
            case 9: // 下转
                RotateObject(1, -45f, 0, 0);
                break;
            case 10: // 调高亮度
                Debug.Log("has in case10");
                if (viewerControlWithVoice != null)
                {
                    viewerControlWithVoice.lightChange(1);
                    //textMeshProUGUI.text = "调高亮度成功";
                    Debug.Log("Brightness up success");
                }
                else
                {
                    Debug.LogError("viewerControlWithVoice 组件未初始化");
                }
                break;
            case 11: // 调低亮度
                if (viewerControlWithVoice != null)
                {
                    viewerControlWithVoice.lightChange(-1);
                    //textMeshProUGUI.text = "调低亮度成功";
                    Debug.Log("Brightness down success");
                }
                else
                {
                    Debug.LogError("BrightnessControlWithVoice 组件未初始化");
                }
                break;
            case 12: // 调高音量
                break;
            case 13: // 调低音量
                break;
            case 14: // 开始装配
                SetUIActive(2, true);
                break;
            case 15: // 停止装配
                SetUIActive(2, false);
                break;
            case 16: // 拍摄舱内图像
                break;
            default:
                // Debug.LogWarning($"未知的语音命令: {state}");
                break;
        }
    }
    // 设置 UI 对象激活状态
    private void SetUIActive(int index, bool isActive)
    {
        if (index >= 0 && index < list.Length)
        {
            list[index].SetActive(isActive);
        }
    }
    // 移动对象
    private void MoveObject(int index, float x, float y, float z)
    {
        if (index >= 0 && index < list.Length)
        {
            list[index].transform.Translate(x, y, z, Space.World);
        }
    }
    // 旋转对象
    private void RotateObject(int index, float x, float y, float z)
    {
        if (index >= 0 && index < list.Length)
        {
            list[index].transform.Rotate(x, y, z, Space.World);
        }
    }
}
