namespace LOP
{
    /// <summary>
    /// 이 사람의 결과가 이미 정해졌나(완주·탈락). 정해진 사람은 판 도중 나가도 꼴찌로 내리지 않는다 —
    /// 1등으로 들어와 관전 화면에서 나간 사람이 꼴찌가 되면 안 된다. 결과가 끝까지 안 정해지는 게임(점수제)은 구현하지 않는다.
    /// </summary>
    public interface ISettledResults
    {
        bool IsResultSettled(string userId);
    }
}
