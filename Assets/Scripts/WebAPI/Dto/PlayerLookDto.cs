using System;
using System.Collections.Generic;

namespace LOP
{
    [Serializable]
    public class PlayerLookDto
    {
        public string displayName;
        public int level;
        public Dictionary<string, string> slots;
    }
}
