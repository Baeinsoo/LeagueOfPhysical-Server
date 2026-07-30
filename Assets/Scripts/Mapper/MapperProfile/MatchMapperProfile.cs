using AutoMapper;
using UnityEngine;

namespace LOP
{
    public class MatchMapperProfile : Profile
    {
        public MatchMapperProfile()
        {
            CreateMap<Match, MatchDto>();
            CreateMap<MatchDto, Match>();

            // rounds는 커스텀 타입 배열이라 AutoMapper가 자동으로 매핑을 만들어주지 않음 — 명시 등록 필요
            CreateMap<MatchRound, MatchRoundDto>();
            CreateMap<MatchRoundDto, MatchRound>();
        }
    }
}
