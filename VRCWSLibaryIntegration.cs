using MelonLoader;
using Newtonsoft.Json;
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
    public enum FreezeAction
    {
        Freeze, StartAnim, StopAnim
    }

    class VRCWSLibaryIntegration
    {
        
        public class Datamodel
        {
            public string Target { get; set; }
            public FreezeAction Action { get; set; }
        }

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
            var data = msg.GetContentAs<Datamodel>();
            if (data == null)
                return;
            MelonLogger.Msg($"Freeze Frame was taken by {msg.Target} for user {data.Target} with mode {data.Action}");
            AsyncUtils.ToMain(() => {
                freezeMod.EnsureHolderCreated();
                if (data.Action == FreezeAction.Freeze)
                {
                    if (data.Target == "all")
                    {
                        freezeMod.InstantiateAll();
                    }
                    else
                    {
                        freezeMod.InstantiateByID(data.Target);
                    }
                }
                else if (data.Action == FreezeAction.StartAnim)
                {
                    foreach (var player in PlayerManager.field_Private_Static_PlayerManager_0.field_Private_List_1_Player_0)
                    {
                        if (player.field_Private_APIUser_0.id == data.Target)
                        {
                            freezeMod.LoggerInstance.Msg("Found player to record");
                            freezeMod.animationModule.StartRecording(player, true);
                            break;
                        }
                    }
                }
                else if (data.Action == FreezeAction.StopAnim)
                {
                    freezeMod.animationModule.StopRecording(true);
                }

            });
            
        }

        public static void CreateFreezeOf(FreezeAction action, string ofPlayer = "all")
        {
            var rootObjects = SceneManager.GetActiveScene().GetRootGameObjects();
            foreach (var root in rootObjects)
            {
                var player = root.GetComponent<Player>();
                if (player != null && player != Player.prop_Player_0)
                {
                    client.Send(new Message() { 
                        Method = "FreezeFrameTaken", 
                        Target = player.field_Private_APIUser_0.id, 
                        Content = JsonConvert.SerializeObject(new Datamodel() { Action = action, Target = ofPlayer }) });
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
