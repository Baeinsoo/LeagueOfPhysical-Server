
namespace LOP
{
    public static class EventTopic
    {
        public const string Entity = "Entity";

        public static string EntityId<T>(string entityId) => $"{typeof(T).Name}_{entityId}";
    }
}
