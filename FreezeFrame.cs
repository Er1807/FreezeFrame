using ActionMenuApi.Api;
using FreezeFrame;
using HarmonyLib;
using MelonLoader;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using System.ComponentModel;
using VRC.UI.Elements.Menus;
using System.Collections;
using VRC.UI.Elements;
using VRC.DataModel.Core;
using UnityEngine.UI;
using TMPro;
using System.Threading;
using VRC;

[assembly: MelonInfo(typeof(FreezeFrameMod), "FreezeFrame", "1.3.0", "Eric van Fandenfart")]
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
        public GameObject ClonesParent = null;
        private DateTime? DelayedSelf = null;
        private DateTime? DelayedAll = null;

        private bool VRCWSLibaryPresent = false;
        private static bool active = false;
        MelonPreferences_Entry<FreezeType> freezeType;


        

        public override void OnApplicationStart()
        {
            MelonLogger.Msg("Patching AssetBundle unloading");
            HarmonyInstance.Patch(typeof(AssetBundle).GetMethod("Unload"), prefix: new HarmonyMethod(typeof(FreezeFrameMod).GetMethod("PrefixUnload", BindingFlags.Static | BindingFlags.Public)));

            animationModule = new AnimationModule(this);

            VRCActionMenuPage.AddSubMenu(ActionMenuPage.Main,
                   "Freeze Frame Animation",
                   delegate
                   {
                       MelonLogger.Msg("Freeze Frame Menu Opened");
                       CustomSubMenu.AddButton("Freeze All", () => Create());
                       CustomSubMenu.AddButton("Freeze All (5s)", () => DelayedAll = DateTime.Now.AddSeconds(5));
                       CustomSubMenu.AddButton("Delete All", () => Delete());
                       CustomSubMenu.AddButton("Delete Last", () => DeleteLast());
                       CustomSubMenu.AddButton("Freeze Self", () => CreateSelf());
                       CustomSubMenu.AddButton("Freeze Self (5s)", () => DelayedSelf = DateTime.Now.AddSeconds(5));
                       CustomSubMenu.AddToggle("Record", animationModule.Recording, (state) => animationModule.Recording = state);
                       CustomSubMenu.AddButton("Resync Animations", () => Resync());
                   }
               );

            MelonLogger.Msg($"Actionmenu initialised");

            var category = MelonPreferences.CreateCategory("FreezeFrame");
            MelonPreferences_Entry<bool> onlyTrusted = category.CreateEntry("Only Trusted", false);
            freezeType = category.CreateEntry("FreezeType", FreezeType.PerformanceFreeze, display_name: "Freeze Type", description: "Full Freeze is more accurate and copys everything but is less performant");

            if (MelonHandler.Mods.Any(x => x.Info.Name == "VRCWSLibary"))
            {
                LoadVRCWS(onlyTrusted);
            }

            MelonCoroutines.Start(WaitForUIInit());
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

            if (animationModule.Recording)
            {
                animationModule.Record(Player.prop_Player_0.gameObject.transform.Find("ForwardDirection/_AvatarMirrorClone"));
            }
            if(ClonesParent != null && ClonesParent.scene.IsValid())
                foreach (var item in ClonesParent.GetComponentsInChildren<Animation>())
                {
                    if (!item.IsPlaying("FreezeAnimation"))
                        item.Play("FreezeAnimation");
                }

        }

        private void CreateSelf()
        {
            var player = VRCPlayer.field_Internal_Static_VRCPlayer_0.gameObject;
            MelonLogger.Msg($"Creating Freeze Frame for yourself");
            EnsureHolderCreated();
            if (VRCWSLibaryPresent)
                VRCWSCreateFreezeOfWrapper(VRCPlayer.field_Internal_Static_VRCPlayer_0.prop_String_3);

            InstantiateAvatar(player);
        }

        public void VRCWSCreateFreezeOfWrapper(string attr = "all")
        {
            VRCWSLibaryIntegration.CreateFreezeOf(attr);
        }

        private MenuStateController menuStateController;
        private AnimationModule animationModule;

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
                InstantiateByName(userid);
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
            MelonLogger.Msg("Deleting last Freeze Frame");
            if (ClonesParent != null && ClonesParent.scene.IsValid() && ClonesParent.transform.childCount != 0)
            {
                GameObject.Destroy(ClonesParent.gameObject.transform.GetChild(ClonesParent.transform.childCount - 1).gameObject);
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

        public void InstantiateByName(string name)
        {
            foreach (var player in VRC.PlayerManager.field_Private_Static_PlayerManager_0.field_Private_List_1_Player_0)
            {
                if (player.prop_String_0 == name)
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

        public void FullCopyWithAnimations(GameObject player, AnimationClip animationClip)
        {
            EnsureHolderCreated();
            var copy = FullCopy(player);
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
        }
    }
}
