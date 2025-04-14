using AutoMapper;

namespace LOP
{
    public class RoomMapperProfile : Profile
    {
        public RoomMapperProfile()
        {
            CreateMap<Room, RoomDto>();
            CreateMap<RoomDto, Room>();
        }
    }
}
