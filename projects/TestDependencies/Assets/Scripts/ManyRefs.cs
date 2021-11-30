using UnityEngine;

[CreateAssetMenu]
class ManyRefs : ScriptableObject
{
    public string path;
    public Object any;
    public GameObject gameObject;
    public UnityEditor.DefaultAsset folder;
    public Material material;
}
