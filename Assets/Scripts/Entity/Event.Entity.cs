namespace LOP.Event.Entity
{
    public struct EntityCreated
    {
        public LOPActor actor;
        public EntityCreated(LOPActor actor)
        {
            this.actor = actor;
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
