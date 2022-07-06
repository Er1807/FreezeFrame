using ActionMenuApi.Api;
using FreezeFrame;
using HarmonyLib;
using MelonLoader;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using System.ComponentModel;
using VRC.UI.Elements.Menus;
using System.Collections;
using UnityEngine.UI;
using TMPro;
using System.IO;
using UnhollowerRuntimeLib;
using UnityEngine;
using VRC;
using VRC.SDK3.Dynamics.PhysBone.Components;
using VRC.Dynamics;
using VRC.UI.Elements;

[assembly: MelonInfo(typeof(FreezeFrameMod), "FreezeFrame", "1.4.0", "Eric van Fandenfart")]
[assembly: MelonAdditionalDependencies("ActionMenuApi")]
[assembly: MelonOptionalDependencies("VRCWSLibary")]
[assembly: MelonGame]

namespace FreezeFrame
{
    public enum FreezeType
    {
        [Description("Full Freeze")]
        FullFreeze,
        [Description("Performance Freeze")]
        PerformanceFreeze
    }

    public class FreezeFrameMod : MelonMod
    {

        static FreezeFrameMod()
        {
            try
            {
                //Adapted from knah's JoinNotifier mod found here: https://github.com/knah/VRCMods/blob/master/JoinNotifier/JoinNotifierMod.cs 
                using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("FreezeFrame.icons-freeze"))
                using (var tempStream = new MemoryStream((int)stream.Length))
                {
                    stream.CopyTo(tempStream);
                    iconsAssetBundle = AssetBundle.LoadFromMemory_Internal(tempStream.ToArray(), 0);
                    iconsAssetBundle.hideFlags |= HideFlags.DontUnloadUnusedAsset;
                }
            }
            catch (Exception e)
            {
                MelonLogger.Warning("Consider checking for newer version as mod possibly no longer working, Exception occured OnAppStart(): " + e.Message);
            }
        }

        public Texture2D LoadImage(string name)
        {
            return iconsAssetBundle.LoadAsset_Internal($"Assets/icons-freeze/{name}.png", Il2CppType.Of<Texture2D>()).Cast<Texture2D>();
        }

        public GameObject ClonesParent = null;
        private DateTime? DelayedSelf = null;
        private DateTime? DelayedAll = null;

        public bool VRCWSLibaryPresent = false;
        private static bool active = false;
        public MelonPreferences_Entry<FreezeType> freezeType;
        public MelonPreferences_Entry<bool> allowRemoteRecording;
        public MelonPreferences_Entry<bool> recordBlendshapes;
        public MelonPreferences_Entry<int> skipFrames;
        public MelonPreferences_Entry<float> smoothLoopingDuration;
        
        public AnimationModule animationModule;

        private bool deleteMode;


        public override void OnApplicationStart()
        {
            MelonLogger.Msg("Patching AssetBundle unloading");
            HarmonyInstance.Patch(typeof(AssetBundle).GetMethod("Unload"), prefix: new HarmonyMethod(typeof(FreezeFrameMod).GetMethod("PrefixUnload", BindingFlags.Static | BindingFlags.Public)));

            animationModule = new AnimationModule(this);

            MelonCoroutines.Start(RecordingCoroutine());
            var freeze = LoadImage("freeze");
            freeze.hideFlags |= HideFlags.DontUnloadUnusedAsset;

            var category = MelonPreferences.CreateCategory("FreezeFrame");
            MelonPreferences_Entry<bool> onlyTrusted = category.CreateEntry("Only Trusted", false);
            freezeType = category.CreateEntry("FreezeType", FreezeType.PerformanceFreeze, display_name: "Freeze Type", description: "Full Freeze is more accurate and copys everything but is less performant");
            recordBlendshapes = category.CreateEntry("recordBlendshapes", true, display_name: "Record Blendshapes", description: "Blendshape Recording can quite limit the performance you can disable it here");
            skipFrames = category.CreateEntry("skipFrames", 0, display_name: "Skip Frames", description: "Amount of frames to skip between recordings");

            MelonPreferences_Entry<bool> showInModMenu = category.CreateEntry("UseModMenu", false, "Use the AM Mods Category");
            if (showInModMenu.Value)
                AMUtils.AddToModsFolder("Freeze Frame Animation", CreateActionMenu, freeze);
            else
                VRCActionMenuPage.AddSubMenu(ActionMenuPage.Main, "Freeze Frame Animation", CreateActionMenu, freeze);
            
            smoothLoopingDuration = category.CreateEntry("smoothLoopingDuration", 0.2f, "Smoothing Duration", "Duration of loop smoothing");

            MelonLogger.Msg($"Actionmenu initialised");

            if (MelonHandler.Mods.Any(x => x.Info.Name == "VRCWSLibary"))
            {
                allowRemoteRecording = category.CreateEntry("allowRemoteRecording", true, display_name: "Allow Remote Recording (VRCWS)", description: "Since Recordings can quite limit the performance you can disable it here");

                LoadVRCWS(onlyTrusted);
            }

            MelonCoroutines.Start(WaitForUIInit());
        }

