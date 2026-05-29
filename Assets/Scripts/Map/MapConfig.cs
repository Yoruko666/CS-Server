using UnityEngine;

[CreateAssetMenu(fileName = "Map Config", menuName = "SO/Map/MapConfig")]

public class MapConfig : ScriptableObject
{
    public Vector3[] bornPoints = new Vector3[6];
}