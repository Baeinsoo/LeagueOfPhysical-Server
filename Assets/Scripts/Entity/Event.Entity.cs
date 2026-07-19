namespace LOP.Event.Entity
{
    public struct EntityCreated
    {
        public string entityId;
        public EntityCreated(string entityId)
        {
            this.entityId = entityId;
        }
    }

    public struct EntityDestroyed
    {
        public string entityId;
        public EntityDestroyed(string entityId)
        {
            this.entityId = entityId;
        }
    }

    public struct ItemTouch
    {
        public string itemId;
        public string toucherId;

        public ItemTouch(string itemId, string toucherId)
        {
            this.itemId = itemId;
            this.toucherId = toucherId;
        }
    }
}
