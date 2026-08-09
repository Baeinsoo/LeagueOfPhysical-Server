using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace LOP
{
    [Serializable]
    public class CustomProperties
    {
        public string accessToken;

        //  와이어 형태 유지용으로 남겨둔 필드 — 서버는 현재 이 값을 읽지 않는다(신원은 accessToken의
        //  sub만 사용). 클라와 필드 단위로 동일해야 하는 Mirror Weaver 제약 때문에 지우지 않는다.
        public int characterId;
    }
}
