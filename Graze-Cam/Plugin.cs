using BepInEx;
using Cinemachine;
using Mono.Security.Interface;
using PlayFab.ExperimentationModels;
using System;
using System.Collections;
using System.IO;
using System.Reflection;
using UnityEngine;
using UnityEngine.XR;
using Utilla;

namespace Graze_Cam
{
    [BepInDependency("org.legoandmars.gorillatag.utilla")]
    [BepInPlugin(PluginInfo.GUID, PluginInfo.Name, PluginInfo.Version)]
    public class Plugin : BaseUnityPlugin
    {
        private readonly XRNode rNode = XRNode.RightHand;
        private readonly XRNode lNode = XRNode.LeftHand;
        //GameObject camstore;
        GameObject CamModel;
        Camera cam;
        public int cull;
        public static volatile Plugin Instance;
        CinemachineVirtualCamera defCamsys;
        bool Middle;
        bool firstP;
        bool left;
        bool right;
        bool cooldown;
        bool dropped;
        RenderTexture camscreen;
        bool inLeft;
        bool inRight;
        Camera TexCam;
        GameObject Rhand;
        GameObject Lhand;
        Transform store;
        void Start()
        {
            Utilla.Events.GameInitialized += OnGameInitialized;
            Instance = this;
        }
        void OnGameInitialized(object sender, EventArgs e)
        {
            GameObject.Find("Level").AddComponent<LayerChanger>();
            cam = GameObject.Find("Shoulder Camera").GetComponent<Camera>();
            defCamsys = cam.transform.GetChild(0).GetComponent<CinemachineVirtualCamera>();
            Rhand = GameObject.Find("Global/Local VRRig/Local Gorilla Player/rig/body/shoulder.R/upper_arm.R/forearm.R/hand.R/palm.01.R");
            Lhand = GameObject.Find("Global/Local VRRig/Local Gorilla Player/rig/body/shoulder.L/upper_arm.L/forearm.L/hand.L/palm.01.L");
            Stream str = Assembly.GetExecutingAssembly().GetManifestResourceStream("Graze-Cam.Assets.cam");
            AssetBundle bundle = AssetBundle.LoadFromStream(str);
            GameObject sluber = bundle.LoadAsset<GameObject>("CamModel");
            CamModel = Instantiate(sluber, cam.transform);
            foreach (Transform t in CamModel.transform)
            {
                t.gameObject.layer = LayerMask.NameToLayer("NoMirror");
            }
            camscreen = new RenderTexture(Screen.width, Screen.height, 24);
            TexCam = new GameObject("TexCam").AddComponent<Camera>();
            TexCam.targetTexture = camscreen;
            CamModel.transform.GetChild(4).GetComponent<Renderer>().material.mainTexture = camscreen;
            TexCam.transform.SetParent(cam.transform, false);
            TexCam.farClipPlane = cam.farClipPlane;
            TexCam.nearClipPlane = 0.03f;
            cam.fieldOfView = TexCam.fieldOfView;
        }

        void Update()
        {
            InputDevices.GetDeviceAtXRNode(lNode).TryGetFeatureValue(UnityEngine.XR.CommonUsages.primary2DAxisClick, out left);
            InputDevices.GetDeviceAtXRNode(rNode).TryGetFeatureValue(UnityEngine.XR.CommonUsages.primary2DAxisClick, out right);

            cam.cullingMask = cull;
            TexCam.cullingMask = cull;
           // TexCam.transform.rotation = cam.transform.rotation;

            if (cam.transform.parent == null)
            {
                dropped = true;
            }
            else
            {
                dropped = false;
                cam.transform.rotation = cam.transform.parent.rotation;
            }


            if (cooldown == false)
            {
                if (left && right)
                {
                    if (!Middle) SetMiddle();
                    else TMiddle();

                    cooldown = true;
                    StartCoroutine(Cooldown(.5f));
                }
                if (left && !right)
                {
                    SetLeft();

                    cooldown = true;
                    StartCoroutine(Cooldown(.5f));
                }
                if (!left && right)
                {
                    SetRight();

                    cooldown = true;
                    StartCoroutine(Cooldown(.5f));
                }
            }
           if (inLeft || inRight || !Middle || dropped)
           {
                CamModel.SetActive(true);
           }
            else CamModel.SetActive(false);

            if (inLeft)
            {
                CamModel.transform.localScale = new Vector3 (2f, 2f, 2f);
                cam.transform.localRotation = Quaternion.Euler(0f, 270f, 0f);
            }
            else if(inRight) CamModel.transform.localScale = new Vector3(-2f, 2f, 2f); CamModel.transform.localPosition = new Vector3(0.05f, 0, 0);
        }

        void TMiddle()
        {
            cam.transform.parent = Camera.main.transform;
            firstP = !firstP;
            defCamsys.enabled = !firstP;
            cam.transform.localPosition = Vector3.zero;
        }
        void SetMiddle()
        {
            inLeft = false;
            inRight = false;
            Middle = true;
            TMiddle();
        }

        void DropCam()
        {
            defCamsys.enabled = false;
            cam.transform.parent = null;
            if (inLeft)
            {
                cam.transform.rotation = store.rotation;
            }
            inLeft = false;
            inRight = false;
            Middle = false;
        }

        void SetLeft()
        {
            if (inLeft)
            {
                store = cam.transform;
                store.rotation = Quaternion.Euler(cam.transform.localRotation.x, cam.transform.localRotation.y - 90f, cam.transform.localRotation.z);
                DropCam();
            }
            else
            {
                inLeft = true;
                inRight = false;
                Middle = false;
                defCamsys.enabled = false;
                cam.transform.parent = Lhand.transform;
                cam.transform.localPosition = Vector3.zero;
                //cam.transform.localRotation = Quaternion.Euler(0f, 0f, 270f);
            }
        }

        void SetRight()
        {
            if (inRight)
            {
                DropCam();
            }
            else
            {
                inLeft = false;
                inRight = true;
                Middle = false;
                defCamsys.enabled = false;
                cam.transform.parent = Rhand.transform;
                cam.transform.localPosition = new Vector3(-0.1f, 0.0f, 0.0f);
               // cam.transform.localRotation = Quaternion.Euler(0f, 270f, 90f);
            }
        }
        IEnumerator Cooldown(float sec)
        {
            yield return new WaitForSeconds(sec);
            cooldown = false;
        }

    }

    public class LayerChanger : MonoBehaviour
    {
        void Start()
        {
            if (gameObject.layer == LayerMask.NameToLayer("NoMirror"))
            {
                gameObject.layer = 0;
            }
            if (gameObject.name == "Mirror Backdrop")
            {
                Destroy(gameObject);
            }
            if (gameObject.name == "CameraC")
            {
                Plugin.Instance.cull = GetComponent<Camera>().cullingMask;
                GetComponent<Camera>().farClipPlane = 35;
            }
            foreach (Transform t in gameObject.transform)
            {
                t.gameObject.AddComponent<LayerChanger>();
            }
            Destroy(this);
        }
    }
}
