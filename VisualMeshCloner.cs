using UnityEngine;

namespace ProxyNote
{
    internal static class VisualMeshCloner
    {
        internal static GameObject CloneRenderHierarchy(Transform sourceRoot, string name)
        {
            if (sourceRoot == null)
            {
                return null;
            }

            GameObject root = new GameObject(name);
            root.layer = sourceRoot.gameObject.layer;
            CopyTransformChildrenAndRenderers(sourceRoot, root.transform, copyTransform: false);

            if (root.GetComponentsInChildren<Renderer>(includeInactive: true).Length == 0)
            {
                Object.Destroy(root);
                return null;
            }

            return root;
        }

        private static void CopyTransformChildrenAndRenderers(
            Transform source,
            Transform destination,
            bool copyTransform)
        {
            if (copyTransform)
            {
                destination.localPosition = source.localPosition;
                destination.localRotation = source.localRotation;
                destination.localScale = source.localScale;
            }

            destination.gameObject.layer = source.gameObject.layer;
            CopyMeshRenderer(source.gameObject, destination.gameObject);
            CopySkinnedMeshRenderer(source.gameObject, destination.gameObject);

            for (int index = 0; index < source.childCount; index++)
            {
                Transform sourceChild = source.GetChild(index);
                if (sourceChild.name == "NoteCutGuide")
                {
                    continue;
                }

                GameObject destinationChild = new GameObject(sourceChild.name);
                destinationChild.SetActive(sourceChild.gameObject.activeSelf);
                destinationChild.transform.SetParent(destination, false);
                CopyTransformChildrenAndRenderers(
                    sourceChild,
                    destinationChild.transform,
                    copyTransform: true);
            }
        }

        private static void CopyMeshRenderer(GameObject source, GameObject destination)
        {
            MeshRenderer sourceRenderer = source.GetComponent<MeshRenderer>();
            MeshFilter sourceFilter = source.GetComponent<MeshFilter>();
            if (sourceRenderer == null || sourceFilter == null)
            {
                return;
            }

            MeshFilter destinationFilter = destination.AddComponent<MeshFilter>();
            destinationFilter.sharedMesh = sourceFilter.sharedMesh;

            MeshRenderer destinationRenderer = destination.AddComponent<MeshRenderer>();
            CopyRendererState(sourceRenderer, destinationRenderer);
        }

        private static void CopySkinnedMeshRenderer(GameObject source, GameObject destination)
        {
            SkinnedMeshRenderer sourceRenderer = source.GetComponent<SkinnedMeshRenderer>();
            if (sourceRenderer == null)
            {
                return;
            }

            SkinnedMeshRenderer destinationRenderer = destination.AddComponent<SkinnedMeshRenderer>();
            destinationRenderer.sharedMesh = sourceRenderer.sharedMesh;
            destinationRenderer.localBounds = sourceRenderer.localBounds;
            CopyRendererState(sourceRenderer, destinationRenderer);
        }

        private static void CopyRendererState(Renderer source, Renderer destination)
        {
            destination.sharedMaterials = source.sharedMaterials;
            destination.enabled = source.enabled;
            destination.shadowCastingMode = source.shadowCastingMode;
            destination.receiveShadows = source.receiveShadows;
            destination.lightProbeUsage = source.lightProbeUsage;
            destination.reflectionProbeUsage = source.reflectionProbeUsage;
            destination.probeAnchor = source.probeAnchor;
            destination.sortingLayerID = source.sortingLayerID;
            destination.sortingOrder = source.sortingOrder;

            MaterialPropertyBlock propertyBlock = new MaterialPropertyBlock();
            source.GetPropertyBlock(propertyBlock);
            destination.SetPropertyBlock(propertyBlock);
        }
    }
}
