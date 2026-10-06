using System.Collections.Generic;
using Hearthglade.Core.World;
using Hearthglade.Gameplay.Environment.Block.Base;
using UnityEngine;

namespace Hearthglade.Gameplay.Map
{
    /// <summary>The look of one resource that can grow on the map, taken from its prefab.</summary>
    public struct SceneObjectTemplate
    {
        public string Name;
        public float Y;
        public Vector3 Euler;
        public Vector3 Scale;
    }

    /// <summary>
    /// The resources of one biome, read once from its asset: the rules the Core roller works on and, at the same
    /// index, the template (prefab name, height, scale) of each resource.
    /// </summary>
    public sealed class ResourceTable
    {
        public static readonly ResourceTable Empty = new ResourceTable(new ResourceRule[0], new SceneObjectTemplate[0]);

        public readonly ResourceRule[] Rules;
        public readonly SceneObjectTemplate[] Templates;

        private ResourceTable(ResourceRule[] rules, SceneObjectTemplate[] templates)
        {
            Rules = rules;
            Templates = templates;
        }

        public static ResourceTable From(IReadOnlyList<SpawnableResourceData> resources)
        {
            if (resources == null || resources.Count == 0)
            {
                return Empty;
            }

            var rules = new List<ResourceRule>(resources.Count);
            var templates = new List<SceneObjectTemplate>(resources.Count);
            foreach (var resource in resources)
            {
                if (resource == null || resource.resourcePrefab == null)
                {
                    continue;
                }
                var t = resource.resourcePrefab.transform;
                rules.Add(resource.ToRule());
                templates.Add(new SceneObjectTemplate
                {
                    Name = t.name,
                    Y = t.position.y,
                    Euler = t.eulerAngles,
                    Scale = t.localScale,
                });
            }
            return new ResourceTable(rules.ToArray(), templates.ToArray());
        }
    }
}
