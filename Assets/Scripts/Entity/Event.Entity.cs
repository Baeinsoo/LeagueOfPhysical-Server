namespace LOP.Event.Entity
{
    public struct EntityCreated
    {
        public LOPActor entity;
        public EntityCreated(LOPActor entity)
        {
            this.entity = entity;
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
