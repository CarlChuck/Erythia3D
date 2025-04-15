using NUnit.Framework;
using System.Collections.Generic;
using UnityEngine;
using System;

public class StatBlock : MonoBehaviour
{
    private string characterName;
    public SpeciesTemplate species;
    private int gender;
    [SerializeField] private Stat strength;
    [SerializeField] private Stat dexterity;
    [SerializeField] private Stat constitution;
    [SerializeField] private Stat intelligence;
    [SerializeField] private Stat spirit;

    [SerializeField] private Stat offence;
    [SerializeField] private Stat defence;
    [SerializeField] private Stat damBonus;
    [SerializeField] private Stat critChance;
    [SerializeField] private Stat critDamage;
    [SerializeField] private Stat dodge;
    [SerializeField] private Stat parry;
    [SerializeField] private Stat spellDamage;
    [SerializeField] private Stat buffDuration;
    [SerializeField] private Stat dotDuration;
    [SerializeField] private Stat healingPower;
    [SerializeField] private Stat healthRegen;
    [SerializeField] private Stat manaRegen;
    [SerializeField] private Stat damageShield;
    [SerializeField] private Stat attackSpeed;

    [SerializeField] private Stat defenceSkill;
    [SerializeField] private Stat offenceSkill;
    [SerializeField] private Stat weaponSkill;
    [SerializeField] private Stat parrySkill;
    [SerializeField] private Stat dodgeSkill;
    [SerializeField] private Stat critSkill;
    private int baseHealth;
    private int baseMana;
    private float currentBloodPool;
    private float currentMana;
    private int resistCorruption;
    private int resistMind;

    [SerializeField] private HitLocationProfile hitLocations;
    [SerializeField] private GameObject statPrefab;

    [SerializeField] private List<Stat> statList = new List<Stat>();
    public event Action OnVitalsChanged;
    public event Action OnStatsChanged;