        private void CreateActionMenu()
        {
            MelonLogger.Msg("Freeze Frame Menu Opened");
            CustomSubMenu.AddButton("Delete Last", DeleteLast, LoadImage("delete last"));
            CustomSubMenu.AddButton("Delete First", () => Delete(0), LoadImage("delete first"));
            CustomSubMenu.AddButton("Freeze Self", CreateSelf, LoadImage("freeze"));
            CustomSubMenu.AddToggle("Record", animationModule.Recording, (state) =>
            {
                if (state) animationModule.StartRecording(Player.prop_Player_0);
                else animationModule.StopRecording();
            }, LoadImage("record"));

            CustomSubMenu.AddButton("Resync Animations", Resync, LoadImage("resync"));
            CustomSubMenu.AddSubMenu("Advanced", delegate
            {
                CustomSubMenu.AddButton("Freeze Self (5s)", () => DelayedSelf = DateTime.Now.AddSeconds(5), LoadImage("freeze 5sec"));
                CustomSubMenu.AddButton("Delete All", Delete, LoadImage("delete all"));
                CustomSubMenu.AddButton("Freeze All", Create, LoadImage("freeze all"));
                CustomSubMenu.AddButton("Freeze All (5s)", () => DelayedAll = DateTime.Now.AddSeconds(5), LoadImage("freeze all 5sec"));
                CustomSubMenu.AddToggle("Record Time Limit", animationModule.Recording, (state) =>
                {
                    if (state) animationModule.StartRecording(Player.prop_Player_0);
                    else animationModule.StopRecording(isMain: true);
                }, LoadImage("record"));
                CustomSubMenu.AddToggle("Delete Mode", deleteMode, SwitchDeleteMode, LoadImage("delete mode")); ;
            }, LoadImage("advanced"));
        }

        private void SwitchDeleteMode(bool state)
        {
            deleteMode = state;
            EnsureHolderCreated();
            if (deleteMode)
            {
                for (int i = 0; i < ClonesParent.transform.childCount; i++)
                {
                    var collidor = ClonesParent.transform.GetChild(i).gameObject.AddComponent<BoxCollider>();
                    collidor.center = new Vector3(0, 0.7f, 0);
                    collidor.size = new Vector3(0.2f, 1, 0.2f);
                }
                foreach (var item in ClonesParent.GetComponentsInChildren<Animation>())
                {
                    item.Stop();
                }
            }
            else
            {
                for (int i = 0; i < ClonesParent.transform.childCount; i++)
                {
                    var collidor = ClonesParent.transform.GetChild(i).gameObject.GetComponent<BoxCollider>();
                    GameObject.Destroy(collidor);
                }
            }
        }

        public void Resync()
        {
            if (ClonesParent != null && ClonesParent.scene.IsValid())
                foreach (var item in ClonesParent.GetComponentsInChildren<Animation>())
                {
                    item.Stop();
                }
        }

        private IEnumerator WaitForUIInit()
        {
            while (VRCUiManager.prop_VRCUiManager_0 == null)
                yield return null;
            while (GameObject.Find("UserInterface").transform.Find("Canvas_QuickMenu(Clone)/Container/Window/QMParent") == null)
                yield return null;

            LoadUI();
        }

        public void LoadVRCWS(MelonPreferences_Entry<bool> onlyTrusted)
        {
            VRCWSLibaryPresent = true;
            MelonLogger.Msg("Found VRCWSLibary. Initialising Client Functions");
            VRCWSLibaryIntegration.Init(this, onlyTrusted);
        }

