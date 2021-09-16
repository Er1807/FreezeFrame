using ActionMenuApi.Api;
using FreezeFrame;
using MelonLoader;
using UnityEngine;
using UnityEngine.SceneManagement;
using VRChatUtilityKit.Ui;
using VRChatUtilityKit.Utilities;

[assembly: MelonInfo(typeof(FreezeFrameMod), "FreezeFrame", "1.0.4", "Eric van Fandenfart")]
[assembly: MelonGame]

namespace FreezeFrame
{

    public class FreezeFrameMod : MelonMod
    {
        public override void OnApplicationStart()
        {
            VRCActionMenuPage.AddSubMenu(ActionMenuPage.Main,
                   "Freeze Frame Animation",
                   delegate {
                       MelonLogger.Msg("Freeze Frame Menu Opened");
                       CustomSubMenu.AddButton("Freeze All", () => Create());
                       CustomSubMenu.AddButton("Delete All", () => Delete());
                       CustomSubMenu.AddButton("Freeze Self", () =>
                       {
                           var player = VRCPlayer.field_Internal_Static_VRCPlayer_0.gameObject;
                           MelonLogger.Msg($"Creating Freeze Frame for yourself");
                           EnsureHolderCreated();
                           InstantiateAvatar(player);
                       });
                   }
               );
            VRCUtils.OnUiManagerInit += Init;

            MelonLogger.Msg($"Actionmenu initialised");
        }

        private void Init()
        {
            new SingleButton(GameObject.Find("UserInterface/QuickMenu/UserInteractMenu"),
                            new Vector3(1, 3), "Create\r\nFreeze", delegate
                            {
                                var player = VRCUtils.ActivePlayerInQuickMenu.gameObject;
                                MelonLogger.Msg($"Creating Freeze Frame for selected avatar");
                                EnsureHolderCreated();
                                InstantiateAvatar(player);
                            },
                            "Create a new Freeze Frame of avatar",
                            "FreezeCreateSingleBtn");

            MelonLogger.Msg("Buttons sucessfully created");
        }

        GameObject ClonesParent = null;

        public void Delete()
        {
            MelonLogger.Msg("Deleting all Freeze Frames");
            if (ClonesParent != null && ClonesParent.scene.IsValid())
            {
                GameObject.Destroy(ClonesParent);
                ClonesParent = null;
            }
        }

        public void EnsureHolderCreated()
        {
            if (ClonesParent == null || !ClonesParent.scene.IsValid())
            {
                ClonesParent = new GameObject("Avatar Clone Holder");
            }
        }

        public void Create()
        {
            MelonLogger.Msg("Creating Freeze Frame for all Avatars");
            EnsureHolderCreated();
            var rootObjects = SceneManager.GetActiveScene().GetRootGameObjects();

            foreach (var item in rootObjects)
            {
                InstantiateAvatar(item);
            }
        }

        private void InstantiateAvatar(GameObject item)
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
                    if (!(copycomp is Transform))
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