    public void SetupStatBlock()
    {
        strength = Instantiate(statPrefab).GetComponent<Stat>();
        strength.transform.SetParent(transform);
        strength.gameObject.name = "Strength";
        statList.Add(strength);
        strength.InitialiseStat(species.strength, 1000, StatType.Strength);

        dexterity = Instantiate(statPrefab).GetComponent<Stat>();
        dexterity.transform.SetParent(transform);
        dexterity.gameObject.name = "Dexterity";
        statList.Add(dexterity);
        dexterity.InitialiseStat(species.dexterity, 1000, StatType.Dexterity);

        constitution = Instantiate(statPrefab).GetComponent<Stat>();
        constitution.transform.SetParent(transform);
        constitution.gameObject.name = "Constitution";
        statList.Add(constitution); 
        constitution.InitialiseStat(species.constitution, 1000, StatType.Constitution);

        intelligence = Instantiate(statPrefab).GetComponent<Stat>();
        intelligence.transform.SetParent(transform);
        intelligence.gameObject.name = "Intelligence";
        statList.Add(intelligence);
        intelligence.InitialiseStat(species.intelligence, 1000, StatType.Intelligence);

        spirit = Instantiate(statPrefab).GetComponent<Stat>();
        spirit.transform.SetParent(transform);
        spirit.gameObject.name = "Spirit";
        statList.Add(spirit);
        spirit.InitialiseStat(species.spirit, 1000, StatType.Spirit);

        offence = Instantiate(statPrefab).GetComponent<Stat>();
        offence.transform.SetParent(transform);
        offence.gameObject.name = "Offence";
        statList.Add(offence);

        defence = Instantiate(statPrefab).GetComponent<Stat>();
        defence.transform.SetParent(transform);
        defence.gameObject.name = "Defence";
        statList.Add(defence);

        damBonus = Instantiate(statPrefab).GetComponent<Stat>();
        damBonus.transform.SetParent(transform);
        damBonus.gameObject.name = "DamBonus";
        statList.Add(damBonus);

        critChance = Instantiate(statPrefab).GetComponent<Stat>();
        critChance.transform.SetParent(transform);
        critChance.gameObject.name = "CritChance";
        statList.Add(critChance);

        critDamage = Instantiate(statPrefab).GetComponent<Stat>();
        critDamage.transform.SetParent(transform);
        critDamage.gameObject.name = "CritDamage";
        statList.Add(critDamage);

        dodge = Instantiate(statPrefab).GetComponent<Stat>();
        dodge.transform.SetParent(transform);
        dodge.gameObject.name = "Dodge";
        statList.Add(dodge);

        parry = Instantiate(statPrefab).GetComponent<Stat>();
        parry.transform.SetParent(transform);
        parry.gameObject.name = "Parry";
        statList.Add(parry);

        spellDamage = Instantiate(statPrefab).GetComponent<Stat>();
        spellDamage.transform.SetParent(transform);
        spellDamage.gameObject.name = "SpellDamage";
        statList.Add(spellDamage);

        buffDuration = Instantiate(statPrefab).GetComponent<Stat>();
        buffDuration.transform.SetParent(transform);
        buffDuration.gameObject.name = "BuffDuration";
        statList.Add(buffDuration);

        dotDuration = Instantiate(statPrefab).GetComponent<Stat>();
        dotDuration.transform.SetParent(transform);
        dotDuration.gameObject.name = "DotDuration";
        statList.Add(dotDuration);

        healingPower = Instantiate(statPrefab).GetComponent<Stat>();
        healingPower.transform.SetParent(transform);
        healingPower.gameObject.name = "HealingPower";
        statList.Add(healingPower);

        healthRegen = Instantiate(statPrefab).GetComponent<Stat>();
        healthRegen.transform.SetParent(transform);
        healthRegen.gameObject.name = "HealthRegen";
        statList.Add(healthRegen);

        manaRegen = Instantiate(statPrefab).GetComponent<Stat>();
        manaRegen.transform.SetParent(transform);
        manaRegen.gameObject.name = "ManaRegen";
        statList.Add(manaRegen);

        damageShield = Instantiate(statPrefab).GetComponent<Stat>();
        damageShield.transform.SetParent(transform);
        damageShield.gameObject.name = "DamageShield";
        statList.Add(damageShield);

        attackSpeed = Instantiate(statPrefab).GetComponent<Stat>();
        attackSpeed.transform.SetParent(transform);
        attackSpeed.gameObject.name = "AttackSpeed";
        statList.Add(attackSpeed);

        defenceSkill = Instantiate(statPrefab).GetComponent<Stat>();
        defenceSkill.transform.SetParent(transform);
        defenceSkill.gameObject.name = "DefenceSkill";
        statList.Add(defenceSkill);
        defenceSkill.InitialiseStat(0, 1000, StatType.DefenceSkill);

        offenceSkill = Instantiate(statPrefab).GetComponent<Stat>();
        offenceSkill.transform.SetParent(transform);
        offenceSkill.gameObject.name = "OffenceSkill";
        statList.Add(offenceSkill);
        offenceSkill.InitialiseStat(0, 1000, StatType.OffenceSkill);

        weaponSkill = Instantiate(statPrefab).GetComponent<Stat>();
        weaponSkill.transform.SetParent(transform);
        weaponSkill.gameObject.name = "WeaponSkill";
        statList.Add(weaponSkill);
        weaponSkill.InitialiseStat(0, 1000, StatType.WeaponSkill);

        parrySkill = Instantiate(statPrefab).GetComponent<Stat>();
        parrySkill.transform.SetParent(transform);
        parrySkill.gameObject.name = "ParrySkill";
        statList.Add(parrySkill);
        parrySkill.InitialiseStat(0, 1000, StatType.ParrySkill);

        dodgeSkill = Instantiate(statPrefab).GetComponent<Stat>();
        dodgeSkill.transform.SetParent(transform);
        dodgeSkill.gameObject.name = "DodgeSkill";
        statList.Add(dodgeSkill);
        dodgeSkill.InitialiseStat(0, 1000, StatType.DodgeSkill);

        critSkill = Instantiate(statPrefab).GetComponent<Stat>();
        critSkill.transform.SetParent(transform);
        critSkill.gameObject.name = "CritSkill";
        statList.Add(critSkill);
        critSkill.InitialiseStat(0, 1000, StatType.CritSkill);

        //Extra step for readability in the formula's
        int str = strength.GetStatValue() - 30;
        int dex = dexterity.GetStatValue() - 30;
        int con = constitution.GetStatValue() - 30;
        int inte = intelligence.GetStatValue() - 30;
        int spi = spirit.GetStatValue() - 30;

        offence.InitialiseStat((str / 4) + (dex / 4) + (inte / 2) + offenceSkill.GetStatValue() + weaponSkill.GetStatValue(), 1000, StatType.Offence);
        defence.InitialiseStat((dex / 2) + (inte / 2) + defenceSkill.GetStatValue() + weaponSkill.GetStatValue(), 1000, StatType.Defence);
        damBonus.InitialiseStat((str / 5), 500, StatType.DamBonus);
        critChance.InitialiseStat(5 + (dex / 4) + critSkill.GetStatValue(), 1000, StatType.CritChance);
        critDamage.InitialiseStat(damBonus.GetStatValue() * 2, 1000, StatType.CritDamage);
        dodge.InitialiseStat((dex / 5) + dodgeSkill.GetStatValue(), 1000, StatType.Dodge);
        parry.InitialiseStat((dex / 10) + (inte / 10) + parrySkill.GetStatValue(), 1000, StatType.Parry);
        spellDamage.InitialiseStat(intelligence.GetStatValue() / 4, 1000, StatType.SpellDamage);
        buffDuration.InitialiseStat(intelligence.GetStatValue() / 4, 100, StatType.BuffDuration);
        dotDuration.InitialiseStat(intelligence.GetStatValue() / 4, 100, StatType.DotDuration);
        healingPower.InitialiseStat(intelligence.GetStatValue() / 4, 1000, StatType.HealingPower);
        healthRegen.InitialiseStat(constitution.GetStatValue() / 20, 10, StatType.HealthRegen);
        manaRegen.InitialiseStat(spirit.GetStatValue() / 20, 10, StatType.ManaRegen);
        damageShield.InitialiseStat(0, 20, StatType.DamageShield);
        attackSpeed.InitialiseStat(0, 100, StatType.AttackSpeed);
        resistCorruption = con;
        resistMind = spi;

        baseHealth = constitution.GetStatValue() * 2;
        baseMana = spirit.GetStatValue() * 2;
        currentBloodPool = baseHealth;
        currentMana = baseMana;
        OnVitalsChanged?.Invoke();
        OnStatsChanged?.Invoke();
    }
    public void SetSpecies(SpeciesTemplate newSpecies)
    {
        species = newSpecies;
    }
    public void SetGender(int newGender)
    {
        gender = newGender;
    }
    public void UpdateDerivedStats()
    {
        //Extra step for readability in the formula's
        int str = strength.GetStatValue() - 30;
        int dex = dexterity.GetStatValue() - 30;
        int con = constitution.GetStatValue() - 30;
        int inte = intelligence.GetStatValue() - 30;
        int spi = spirit.GetStatValue() - 30;

        offence.SetBaseValue((str / 4) + (dex / 4) + (inte / 2) + offenceSkill.GetStatValue() + weaponSkill.GetStatValue());
        defence.SetBaseValue((dex / 2) + (inte / 2) + defenceSkill.GetStatValue() + weaponSkill.GetStatValue());
        damBonus.SetBaseValue((str / 5));
        critChance.SetBaseValue(5 + (dex / 4) + critSkill.GetStatValue());
        critDamage.SetBaseValue(damBonus.GetStatValue() * 2);
        dodge.SetBaseValue((dex / 5) + dodgeSkill.GetStatValue());
        parry.SetBaseValue((dex / 10) + (inte / 10) + parrySkill.GetStatValue());
        spellDamage.SetBaseValue(intelligence.GetStatValue() / 4);
        buffDuration.SetBaseValue(intelligence.GetStatValue() / 4);
        dotDuration.SetBaseValue(intelligence.GetStatValue() / 4);
        healingPower.SetBaseValue(intelligence.GetStatValue() / 4);
        healthRegen.SetBaseValue(constitution.GetStatValue() / 20);
        manaRegen.SetBaseValue(spirit.GetStatValue() / 20);
        damageShield.SetBaseValue(0);
        attackSpeed.SetBaseValue(0);
        resistCorruption = con;
        resistMind = spi;
        //TODO add stats from gear
        OnVitalsChanged?.Invoke();
        OnStatsChanged?.Invoke();
    }
    public void InitialiseHitLocations()
    {
        hitLocations.Initialise(baseHealth);
    }
    public void SetCharacterName(string newName)
    {
        characterName = newName;
    }
    public void SetCharacterGender(int newGender)
    {
        gender = newGender;
    }


