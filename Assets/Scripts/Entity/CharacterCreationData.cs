using GameFramework;
using UnityEngine;

namespace LOP
{
    public struct CharacterCreationData : IEntityCreationData
    {
        public string userId { get; set; }
        public string entityId { get; set; }
        public Vector3 position { get; set; }
        public Vector3 rotation { get; set; }
        public Vector3 velocity { get; set; }
        public string characterCode { get; set; }
        public string visualId { get; set; }

        public int maxHP { get; set; }
        public int currentHP { get; set; }
        public int maxMP { get; set; }
        public int currentMP { get; set; }
        public int level { get; set; }
        public long currentExp { get; set; }

        public int strength { get; set; }
        public int dexterity { get; set; }
        public int intelligence { get; set; }
        public int vitality { get; set; }

        /// <summary>식으로 움직이는 캐릭터(<see cref="ScriptedMotion"/>) — 월드 시뮬·AI 두뇌 없이 모드 시스템이 자리를 쓴다.</summary>
        public bool scriptedMotion { get; set; }
    }
}
