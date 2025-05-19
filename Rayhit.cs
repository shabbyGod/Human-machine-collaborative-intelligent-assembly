using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine.UI;
using UnityEngine;
using XvXR.Engine;
using static Rayhit;

public class Rayhit : MonoBehaviour
{
    public enum TrackMode
    {
        mode_0dof,
        mode_6dof
    }
    [Header("�۶�ע�ӵ�׷��ģʽ")]
    public TrackMode trackMode;

    private Vector3 eyeCenter;
    private Vector3 newGazeDirection;

    [Header("�۶�ע�ӵ�")]
    public Transform gazeSphere;

    [Header("������������")]
    public InteractionObject manualObject;
    public InteractionObject modelObject;
    public InteractionObject assemblyObject;

    [System.Serializable]
    public class InteractionObject
    {
        public GameObject targetUI;
        public bool useDelayDeactivation = true;
        public float deactivationDelay = 3f;
    }

    Matrix4x4 Matrix_gazeOrigin = Matrix4x4.identity;
    Matrix4x4 Matrix_XVgazeOrigin = Matrix4x4.identity;
    Matrix4x4 Matrix_target = Matrix4x4.identity;
    Matrix4x4 Matrix_XVtarget = Matrix4x4.identity;

    [Header("�۶�����")]
    public TMP_Text eyeDataText;

    // Variables for gaze timing
    private float gazeStartTime;
    private bool isGazingAtTarget = false;
    private string currentTargetName = "";
    private Coroutine delayDeactivationCoroutine;

