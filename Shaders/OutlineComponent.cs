using System;
using UnityEngine;
using System.Collections.Generic; // Required for List<T> if OutlinePass.OutlineRenderers is a List

// If OutlinePass is in a different namespace, uncomment and add the correct one:
// using YourNamespace.OutlinePass; 

namespace Modules.Rendering.Outline
{
    [ExecuteInEditMode]
    // Removed [RequireComponent(typeof(Renderer))]
    public class OutlineComponent : MonoBehaviour
    {
        private Renderer cachedRenderer; // Cache the found renderer

        private void OnEnable()
        {
            // Find the first active Renderer component in children
            cachedRenderer = GetComponentInChildren<Renderer>(); 

            if (cachedRenderer != null)
            {
                // Assuming OutlinePass.OutlineRenderers is a static collection like List<Renderer> or HashSet<Renderer>
                if (OutlinePass.OutlineRenderers != null && !OutlinePass.OutlineRenderers.Contains(cachedRenderer))
                {
                    OutlinePass.OutlineRenderers.Add(cachedRenderer);
                }
            }
            else
            {
                Debug.LogWarning($"OutlineComponent on '{gameObject.name}' could not find a Renderer in its children.", this);
            }
        }

        private void OnDisable()
        {
            // Use the cached renderer found on enable
            if (cachedRenderer != null)
            {
                // Remove the specific renderer that was added
                if (OutlinePass.OutlineRenderers != null)
                {
                    OutlinePass.OutlineRenderers.Remove(cachedRenderer);
                }
                cachedRenderer = null; // Clear the cache
            }
            // If cachedRenderer is null, it means either it wasn't found on enable
            // or OnDisable was called without OnEnable (less likely in normal flow).
            // We don't need to search again here, just ensure we don't try to remove null.
        }
    }
}