    public float GetCurrentHealth()
    {
        return currentBloodPool;
    }

    public float GetMaxHealth()
    {
        return baseHealth;
    }

    public float GetCurrentMana()
    {
        return currentMana;
    }

    public float GetMaxMana()
    {
        return baseMana;
    }

    public string GetCharacterName()
    {
        return characterName;
    }

    public List<Stat> GetAllStats()
    {
        // Return the list of Stat components you populate in SetupStatBlock
        // Make sure statList is correctly populated and accessible.
        // If statList is private, make this method public.
        // If statList doesn't contain *all* stats you want to display,
        // you might need to manually build and return a list here.
        if (statList == null)
        {
            Debug.LogError("StatBlock: statList is null!");
            return new List<Stat>(); // Return empty list to prevent errors
        }
        return statList;
    }

}
public enum StatType
{
    Strength,
    Dexterity,
    Constitution,
    Intelligence,
    Spirit,

    Offence,
    Defence,
    DamBonus,
    CritChance,
    CritDamage,
    Dodge,
    Parry,
    SpellDamage,
    BuffDuration,
    DotDuration,
    HealingPower,
    HealthRegen,
    ManaRegen,
    DamageShield,
    AttackSpeed,

    DefenceSkill,
    OffenceSkill,
    WeaponSkill,
    ParrySkill,
    DodgeSkill,
    CritSkill
}