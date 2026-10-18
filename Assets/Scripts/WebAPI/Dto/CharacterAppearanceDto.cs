using System;

namespace LOP
{
    /// <summary>로비가 주는 캐릭터 외형(피부·머리 색 번호). 게임서버는 검증 없이 그대로 넘긴다 — 로비가 이미 검증했다.</summary>
    [Serializable]
    public class CharacterAppearanceDto
    {
        public int skinColorId;
        public int hairColorId;
    }
}
