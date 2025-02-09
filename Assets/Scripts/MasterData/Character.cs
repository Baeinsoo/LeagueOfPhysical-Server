using GameFramework;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace LOP.MasterData
{
    public class Character : IMasterData
    {
        public string code { get; private set; }
        public string name { get; private set; }
        public float speed { get; private set; }
        public string resource_code { get; private set; }
    }
}
