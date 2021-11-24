using MelonLoader;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using UnityEngine.SceneManagement;
using VRC;
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
                client.RegisterEvent("FreezeFrameTaken", (msg) =>
                {
                    EventCall(msg);
                }, signatureRequired: newValue);
            };


            client.RegisterEvent("FreezeFrameTaken", (msg) =>
            {
                EventCall(msg);
            }, signatureRequired: onlyTrusted.Value);

        }

        private static void EventCall(Message msg)
        {
            
            MelonLogger.Msg($"Freeze Frame was taken by {msg.Target} for user {msg.Content}");
            AsyncUtils.ToMain(() => {
                freezeMod.EnsureHolderCreated();
                if (msg.Content == "all")
                {
                    freezeMod.InstantiateAll();
                }
                else
                {
                    freezeMod.InstantiateByName(msg.Content);
                }
            });
            
        }

        public static void CreateFreezeOf(string ofPlayer = "all")
        {
            var rootObjects = SceneManager.GetActiveScene().GetRootGameObjects();
            foreach (var root in rootObjects)
            {
                var player = root.GetComponent<Player>();
                if (player != null && player != Player.prop_Player_0)
                {
                    client.Send(new Message() { Method = "FreezeFrameTaken", Target = player.prop_String_0, Content = ofPlayer });
                }
            }
        }


        // Based on https://github.com/loukylor/VRC-Mods/blob/main/VRChatUtilityKit/Utilities/AsyncUtils.cs
        // By loukylor
        // original by knah
        public static class AsyncUtils
        {
            internal static System.Collections.Concurrent.ConcurrentQueue<Action> _toMainThreadQueue = new System.Collections.Concurrent.ConcurrentQueue<Action>();

            public static void ToMain(Action action)
            {
                _toMainThreadQueue.Enqueue(action);
            }
            
        }
    }
}
