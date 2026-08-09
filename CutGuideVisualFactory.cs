using UnityEngine;

namespace ProxyNote
{
    internal static class CutGuideVisualFactory
    {
        internal const string GuideName = "ProxyNoteCutGuide";

        internal static GameObject Create(Transform proxyRoot)
        {
            Transform guideParent = FindNoteCube(proxyRoot) ?? proxyRoot;
            MeshRenderer sourceRenderer =
                guideParent.GetComponentInChildren<MeshRenderer>(includeInactive: true);
            if (sourceRenderer == null && guideParent != proxyRoot)
            {
                guideParent = proxyRoot;
                sourceRenderer =
                    proxyRoot.GetComponentInChildren<MeshRenderer>(includeInactive: true);
            }

            if (sourceRenderer == null || sourceRenderer.sharedMaterial == null)
            {
                return null;
            }

            GameObject guide = GameObject.CreatePrimitive(PrimitiveType.Cube);
            guide.name = GuideName;
            guide.SetActive(false);

            Collider collider = guide.GetComponent<Collider>();
            if (collider != null)
            {
                collider.enabled = false;
                Object.Destroy(collider);
            }

            MeshRenderer renderer = guide.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = sourceRenderer.sharedMaterial;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = sourceRenderer.lightProbeUsage;
            renderer.reflectionProbeUsage = sourceRenderer.reflectionProbeUsage;

            MaterialPropertyBlock properties = new MaterialPropertyBlock();
            sourceRenderer.GetPropertyBlock(properties);
            renderer.SetPropertyBlock(properties);

            guide.transform.SetParent(guideParent, false);
            guide.transform.localRotation = Quaternion.identity;
            return guide;
        }

        private static Transform FindNoteCube(Transform proxyRoot)
        {
            Transform[] transforms =
                proxyRoot.GetComponentsInChildren<Transform>(includeInactive: true);
            foreach (Transform child in transforms)
            {
                if (child.name == "NoteCube")
                {
                    return child;
                }
            }

            return null;
        }
    }
}
