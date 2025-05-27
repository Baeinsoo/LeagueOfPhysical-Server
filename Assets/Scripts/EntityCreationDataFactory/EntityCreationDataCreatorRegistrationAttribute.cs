using System;

namespace LOP
{
    [AttributeUsage(AttributeTargets.Class)]
    public class EntityCreationDataCreatorRegistrationAttribute : Attribute
    {
        public bool value { get; }

        public EntityCreationDataCreatorRegistrationAttribute(bool value = true)
        {
            this.value = value;
        }
    }
}
