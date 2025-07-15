using System;

namespace LOP
{
    [AttributeUsage(AttributeTargets.Class)]
    public class EntityCreationDataCreatorRegistrationAttribute : Attribute
    {
        public object type { get; }
        public bool value { get; }

        public EntityCreationDataCreatorRegistrationAttribute(object type, bool value = true)
        {
            this.type = type;
            this.value = value;
        }
    }
}
