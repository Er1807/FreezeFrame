using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using VRC;

namespace FreezeFrame
{


    public class AnimationModule
    {

        public class AnimationContainer
        {
            public string Path;
            public string Property;
            public AnimationCurve Curve = new AnimationCurve();
            public float lastFrame = 0;

            public AnimationContainer(string path, string property)
            {
                Path = path;
                Property = property;
            }


            public void Record(float value)
            {
                Curve.AddKey(lastFrame, value);

                lastFrame += Time.deltaTime;
            }
        }

        public AnimationModule(FreezeFrameMod fr) => freezeFrame = fr;

        public FreezeFrameMod freezeFrame;

        public Dictionary<(string, string), AnimationContainer> AnimationsCache = new Dictionary<(string, string), AnimationContainer>();


        private bool _recording = false;
        public bool Recording
        {
            get => _recording;
            set
            {
                _recording = value;
                if (value)
                {
                    AnimationsCache.Clear();
                    freezeFrame.Resync();
                }
                else
                {
                    freezeFrame.FullCopyWithAnimations(Player.prop_Player_0.gameObject, CreateClip());
                }
            }
        }


        public AnimationClip CreateClip()
        {
            AnimationClip clip = new AnimationClip();
            clip.legacy = true;
            clip.name = "FreezeAnimation";

            var type = UnhollowerRuntimeLib.Il2CppType.Of<Transform>();
            foreach (var item in AnimationsCache)
            {
                clip.SetCurve(item.Value.Path, type, item.Value.Property, item.Value.Curve);
            }


            return clip;
        }


        public void Record(Transform transform, string path = "")
        {
            if(path == "")
            {
                Save(path, "localPosition.x", transform.position.x);
                Save(path, "localPosition.y", transform.position.y);
                Save(path, "localPosition.z", transform.position.z);
            }
            else
            {
                Save(path, "localPosition.x", transform.localPosition.x);
                Save(path, "localPosition.y", transform.localPosition.y);
                Save(path, "localPosition.z", transform.localPosition.z);
            }

            if (path == "")
            {
                Save(path, "localRotation.x", transform.rotation.x);
                Save(path, "localRotation.y", transform.rotation.y);
                Save(path, "localRotation.z", transform.rotation.z);
                Save(path, "localRotation.w", transform.rotation.w);
            }
            else
            {
                Save(path, "localRotation.x", transform.localRotation.x);
                Save(path, "localRotation.y", transform.localRotation.y);
                Save(path, "localRotation.z", transform.localRotation.z);
                Save(path, "localRotation.w", transform.localRotation.w);
            }


            Save(path, "localScale.x", transform.localScale.x);
            Save(path, "localScale.y", transform.localScale.y);
            Save(path, "localScale.z", transform.localScale.z);

            for (int i = 0; i < transform.childCount; i++)
            {
                if (path == "")

                    Record(transform.GetChild(i), path + transform.GetChild(i).name);
                else
                    Record(transform.GetChild(i), path + $"/{transform.GetChild(i).name}");


            }
        }

        private void Save(string path, string propertyName, float value)
        {
            AnimationContainer container;
            if (!AnimationsCache.TryGetValue((path, propertyName), out container))
            {
                container = new AnimationContainer(path, propertyName);
                AnimationsCache[(path, propertyName)] = container;
            }

            container.Record(value);
        }
    }
}
