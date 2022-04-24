using MelonLoader;
using System;
using UnityEngine;

namespace FreezeFrame
{

    [RegisterTypeInIl2Cpp]
    public class VRC_FreezeData : MonoBehaviour 
    {
        public VRC_FreezeData(IntPtr ptr) : base(ptr) { }
        public bool IsMain = false;
    }
}
