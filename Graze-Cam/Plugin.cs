using BepInEx;
using BepInEx.Configuration;
using Cinemachine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;
using System.Linq;
using TMPro;
using System;
using Photon.Pun;
using GorillaNetworking;

namespace Graze_Cam
{
    [BepInPlugin(PluginInfo.GUID, PluginInfo.Name, PluginInfo.Version)]
    public class Plugin : BaseUnityPlugin
    {
        public static Plugin Instance;

        private Camera useCam;
        public Camera CameraCam => Plugin.Instance.useCam;

        private ConfigEntry<bool> leftStick;

        private CinemachineBrain camBrain;
        private CinemachineVirtualCamera virtualCamera;
        private Cinemachine3rdPersonFollow thirdPFollow;
        private CinemachineSameAsFollowTarget saFollowTarget;

        private GameObject hintCanvas;
        private TextMeshPro textBox;

        private float rotationSpeed = 2.0f;
        private float defaultFOV;
        private int direction;
        private Quaternion targetRotation;
        private bool isRotating, Player, firstPerson, isInitialized, debounce, inFirstPerson, click;

        public Plugin()
        {
            HarmonyPatches.ApplyHarmonyPatches();
            Instance = this;
            leftStick = Config.Bind("Settings", "LeftStick", true);
        }

        private IEnumerator Spawned()
        {
            yield return new WaitForEndOfFrame();
            useCam = GorillaTagger.Instance.thirdPersonCamera.GetComponentInChildren<Camera>();

            int metaReportScreen = 1 << LayerMask.NameToLayer("MetaReportScreen");
            int gorillaSpectator = 1 << LayerMask.NameToLayer("Gorilla Spectator");
            int mirrorOnlyMask = 1 << LayerMask.NameToLayer("MirrorOnly");
            int noMirrorMask = 1 << LayerMask.NameToLayer("NoMirror");

            useCam.cullingMask &= ~metaReportScreen;
            useCam.cullingMask |= gorillaSpectator;
            useCam.cullingMask |= mirrorOnlyMask;
            useCam.cullingMask &= ~noMirrorMask;

            camBrain = useCam.GetComponent<CinemachineBrain>();
            virtualCamera = camBrain.ActiveVirtualCamera.VirtualCameraGameObject.GetComponent<CinemachineVirtualCamera>();
            thirdPFollow = virtualCamera.transform.GetComponentInChildren<Cinemachine3rdPersonFollow>();
            saFollowTarget = virtualCamera.transform.GetComponentInChildren<CinemachineSameAsFollowTarget>();

            saFollowTarget.m_Damping = 1;
            thirdPFollow.Damping = new Vector3(1, -0.5f, 1);
            thirdPFollow.CameraCollisionFilter = GorillaLocomotion.Player.Instance.locomotionEnabledLayers;

            defaultFOV = useCam.fieldOfView;

            isInitialized = true;
        }

        private void Start()
        {
            GorillaTagger.OnPlayerSpawned(() => StartCoroutine(Spawned()));
        }

        private void StartRotation(int dir)
        {
            Player = !Player;
            direction = dir;
            targetRotation = Quaternion.Euler(0, 180 * direction, 0) * thirdPFollow.FollowTarget.transform.localRotation;
            isRotating = true;
        }

        private void UpdateRotation()
        {
            thirdPFollow.FollowTarget.transform.localRotation = Quaternion.RotateTowards(thirdPFollow.FollowTarget.transform.localRotation, targetRotation, rotationSpeed * Time.deltaTime * 180);

            if (Quaternion.Angle(thirdPFollow.FollowTarget.transform.localRotation, targetRotation) < 0.1f)
            {
                thirdPFollow.FollowTarget.transform.localRotation = targetRotation;
                isRotating = false;
            }
        }

