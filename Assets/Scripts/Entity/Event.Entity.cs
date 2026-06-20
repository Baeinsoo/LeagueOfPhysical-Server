namespace LOP.Event.Entity
{
    public struct PropertyChange
    {
        public string propertyName;
        public PropertyChange(string propertyName)
        {
            this.propertyName = propertyName;
        }
    }

    public struct EntityCreated
    {
        public GameFramework.IEntity entity;
        public EntityCreated(GameFramework.IEntity entity)
        {
            this.entity = entity;
        }
    }

    public struct ActionStart
    {
        public string actionCode;
        public ActionStart(string actionCode)
        {
            this.actionCode = actionCode;
        }
    }

    public struct ActionEnd
    {
        public string actionCode;
        public ActionEnd(string actionCode)
        {
            this.actionCode = actionCode;
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
