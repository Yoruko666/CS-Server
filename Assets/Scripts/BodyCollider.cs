using UnityEngine;

public class BodyCollider : MonoBehaviour
{
    public Transform character;
    public BodyPart part;

    public void GetDamaged(int attackerUid, int damage, int weaponId)
    {
        character.GetComponent<PlayerState>().GetDamaged(attackerUid, damage, part == BodyPart.Head, weaponId);
    }
}

public enum BodyPart
{
    Head, Torso, Legs
}