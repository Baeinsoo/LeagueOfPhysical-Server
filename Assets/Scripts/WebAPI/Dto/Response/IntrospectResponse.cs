using System;

namespace LOP
{
    //  이 엔드포인트는 다른 API와 달리 code 봉투를 쓰지 않는다(RFC 7662 형식) — HttpResponse를 상속하지 않는다.
    [Serializable]
    public class IntrospectResponse
    {
        public bool active;
        public string sub;
        public long exp;
    }
}