        public static List<AssetBundle> StillLoaded = new List<AssetBundle>();

        public static void PrefixUnload(AssetBundle __instance, ref bool unloadAllLoadedObjects)
        {
            if (!active)
                return;

            unloadAllLoadedObjects = false;
            StillLoaded.Add(__instance);
        }

        public override void OnUpdate()
        {
            if (VRCWSLibaryIntegration.AsyncUtils._toMainThreadQueue.TryDequeue(out Action result))
                result.Invoke();

            if (DelayedSelf.HasValue && DelayedSelf < DateTime.Now)
            {
                DelayedSelf = null;
                CreateSelf();
            }
            if (DelayedAll.HasValue && DelayedAll < DateTime.Now)
            {
                DelayedAll = null;
                Create();
            }

            //if (animationModule.Recording)
            //{
            //    if(Time.frameCount % (skipFrames.Value + 1) == 0)
            //        animationModule.Record();

            //    AnimationModule.CurrentTime += Time.deltaTime;
            //}
            if (ClonesParent != null && ClonesParent.scene.IsValid())
                foreach (var anim in ClonesParent.GetComponentsInChildren<Animation>())
                {
                    if (!anim.IsPlaying("FreezeAnimation") && !deleteMode)
                    {
                        anim.Play("FreezeAnimation");
                        if (anim.gameObject.GetComponent<VRC_FreezeData>().IsMain)
                        {
                            foreach (var anim2 in ClonesParent.GetComponentsInChildren<Animation>())
                            {
                                anim2.Stop();
                                anim2.Play("FreezeAnimation");
                            }

                        }
                    }
                }
        }
        public IEnumerator RecordingCoroutine()
        {
            while (true)
            {
                yield return new WaitForEndOfFrame();

                if (animationModule.Recording)
                {
                    if (Time.frameCount % (skipFrames.Value + 1) == 0)
                        animationModule.Record();

                    AnimationModule.CurrentTime += Time.deltaTime;
                }
            }
        }

        private void CreateSelf()
        {
            var player = VRCPlayer.field_Internal_Static_VRCPlayer_0.gameObject;
            MelonLogger.Msg($"Creating Freeze Frame for yourself");
            EnsureHolderCreated();
            if (VRCWSLibaryPresent)
                VRCWSCreateFreezeOfWrapper(Player.prop_Player_0.field_Private_APIUser_0.id);

            InstantiateAvatar(player);
        }

        public void VRCWSCreateFreezeOfWrapper(string attr = "all")
        {
            VRCWSLibaryIntegration.CreateFreezeOf(FreezeAction.Freeze, attr);
        }

        private MenuStateController menuStateController;
        private static AssetBundle iconsAssetBundle;

        private void LoadUI()
        {
            menuStateController = GameObject.Find("UserInterface").transform.Find("Canvas_QuickMenu(Clone)").GetComponent<MenuStateController>();
            //based on VRCUKs code
            var camera = menuStateController.transform.Find("Container/Window/QMParent/Menu_Camera/Scrollrect/Viewport/VerticalLayoutGroup/Buttons/Button_Screenshot");
            var useractions = menuStateController.transform.Find("Container/Window/QMParent/Menu_SelectedUser_Local/ScrollRect/Viewport/VerticalLayoutGroup/Buttons_UserActions");
            var createFreezeButton = GameObject.Instantiate(camera, useractions);
            createFreezeButton.GetComponent<Button>().onClick.RemoveAllListeners();
            createFreezeButton.GetComponent<Button>().onClick.AddListener(new Action(() =>
            {
                MelonLogger.Msg($"Creating Freeze Frame for selected avatar");
                EnsureHolderCreated();
                string userid = menuStateController.GetComponentInChildren<SelectedUserMenuQM>().field_Private_IUser_0.prop_String_0;
                if (VRCWSLibaryPresent)
                    VRCWSCreateFreezeOfWrapper(userid);
                InstantiateByID(userid);
            }));
            createFreezeButton.GetComponentInChildren<TextMeshProUGUI>().text = "Create Freeze";



            MelonLogger.Msg("Buttons sucessfully created");
        }

