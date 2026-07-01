using System;
using System.IO;
using UnityEngine;

[CreateAssetMenu(fileName = "CA_Attack", menuName = "Combat/Action")]
public class CombatActionSO : ScriptableObject
{
    public new string name;

    public int energyCost;

    public CombatTargetEnum target;
    [Tooltip("Target count <=0 or >=4 is considered party wide")]
    public int targetCount = 1; // was defaulting to 0 -> every new action defaulted to party-wide

    public float damageMult = 1;
    public bool isIgnoreDef;

    public bool isBuff;
    [Tooltip("A type of buff that removes damage instance but also decrease the buff turn by the count of damage instance")]
    public bool isGuard;
    public UnitStats buffValue;
    public int buffTurn;

    public bool appliesStun; // new — Stun is a status effect (GDD 5/8), needs a way to be authored on an action
}

[Serializable]
public enum CombatTargetEnum { Allies, Foes }