        private void AlwaysRun()
        {
            if (hintCanvas == null)
            {
                hintCanvas = Instantiate(Camera.main.transform.FindChildRecursive("DebugCanvas").gameObject, Camera.main.transform.FindChildRecursive("DebugCanvas").parent);
                hintCanvas.transform.localPosition = new Vector3(0.2f, 0.02f, 0.3f);
                hintCanvas.transform.localRotation = Quaternion.Euler(343.5097f, 0, 0);
                textBox = hintCanvas.transform.GetChild(0).GetComponent<TextMeshPro>();
                Destroy(hintCanvas.GetComponent<DebugHudStats>());
                hintCanvas.name = "HintCanvas";
                hintCanvas.layer = LayerMask.NameToLayer("MetaReportScreen");
                textBox.gameObject.layer = LayerMask.NameToLayer("MetaReportScreen");
            }
            else
            {
                hintCanvas.SetActive(true);
                useCam.nearClipPlane = 0.00001f;
                useCam.farClipPlane = float.MaxValue;
                camBrain.enabled = !firstPerson;

                if (!firstPerson)
                {
                    textBox.text = isRotating ? DirToArrow() : debounce ? "" : "";
                }
                if (!isRotating)
                {
                    textBox.text = debounce ? (inFirstPerson ? "FIRST" : $"THIRD {Facing()}") : "";
                }
            }
        }

        private void FixedUpdate()
        {
            if (!isInitialized) return;

            ControllerInputPoller.instance.leftControllerDevice.TryGetFeatureValue(CommonUsages.primary2DAxisClick, out bool lStickClick);
            ControllerInputPoller.instance.rightControllerDevice.TryGetFeatureValue(CommonUsages.primary2DAxisClick, out bool rStickClick);
            click = leftStick.Value ? lStickClick : rStickClick;

            if (click && !debounce && !isRotating)
            {
                debounce = true;
                firstPerson = !firstPerson;
                StartCoroutine(Debounce());
            }

            camBrain.enabled = !firstPerson;

            if (firstPerson)
            {
                if (!inFirstPerson)
                {
                    useCam.transform.SetParent(Camera.main.transform, false);
                    useCam.transform.localPosition = Vector3.zero;
                    useCam.transform.localRotation = Quaternion.Euler(Vector3.zero);
                    foreach (GameObject g in GorillaTagger.Instance.offlineVRRig.overrideCosmetics)
                    {
                        g.layer = LayerMask.NameToLayer("NoMirror");
                    }
                    useCam.fieldOfView = 90;
                    inFirstPerson = true;
                }
                FirstPersonView();
            }
            else
            {
                if (inFirstPerson)
                {
                    useCam.transform.SetParent(GorillaTagger.Instance.thirdPersonCamera.transform, false);
                    useCam.transform.localPosition = Vector3.zero;
                    useCam.transform.localRotation = targetRotation;
                    foreach (GameObject g in GorillaTagger.Instance.offlineVRRig.overrideCosmetics)
                    {
                        g.layer = 0;
                    }
                    useCam.fieldOfView = defaultFOV;
                    inFirstPerson = false;
                }
                ThirdPersonView();
            }

            AlwaysRun();
        }

        private void ThirdPersonView()
        {
            Vector2 primary2DAxis = leftStick.Value ? ControllerInputPoller.instance.leftControllerPrimary2DAxis : ControllerInputPoller.instance.rightControllerPrimary2DAxis;
            if (primary2DAxis.x < -0.5f && !isRotating)
            {
                StartRotation(-1);
            }
            else if (primary2DAxis.x > 0.5f && !isRotating)
            {
                StartRotation(1);
            }

            if (isRotating)
            {
                UpdateRotation();
            }

            float xOffset = Player ? 0.5f : -0.5f;
            thirdPFollow.FollowTarget.transform.localPosition = new Vector3(xOffset, -0.15f, 0);
        }

        private void FirstPersonView()
        {
            useCam.transform.position = new Vector3(Camera.main.transform.position.x, Mathf.Lerp(useCam.transform.position.y, Camera.main.transform.position.y, 75 * Time.deltaTime), Camera.main.transform.position.z);
            useCam.transform.rotation = Quaternion.Slerp(useCam.transform.rotation, Camera.main.transform.rotation, 5f * Time.deltaTime);
        }

        private IEnumerator Debounce()
        {
            yield return new WaitForSecondsRealtime(1.5f);
            debounce = false;
        }

        private string DirToArrow()
        {
            return direction == -1 ? "<--\n" + Facing() : direction == 1 ? "   -->\n" + Facing() : "";
        }

        private string Facing()
        {
            return Player ? "FACING" : "BEHIND";
        }
    }
}