        public void Delete()
        {
            MelonLogger.Msg("Deleting all Freeze Frames");
            if (ClonesParent != null && ClonesParent.scene.IsValid())
            {
                GameObject.Destroy(ClonesParent);
                ClonesParent = null;
                active = false;
                MelonLogger.Msg("Cleanup after all Freezes are gone");
                foreach (var item in StillLoaded)
                {
                    item.Unload(true);
                }
                StillLoaded.Clear();
            }
        }

        public void DeleteLast()
        {
            EnsureHolderCreated();
            Delete(ClonesParent.transform.childCount - 1);
        }

        public void Delete(int i)
        {
            EnsureHolderCreated();
            if (ClonesParent.transform.childCount > i && i >= 0)
            {
                MelonLogger.Msg($"Deleting Frame {i}");
                GameObject.DestroyImmediate(ClonesParent.gameObject.transform.GetChild(i).gameObject);
                if (ClonesParent.transform.childCount == 0)
                {
                    active = false;
                    MelonLogger.Msg("Cleanup after all Freezes are gone");
                    foreach (var item in StillLoaded)
                        item.Unload(true);
                }
            }
        }

        public void EnsureHolderCreated()
        {
            active = true;
            if (ClonesParent == null || !ClonesParent.scene.IsValid())
            {
                ClonesParent = new GameObject("Avatar Clone Holder");
            }
        }

        public void Create()
        {
            MelonLogger.Msg("Creating Freeze Frame for all Avatars");
            EnsureHolderCreated();
            if (VRCWSLibaryPresent)
                VRCWSCreateFreezeOfWrapper();

            InstantiateAll();
        }

        public void InstantiateByID(string id)
        {
            foreach (var player in PlayerManager.field_Private_Static_PlayerManager_0.field_Private_List_1_Player_0)
            {
                if (player.field_Private_APIUser_0.id == id)
                {
                    InstantiateAvatar(player.gameObject);
                    return;
                }
            }
        }


        public void InstantiateAll()
        {
            foreach (var player in VRC.PlayerManager.field_Private_Static_PlayerManager_0.field_Private_List_1_Player_0)
                InstantiateAvatar(player.gameObject);
        }

        public void InstantiateAvatar(GameObject item)
        {
            if (item == null)
                return;

            if (freezeType.Value == FreezeType.FullFreeze)
                FullCopy(item);
            else
                PerformantCopy(item);


        }

        public void FullCopyWithAnimations(GameObject player, AnimationClip animationClip, bool isMain)
        {
            EnsureHolderCreated();
            var copy = FullCopy(player);
            if (isMain == true)
                ClonesParent.GetComponentsInChildren<VRC_FreezeData>().Do(x => x.IsMain = false);
            copy.AddComponent<VRC_FreezeData>().IsMain = isMain;

            var animator = copy.AddComponent<Animation>();
            animator.AddClip(animationClip, animationClip.name);
            animator.Play(animationClip.name);
        }

        public GameObject FullCopy(GameObject player)
        {
            Transform temp = player.transform.Find("ForwardDirection/Avatar");
            var obj = temp.gameObject;
            var copy = GameObject.Instantiate(obj, ClonesParent.transform, true);
            copy.name = "Avatar Clone";
            UpdateLayerRecurive(copy);
            UpdateShadersRecurive(copy, obj);

            if (obj.layer == LayerMask.NameToLayer("PlayerLocal"))
            {
                foreach (var copycomp in copy.GetComponents<UnityEngine.Component>())
                {
                    if (copycomp != copy.transform)
                    {
                        GameObject.Destroy(copycomp);
                    }
                }
            }

            var pbComponents = copy.GetComponentsInChildren<VRCPhysBone>();
            foreach (var pb in pbComponents)
            {
                var byteArray = Guid.NewGuid().ToByteArray();
                pb.chainId = BitConverter.ToUInt64(byteArray, 0);
                PhysBoneManager.Inst.AddPhysBone(pb);
            }

            copy.GetComponentsInChildren<SkinnedMeshRenderer>().Do(x => x.castShadows = true);

            VRC_UdonTrigger.Instantiate(copy, "Delete", () => GameObject.Destroy(copy));
            return copy;
        }

