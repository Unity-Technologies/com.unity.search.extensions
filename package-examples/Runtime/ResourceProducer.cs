using System;
using UnityEngine;

public class ResourceProducer : MonoBehaviour
{
    public ProductionRecipe[] recipes;

    [Serializable]
    public struct ProductionRecipe
    {
        public ResourceType[] requiredResources;
        public ResourceType[] producedResources;
    }
}