    void Update()
    {
        if (XVETinit.isInit)
        {
            if (trackMode == TrackMode.mode_0dof)
            {
                #region 0dofģʽ
                if (XVETinit.eyeData.leftPupil.pupilCenter.x != -1 || XVETinit.eyeData.leftPupil.pupilCenter.y != -1 || XVETinit.eyeData.rightPupil.pupilCenter.x != -1 || XVETinit.eyeData.rightPupil.pupilCenter.y != -1)
                {
                    eyeCenter = XVETinit.middleOfEyes_pos;
                    Vector3 target = new Vector3(XVETinit.gazeDirection.x, -XVETinit.gazeDirection.y, XVETinit.gazeDirection.z) * 15 + new Vector3(eyeCenter.x, eyeCenter.y, eyeCenter.z);
                    gazeSphere.position = target;

                    newGazeDirection = target - eyeCenter;
                    Ray ray = new Ray(eyeCenter, newGazeDirection);
                    Debug.DrawRay(ray.origin, ray.direction * 100, Color.red);

                    #region �۶�ע�������볡���е�������н���
                    RaycastHit hit;

                    if (Physics.Raycast(ray, out hit))
                    {
                        // ����Ƿ����д���EyeInteraction��ǩ������
                        if (hit.collider.CompareTag("EyeInteraction"))
                        {
                            string hitObjectName = hit.collider.gameObject.name;

                            // ������µ�Ŀ������
                            if (currentTargetName != hitObjectName)
                            {
                                currentTargetName = hitObjectName;
                                gazeStartTime = Time.time;
                                isGazingAtTarget = true;

                                // ȡ��֮ǰ���ӳٹر�Э��
                                if (delayDeactivationCoroutine != null)
                                {
                                    StopCoroutine(delayDeactivationCoroutine);
                                    delayDeactivationCoroutine = null;
                                }
                            }
                            // ���ע��ʱ�䳬��1��
                            else if (isGazingAtTarget && Time.time - gazeStartTime >= 1.5f)
                            {
                                // ������������ִ�в�ͬ����
                                switch (hitObjectName)
                                {
                                    case "Show Manual":
                                        if (manualObject.targetUI != null) manualObject.targetUI.SetActive(true);
                                        break;
                                    case "Show Models":
                                        if (modelObject.targetUI != null) modelObject.targetUI.SetActive(true);
                                        break;
                                    case "Start Assembly":
                                        if (assemblyObject.targetUI != null) assemblyObject.targetUI.SetActive(true);
                                        break;
                                }
                            }
                        }
                    }
                    else
                    {
                        // �����ƿ�Ŀ������
                        if (isGazingAtTarget)
                        {
                            isGazingAtTarget = false;

                            // ����Ŀ�����ƻ�ȡ��Ӧ�Ľ�������
                            InteractionObject currentObject = GetCurrentInteractionObject(currentTargetName);
                            if (currentObject != null && currentObject.useDelayDeactivation)
                            {
                                // �����ӳٹر�Э��
                                if (delayDeactivationCoroutine != null)
                                {
                                    StopCoroutine(delayDeactivationCoroutine);
                                }
                                delayDeactivationCoroutine = StartCoroutine(DelayedDeactivation(currentObject.deactivationDelay, currentTargetName));
                            }

                            currentTargetName = "";
                        }
                    }
                    #endregion
                }
                #endregion
            }
            else if (trackMode == TrackMode.mode_6dof)
            {
                // 6dofģʽ�Ĵ��뱣�ֲ���
                Matrix_gazeOrigin.SetTRS(new Vector3(XVETinit.gazeOrigin.x, -XVETinit.gazeOrigin.y, XVETinit.gazeOrigin.z), Quaternion.identity, Vector3.one);
                Matrix_XVgazeOrigin = XVETinit.MatrixNewhead * Matrix_gazeOrigin;
                eyeCenter = Matrix_XVgazeOrigin.GetColumn(3);

                Vector3 target = new Vector3(XVETinit.gazeDirection.x, -XVETinit.gazeDirection.y, XVETinit.gazeDirection.z) * 15 + new Vector3(XVETinit.gazeOrigin.x, -XVETinit.gazeOrigin.y, XVETinit.gazeOrigin.z);
                Matrix_target.SetTRS(target, Quaternion.identity, Vector3.one);
                Matrix_XVtarget = XVETinit.MatrixNewhead * Matrix_target;
                newGazeDirection = (Vector3)Matrix_XVtarget.GetColumn(3) - eyeCenter;

                if (XVETinit.eyeData.leftPupil.pupilCenter.x != -1 || XVETinit.eyeData.leftPupil.pupilCenter.y != -1 || XVETinit.eyeData.rightPupil.pupilCenter.x != -1 || XVETinit.eyeData.rightPupil.pupilCenter.y != -1)
                {
                    gazeSphere.position = Matrix_XVtarget.GetColumn(3);
                    Ray ray = new Ray(eyeCenter, newGazeDirection);
                    Debug.DrawRay(ray.origin, ray.direction * 100, Color.red);

                    // 6dofģʽ�µĽ����߼�����0dofģʽ���ƣ�
                    RaycastHit hit;

                    if (Physics.Raycast(ray, out hit))
                    {
                        if (hit.collider.CompareTag("EyeInteraction"))
                        {
                            string hitObjectName = hit.collider.gameObject.name;

                            if (currentTargetName != hitObjectName)
                            {
                                currentTargetName = hitObjectName;
                                gazeStartTime = Time.time;
                                isGazingAtTarget = true;

                                if (delayDeactivationCoroutine != null)
                                {
                                    StopCoroutine(delayDeactivationCoroutine);
                                    delayDeactivationCoroutine = null;
                                }
                            }
                            else if (isGazingAtTarget && Time.time - gazeStartTime >= 1.5f)
                            {
                                switch (hitObjectName)
                                {
                                    case "Show Manual":
                                        if (manualObject.targetUI != null)
                                        {
                                            
                                            manualObject.targetUI.SetActive(true);
                                            
                                        }
                                            
                                        else
                                            Debug.Log("Manual UI is not assigned.");
                                        break;
                                    case "Show Models":
                                        if (modelObject.targetUI != null) modelObject.targetUI.SetActive(true);
                                        break;
                                    case "Start Assembly":
                                        if (assemblyObject.targetUI != null) assemblyObject.targetUI.SetActive(true);
                                        break;
                                }
                            }
                        }
                    }
                    else
                    {
                        if (isGazingAtTarget)
                        {
                            isGazingAtTarget = false;

                            InteractionObject currentObject = GetCurrentInteractionObject(currentTargetName);
                            if (currentObject != null && currentObject.useDelayDeactivation)
                            {
                                if (delayDeactivationCoroutine != null)
                                {
                                    StopCoroutine(delayDeactivationCoroutine);
                                }
                                delayDeactivationCoroutine = StartCoroutine(DelayedDeactivation(currentObject.deactivationDelay, currentTargetName));
                            }

                            currentTargetName = "";
                        }
                    }
                }
            }

            if (eyeDataText != null)
            {
                eyeDataText.text =
                    $"timestamp:{XVETinit.eyeData.timestamp}\n" +
                    $"recomGaze gazePoint:{XVETinit.eyeData.recomGaze.gazePoint.x},{XVETinit.eyeData.recomGaze.gazePoint.y},{XVETinit.eyeData.recomGaze.gazePoint.z}\n" +
                    $"recomGaze gazeOrigin:{XVETinit.eyeData.recomGaze.gazeOrigin.x},{XVETinit.eyeData.recomGaze.gazeOrigin.y},{XVETinit.eyeData.recomGaze.gazeOrigin.z}\n" +
                    $"recomGaze gazeDirection:{XVETinit.eyeData.recomGaze.gazeDirection.x},{XVETinit.eyeData.recomGaze.gazeDirection.y},{XVETinit.eyeData.recomGaze.gazeDirection.z}\n" +
                    $"recomGaze re:{XVETinit.eyeData.recomGaze.re}\n" +
                    $"leftGaze gazePoint:{XVETinit.eyeData.leftGaze.gazePoint.x},{XVETinit.eyeData.leftGaze.gazePoint.y},{XVETinit.eyeData.leftGaze.gazePoint.z}\n" +
                    $"leftGaze gazeOrigin:{XVETinit.eyeData.leftGaze.gazeOrigin.x},{XVETinit.eyeData.leftGaze.gazeOrigin.y},{XVETinit.eyeData.leftGaze.gazeOrigin.z}\n" +
                    $"leftGaze gazeDirection:{XVETinit.eyeData.leftGaze.gazeDirection.x},{XVETinit.eyeData.leftGaze.gazeDirection.y},{XVETinit.eyeData.leftGaze.gazeDirection.z}\n" +
                    $"leftGaze re:{XVETinit.eyeData.leftGaze.re}\n" +
                    $"rightGaze gazePoint:{XVETinit.eyeData.rightGaze.gazePoint.x},{XVETinit.eyeData.rightGaze.gazePoint.y},{XVETinit.eyeData.rightGaze.gazePoint.z}\n" +
                    $"rightGaze gazeOrigin:{XVETinit.eyeData.rightGaze.gazeOrigin.x},{XVETinit.eyeData.rightGaze.gazeOrigin.y},{XVETinit.eyeData.rightGaze.gazeOrigin.z}\n" +
                    $"rightGaze gazeDirection:{XVETinit.eyeData.rightGaze.gazeDirection.x},{XVETinit.eyeData.rightGaze.gazeDirection.y},{XVETinit.eyeData.rightGaze.gazeDirection.z}\n" +
                    $"rightGaze re:{XVETinit.eyeData.rightGaze.re}\n" +
                    $"leftPupil pupilCenter:{XVETinit.eyeData.leftPupil.pupilCenter.x},{XVETinit.eyeData.leftPupil.pupilCenter.y}\n" +
                    $"rightPupil pupilCenter:{XVETinit.eyeData.rightPupil.pupilCenter.x},{XVETinit.eyeData.rightPupil.pupilCenter.y}\n" +
                    $"ipd:{XVETinit.eyeData.ipd}\n" +
                    $"leftEyeMove:{XVETinit.eyeData.leftEyeMove}\n" +
                    $"rightEyeMove:{XVETinit.eyeData.rightEyeMove}\n";
            }
        }
    }

    private InteractionObject GetCurrentInteractionObject(string targetName)
    {
        switch (targetName)
        {
            case "Show Manual":
                return manualObject;
            case "Show Models":
                return modelObject;
            case "Start Assembly":
                return assemblyObject;
            default:
                return null;
        }
    }

    private IEnumerator DelayedDeactivation(float delay, string targetName)
    {
        yield return new WaitForSeconds(delay);

        // ����Ŀ�����ƹرն�Ӧ����
        switch (targetName)
        {
            case "Show Manual":
                if (manualObject.targetUI != null) manualObject.targetUI.SetActive(false);
                break;
            case "Show Models":
                if (modelObject.targetUI != null) modelObject.targetUI.SetActive(false);
                break;
            case "Start Assembly":
                if (assemblyObject.targetUI != null) assemblyObject.targetUI.SetActive(false);
                break;
        }

        delayDeactivationCoroutine = null;
    }
}