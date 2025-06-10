using Unity.Netcode;
using System.Collections.Generic;
using UnityEngine;
using System;

public class StatBlock : NetworkBehaviour
{
    private string characterName;
    public SpeciesTemplate species;
    private int speciesNum;
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

    private Stat CreateAndRegisterStat(string name, StatType statType, int baseValue = 0, int maxValue = 1000)
    {
        Stat newStat = Instantiate(statPrefab).GetComponent<Stat>();
        newStat.transform.SetParent(transform);
        newStat.gameObject.name = name;
        newStat.InitialiseStat(baseValue, maxValue, statType);
        statList.Add(newStat);
        return newStat;
    }

    public void SetupStatBlock()
    {
        // Primary Stats
        strength = CreateAndRegisterStat("Strength", StatType.Strength, species.strength);
        dexterity = CreateAndRegisterStat("Dexterity", StatType.Dexterity, species.dexterity);
        constitution = CreateAndRegisterStat("Constitution", StatType.Constitution, species.constitution);
        intelligence = CreateAndRegisterStat("Intelligence", StatType.Intelligence, species.intelligence);
        spirit = CreateAndRegisterStat("Spirit", StatType.Spirit, species.spirit);

        // Derived Combat Stats
        offence = CreateAndRegisterStat("Offence", StatType.Offence);
        defence = CreateAndRegisterStat("Defence", StatType.Defence);
        damBonus = CreateAndRegisterStat("DamBonus", StatType.DamBonus, 0, 500);
        critChance = CreateAndRegisterStat("CritChance", StatType.CritChance);
        critDamage = CreateAndRegisterStat("CritDamage", StatType.CritDamage);
        dodge = CreateAndRegisterStat("Dodge", StatType.Dodge);
        parry = CreateAndRegisterStat("Parry", StatType.Parry);
        spellDamage = CreateAndRegisterStat("SpellDamage", StatType.SpellDamage);
        buffDuration = CreateAndRegisterStat("BuffDuration", StatType.BuffDuration, 0, 100);
        dotDuration = CreateAndRegisterStat("DotDuration", StatType.DotDuration, 0, 100);
        healingPower = CreateAndRegisterStat("HealingPower", StatType.HealingPower);
        healthRegen = CreateAndRegisterStat("HealthRegen", StatType.HealthRegen, 0, 10);
        manaRegen = CreateAndRegisterStat("ManaRegen", StatType.ManaRegen, 0, 10);
        damageShield = CreateAndRegisterStat("DamageShield", StatType.DamageShield, 0, 20);
        attackSpeed = CreateAndRegisterStat("AttackSpeed", StatType.AttackSpeed, 0, 100);

        // Skill Stats
        defenceSkill = CreateAndRegisterStat("DefenceSkill", StatType.DefenceSkill);
        offenceSkill = CreateAndRegisterStat("OffenceSkill", StatType.OffenceSkill);
        weaponSkill = CreateAndRegisterStat("WeaponSkill", StatType.WeaponSkill);
        parrySkill = CreateAndRegisterStat("ParrySkill", StatType.ParrySkill);
        dodgeSkill = CreateAndRegisterStat("DodgeSkill", StatType.DodgeSkill);
        critSkill = CreateAndRegisterStat("CritSkill", StatType.CritSkill);

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
    public void SetSpeciesNum(int newSpeciesNum)
    {
        speciesNum = newSpeciesNum;
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

    public int GetSpecies()
    {
        return speciesNum;
    }
    public int GetGender()
    {
        return gender;
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