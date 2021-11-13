using ActionMenuApi.Api;
using FreezeFrame;
using HarmonyLib;
using MelonLoader;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using VRChatUtilityKit.Ui;
using VRChatUtilityKit.Utilities;
using TMPro;
using UnityEngine.UI;

[assembly: MelonInfo(typeof(FreezeFrameMod), "FreezeFrame", "1.2.2", "Eric van Fandenfart")]
[assembly: MelonAdditionalDependencies("VRChatUtilityKit", "ActionMenuApi")]
[assembly: MelonOptionalDependencies("VRCWSLibary")]
[assembly: MelonGame]

namespace FreezeFrame
{

    public class FreezeFrameMod : MelonMod
    {
        private GameObject ClonesParent = null;
        private DateTime? DelayedSelf = null;
        private DateTime? DelayedAll = null;

        private bool VRCWSLibaryPresent = false;
        private static bool active = false;

        public override void OnApplicationStart()
        {
            MelonLogger.Msg("Patching AssetBundle unloading");
            HarmonyInstance.Patch(typeof(AssetBundle).GetMethod("Unload"), prefix: new HarmonyMethod(typeof(FreezeFrameMod).GetMethod("PrefixUnload", BindingFlags.Static | BindingFlags.Public)));

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
                   }
               );
            VRCUtils.OnUiManagerInit += Init;

            MelonLogger.Msg($"Actionmenu initialised");

            var category = MelonPreferences.CreateCategory("FreezeFrame");
            MelonPreferences_Entry<bool> onlyTrusted = category.CreateEntry("Only Trusted", false);

            if (MelonHandler.Mods.Any(x => x.Info.Name == "VRCWSLibary"))
            {
                LoadVRCWS(onlyTrusted);
            }
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

        private void Init()
        {
            UiManager.AddButtonToExistingGroup(UiManager.QMStateController.transform.Find("Container/Window/QMParent/Menu_SelectedUser_Local/ScrollRect/Viewport/VerticalLayoutGroup/Buttons_UserActions").gameObject, new SingleButton(new Action(() => {
                MelonLogger.Msg($"Creating Freeze Frame for selected avatar");
                EnsureHolderCreated();
                if (VRCWSLibaryPresent)
                    VRCWSCreateFreezeOfWrapper(VRCUtils.ActiveUserInUserInfoMenu.ToIUser().prop_String_0);
                InstantiateByName(VRCUtils.ActiveUserInUserInfoMenu.ToIUser().prop_String_0);
            }), null, "Create Freeze", "CreateFreeze"));
            
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
                if (player.prop_VRCPlayer_0.prop_String_3 == name)
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
            var layer = LayerMask.NameToLayer("Player");
            // Use mirror clone for self
            var isLocal = item.GetComponent<VRCPlayer>().prop_VRCPlayerApi_0.isLocal;
            var source = item.transform.Find(isLocal ? "ForwardDirection/_AvatarMirrorClone" : "ForwardDirection/Avatar");
            if (source == null)
                return;

            var avatar = new GameObject("Avatar Clone");
            avatar.layer = layer;
            avatar.transform.SetParent(ClonesParent.transform);

            // Get all the SkinnedMeshRenderers that belong to this avatar
            foreach (var renderer in source.GetComponentsInChildren<Renderer>())
            {
                // Bake all the SkinnedMeshRenderers that belong to this avatar
                if (renderer.TryCast<SkinnedMeshRenderer>() is SkinnedMeshRenderer skinnedMeshRenderer)
                {
                    // Create a new GameObject to house our mesh
                    var holder = new GameObject("Mesh Holder for " + skinnedMeshRenderer.name);
                    holder.layer = layer;
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
                    holder.layer = layer;
                }
            }
        }
    }
}
