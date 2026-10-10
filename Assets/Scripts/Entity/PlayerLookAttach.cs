namespace LOP
{
    /// <summary>
    /// 생성 데이터의 룩을 월드 엔티티에 붙인다. 룩이 없으면(몬스터·심판 등 사람이 아닌 몸) 아무 일도
    /// 하지 않는다 — 그런 몸에 "플레이어 N" 이름표가 달리면 안 되기 때문이다.
    /// </summary>
    public static class PlayerLookAttach
    {
        public static bool Attach(GameFramework.World.Entity entity, PlayerLook look)
        {
            if (look == null)
            {
                return false;
            }

            entity.Add(look);
            return true;
        }
    }
}
