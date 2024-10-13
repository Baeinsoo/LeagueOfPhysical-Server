using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using GameFramework;

namespace LOP
{
    public class LOPGameEngine : GameEngineBase
    {
        public override async Task InitializeAsync()
        {
            await base.InitializeAsync();
        }

        public override async Task DeinitializeAsync()
        {
            await base.DeinitializeAsync();
        }
    }
}
