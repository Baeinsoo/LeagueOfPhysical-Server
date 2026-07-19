using UnityEngine;

namespace LOP
{
    public class LOPActor : MonoBehaviour
    {
        public string entityId { get; private set; }

        public void SetEntityId(string entityId)
        {
            this.entityId = entityId;
        }
    }
}
