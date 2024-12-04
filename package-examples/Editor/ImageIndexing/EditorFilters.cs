using System;
using UnityEngine.UIElements;

namespace UnityEditor.Search
{
    interface IEditorImageFilter : IImageFilter
    {
        void PopulateParameters(VisualElement root, Action onFilterChanged);
    }

    [AttributeUsage(AttributeTargets.Method)]
    class EditorFilterAttribute : Attribute
    {
        public string name;

        public EditorFilterAttribute(string name)
        {
            this.name = name;
        }
    }
}