        public void UpdateLayerRecurive(GameObject obj)
        {
            obj.layer = LayerMask.NameToLayer("Player");
            //Restore head. Hope no one changes the scale from 1,1,1
            if (obj.transform.localScale == new Vector3(0.0001f, 0.0001f, 0.0001f))
            {
                obj.transform.localScale = new Vector3(1, 1, 1);
            }

            for (int i = 0; i < obj.transform.GetChildCount(); i++)
            {
                UpdateLayerRecurive(obj.transform.GetChild(i).gameObject);
            }

        }

        public void UpdateShadersRecurive(GameObject copy, GameObject original)
        {
            Renderer copyRenderer = copy.GetComponent<Renderer>();
            Renderer orginalRenderer = original.GetComponent<Renderer>();
            if (copyRenderer != null && orginalRenderer != null)
            {
                MaterialPropertyBlock p = new MaterialPropertyBlock();

                orginalRenderer.GetPropertyBlock(p);
                copyRenderer.SetPropertyBlock(p);
            }


            for (int i = 0; i < copy.transform.GetChildCount(); i++)
            {
                UpdateShadersRecurive(copy.transform.GetChild(i).gameObject, original.transform.GetChild(i).gameObject);
            }

        }

        public void PerformantCopy(GameObject player)
        {
            var isLocal = player.GetComponent<VRCPlayer>().prop_VRCPlayerApi_0.isLocal;
            var source = player.transform.Find(isLocal ? "ForwardDirection/_AvatarMirrorClone" : "ForwardDirection/Avatar");

            var avatar = new GameObject("Avatar Clone");
            avatar.layer = LayerMask.NameToLayer("Player");
            avatar.transform.SetParent(ClonesParent.transform);

            // Get all the SkinnedMeshRenderers that belong to this avatar
            foreach (var renderer in source.GetComponentsInChildren<Renderer>())
            {
                // Bake all the SkinnedMeshRenderers that belong to this avatar
                if (renderer.TryCast<SkinnedMeshRenderer>() is SkinnedMeshRenderer skinnedMeshRenderer)
                {
                    MelonLogger.Msg(renderer.gameObject.name);
                    // Create a new GameObject to house our mesh
                    var holder = new GameObject("Mesh Holder for " + skinnedMeshRenderer.name);
                    holder.layer = LayerMask.NameToLayer("Player");
                    holder.transform.SetParent(avatar.transform, false);

                    // Fix mesh location
                    holder.transform.position = skinnedMeshRenderer.transform.position;
                    holder.transform.rotation = skinnedMeshRenderer.transform.rotation;

                    // Bake the current pose
                    var mesh = new Mesh();
                    skinnedMeshRenderer.BakeMesh(mesh);

                    // setup the rendering components;
                    holder.AddComponent<MeshFilter>().mesh = mesh;
                    var target = holder.AddComponent<MeshRenderer>();
                    target.materials = skinnedMeshRenderer.materials;
                    var propertyBlock = new MaterialPropertyBlock();
                    skinnedMeshRenderer.GetPropertyBlock(propertyBlock);
                    target.SetPropertyBlock(propertyBlock);
                }
                else
                {
                    var holder = GameObject.Instantiate(renderer.gameObject, avatar.transform, true);
                    holder.layer = LayerMask.NameToLayer("Player");
                }
            }
            foreach (var lightsource in source.GetComponentsInChildren<Light>())
            {
                if (lightsource.isActiveAndEnabled)
                {
                    var holder = new GameObject("Wholesome Light Holder");
                    holder.layer = LayerMask.NameToLayer("Player");
                    holder.transform.SetParent(avatar.transform, false);
                    Light copy = holder.AddComponent<Light>();
                    copy.intensity = lightsource.intensity;
                    copy.range = lightsource.range;
                    copy.shape = lightsource.shape;
                    copy.attenuate = lightsource.attenuate;
                    copy.color = lightsource.color;
                    copy.colorTemperature = lightsource.colorTemperature;
                    holder.transform.position = lightsource.transform.position;
                    holder.transform.rotation = lightsource.transform.rotation;
                }
            }

            VRC_UdonTrigger.Instantiate(avatar, "Delete", () => GameObject.Destroy(avatar));
        }
    }
}
