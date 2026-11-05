using System;
using UnityEngine;

namespace GrandpaRobot.Data
{
    [Serializable] public sealed class Point { public float x, y, z; public Vector3 Vector => new Vector3(x,y,z); }
    [Serializable] public sealed class ObjectDefinition
    {
        public string id, name, kind, prefabId, materialId, soundId;
        public bool movable = true;
        public bool blocksMovement = true, preservePrefabMaterials, pushOnly;
        public Point size;
        public Point storageEuler;
    }
    [Serializable] public sealed class ObjectLibrary { public ObjectDefinition[] objects; }
    [Serializable] public sealed class InstanceDefinition { public string id, definitionId, initialNodeId; public Point position, scale, visualEuler; public int rotation; }
    [Serializable] public sealed class NodeDefinition
    {
        public string id, name;
        public Point position;
        public int capacity = 1;
        public float layerHeight = .6f;
        public string[] requiresNodes;
        public bool allowStructuralOverlap;
        public float interactionRadius=1.25f;
        public string role;
        public float displayScale=1;
    }
    [Serializable] public sealed class GoalDefinition
    {
        public string nodeId, kind;
        public int level, rotation = -1;
        public string[] prerequisites;
        public bool adjusted;
    }
    [Serializable] public sealed class PhaseDefinition { public string name,hint; public string[] nodes; }
    [Serializable] public sealed class RepairDefinition
    {
        public string[] debrisIds, temporaryNodes;
        public GoalDefinition[] arrangementGoals;
    }
    [Serializable] public sealed class StageDefinition
    {
        public string id, name, description, backgroundMaterialId, soundId;
        public float width = 16, depth = 12;
        public Point grandfatherSpawn, robotSpawn;
        public InstanceDefinition[] objects;
        public NodeDefinition[] nodes;
        public GoalDefinition[] goals;
        public PhaseDefinition[] phases;
        public RepairDefinition repair;
    }
    [Serializable] public sealed class PrefabEntry { public string id; public GameObject asset; }
    [Serializable] public sealed class MaterialEntry { public string id; public Material asset; }
    [Serializable] public sealed class AudioEntry { public string id; public AudioClip asset; }
    [CreateAssetMenu(menuName="Grandpa Robot/Asset Catalog")]
    public sealed class AssetCatalog : ScriptableObject
    {
        public TextAsset objectDefinitions;
        public TextAsset[] stages;
        public PrefabEntry[] prefabs;
        public MaterialEntry[] materials;
        public AudioEntry[] sounds;
        public Font uiFont;
        public GameObject Prefab(string id) => Array.Find(prefabs, x=>x.id==id)?.asset;
        public Material Material(string id) => Array.Find(materials, x=>x.id==id)?.asset;
        public AudioClip Sound(string id) => Array.Find(sounds, x=>x.id==id)?.asset;
        public ObjectLibrary Library() => JsonUtility.FromJson<ObjectLibrary>(objectDefinitions.text);
        public StageDefinition Stage(int index) => JsonUtility.FromJson<StageDefinition>(stages[index].text);
    }
}
