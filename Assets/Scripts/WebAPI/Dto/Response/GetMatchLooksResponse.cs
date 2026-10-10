using System.Collections.Generic;

namespace LOP
{
    public class GetMatchLooksResponse : HttpResponse
    {
        public Dictionary<string, PlayerLookDto> looks;
    }
}
