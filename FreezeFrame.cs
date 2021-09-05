using FreezeFrame;
using MelonLoader;
using UnityEngine;
using UnityEngine.SceneManagement;
using VRChatUtilityKit.Ui;
using VRChatUtilityKit.Utilities;

[assembly: MelonInfo(typeof(FreezeFrameMod), "FreezeFrame", "1.0.2", "Eric")]
[assembly: MelonGame]

namespace FreezeFrame
{

    public class FreezeFrameMod : MelonMod
    {
        public override void OnApplicationStart()
        {
            VRCUtils.OnUiManagerInit += Init;
        }

        private void Init()
        {
            new SingleButton(GameObject.Find("UserInterface/QuickMenu/CameraMenu"),
                            new Vector3(2, 3), "Create\r\nFreeze", delegate
                            {
                                Create();
                            },
                            "Create a new Freeze Frame of all avatars",
                            "FreezeCreateBtn");

            new SingleButton(GameObject.Find("UserInterface/QuickMenu/CameraMenu"),
                             new Vector3(3, 3), "Delete\r\nFreeze", delegate
                             {
                                 Delete();
                             },
                             "Deletes all Freeze Frames",
                             "FreezeDeleteBtn");

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

            new SingleButton(GameObject.Find("UserInterface/QuickMenu/CameraMenu"),
                            new Vector3(4, 1), "Create self\r\nFreeze", delegate
                            {
                                var player = VRCPlayer.field_Internal_Static_VRCPlayer_0.gameObject;
                                MelonLogger.Msg($"Creating Freeze Frame for yourself");
                                EnsureHolderCreated();
                                InstantiateAvatar(player);
                            },
                            "Create a new Freeze Frame of yourself",
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
            if (item.layer == LayerMask.NameToLayer("Player"))
            {
                var obj = item.transform.Find("ForwardDirection/Avatar").gameObject;
                GameObject.Instantiate(obj, ClonesParent.transform, true);
            }
            else if (item.layer == LayerMask.NameToLayer("PlayerLocal"))
            {
                var obj = item.transform.Find("ForwardDirection/Avatar").gameObject;
                var copy = GameObject.Instantiate(obj, ClonesParent.transform, true);
                foreach (var copycomp in copy.GetComponents<Component>())
                {
                    if (!(copycomp is Transform))
                    {
                        GameObject.Destroy(copycomp);
                    }
                }
                UpdateLayerRecurive(copy);
                UpdateShadersRecurive(copy, obj);

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
