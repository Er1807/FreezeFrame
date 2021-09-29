using MelonLoader;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine.SceneManagement;
using VRChatUtilityKit.Utilities;
using VRCWSLibary;

namespace FreezeFrame
{
    class VRCWSLibaryIntegration
    {

        public static void Init(FreezeFrameMod freeze, MelonPreferences_Entry<bool> onlyTrustedP)
        {
            freezeMod = freeze;
            onlyTrusted = onlyTrustedP;
            MelonCoroutines.Start(LoadClient());
        }

        private static Client client;
        private static FreezeFrameMod freezeMod;
        private static MelonPreferences_Entry<bool> onlyTrusted;
        private static IEnumerator LoadClient()
        {
            while (!Client.ClientAvailable())
                yield return null;


            client = Client.GetClient();

            onlyTrusted.OnValueChanged += (_, newValue) =>
            {
                client.RemoveEvent("FreezeFrameTaken");
                client.RegisterEvent("FreezeFrameTaken", async (msg) =>
                {
                    await EventCall(msg);
                }, signatureRequired: newValue);
            };


            client.RegisterEvent("FreezeFrameTaken", async (msg) =>
            {
                await EventCall(msg);
            }, signatureRequired: onlyTrusted.Value);

        }

        private static async Task EventCall(Message msg)
        {
            MelonLogger.Msg($"Freeze Frame was taken by {msg.Target} for user {msg.Content}");
            await AsyncUtils.YieldToMainThread();
            freezeMod.EnsureHolderCreated();
            var rootObjects = SceneManager.GetActiveScene().GetRootGameObjects();
            if (msg.Content == "all")
            {
                foreach (var item in rootObjects)
                {
                    freezeMod.InstantiateAvatar(item);
                }
            }
            else
            {
                foreach (var item in rootObjects)
                {
                    if (item.GetComponent<VRCPlayer>()?.prop_String_2 == msg.Content)
                        freezeMod.InstantiateAvatar(item);
                }
            }
        }

        public static void CreateFreezeOf(string ofPlayer = "all")
        {
            var rootObjects = SceneManager.GetActiveScene().GetRootGameObjects();
            foreach (var root in rootObjects)
            {
                var player = root.GetComponent<VRCPlayer>();
                if (player != null && player != VRCPlayer.field_Internal_Static_VRCPlayer_0)
                {
                    client.Send(new Message() { Method = "FreezeFrameTaken", Target = player.prop_String_2, Content = ofPlayer });
                }
            }
        }
    }
}
