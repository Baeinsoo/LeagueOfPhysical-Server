namespace LOP
{
    /// <summary>
    /// 게임별 서버 룰 — 누구를 어디에 스폰하고, 무엇으로 점수를 매기고, 언제 끝내는지.
    /// 언리얼의 GameMode에 해당한다. 호스트(Runner)가 초기화·해제만 구동하고 내용은 모른다.
    /// </summary>
    public interface IGameRuleSystem
    {
        void Initialize();
        void Deinitialize();
    }
}
