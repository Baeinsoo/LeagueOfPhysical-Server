using GameFramework;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace LOP
{
    public struct LOPEntityCreationData : IEntityCreationData
    {
        public string entityId { get; set; }
        public Vector3 position { get; set; }
        public Vector3 rotation { get; set; }
        public Vector3 velocity { get; set; }
        public string modelId { get; set; }
    }
}
