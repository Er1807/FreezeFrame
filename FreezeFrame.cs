using ActionMenuApi.Api;
using FreezeFrame;
using HarmonyLib;
using MelonLoader;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;
using VRChatUtilityKit.Ui;
using VRChatUtilityKit.Utilities;

[assembly: MelonInfo(typeof(FreezeFrameMod), "FreezeFrame", "1.1.3", "Eric van Fandenfart")]
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
                   delegate {
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

            if(MelonHandler.Mods.Any(x => x.Info.Name == "VRCWSLibary"))
            {
                LoadVRCWS();
            }

        }

        public void LoadVRCWS()
        {
            VRCWSLibaryPresent = true;
            MelonLogger.Msg("Found VRCWSLibary. Initialising Client Functions");
            VRCWSLibaryIntegration.Init(this);
        }

        public static List<AssetBundle> StillLoaded = new List<AssetBundle>();
        public static void PrefixUnload(AssetBundle __instance,ref bool unloadAllLoadedObjects)
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
                VRCWSCreateFreezeOfWrapper(VRCPlayer.field_Internal_Static_VRCPlayer_0.prop_String_2);
                
            InstantiateAvatar(player);
        }

        public void VRCWSCreateFreezeOfWrapper(string attr = "all")
        {
            VRCWSLibaryIntegration.CreateFreezeOf(attr);
        }

        private void Init()
        {
            new SingleButton(GameObject.Find("UserInterface/QuickMenu/UserInteractMenu"),
                            new Vector3(1, 3), "Create\r\nFreeze", delegate
                            {
                                var player = VRCUtils.ActivePlayerInQuickMenu.gameObject;
                                MelonLogger.Msg($"Creating Freeze Frame for selected avatar");
                                EnsureHolderCreated();
                                if (VRCWSLibaryPresent)
                                    VRCWSCreateFreezeOfWrapper(VRCUtils.ActivePlayerInQuickMenu.prop_String_0);
                                InstantiateAvatar(player);
                            },
                            "Create a new Freeze Frame of avatar",
                            "FreezeCreateSingleBtn");

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
                GameObject.Destroy(ClonesParent.gameObject.transform.GetChild(ClonesParent.transform.childCount-1).gameObject);
                if(ClonesParent.transform.childCount == 0)
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

            var rootObjects = SceneManager.GetActiveScene().GetRootGameObjects();
            
            foreach (var item in rootObjects)
            {
                InstantiateAvatar(item);
            }
        }

        public void InstantiateAvatar(GameObject item)
        {
            Transform temp = item.transform.Find("ForwardDirection/Avatar");
            if (temp == null) return;
            var obj = temp.gameObject;
            var copy = GameObject.Instantiate(obj, ClonesParent.transform, true);

            UpdateLayerRecurive(copy);
            UpdateShadersRecurive(copy, obj);

            if (item.layer == LayerMask.NameToLayer("PlayerLocal"))
            {
                foreach (var copycomp in copy.GetComponents<Component>())
                {
                    if (copycomp != copy.transform)
                    {
                        GameObject.Destroy(copycomp);
                    }
                }
            }
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

    }
}
