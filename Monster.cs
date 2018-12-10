using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Monster {

    public Game.Models.MonsterMeta m_Params;

    public PrefabManager.AssetPathInfo m_AssetInfo;

    List<AttackLogic> m_AllAttacks;

    AttackLogic[] m_NormalAbilities;
    AttackLogic[] m_AwakenedAbilities;

	List<AttackLogic> m_PassiveAbilities;
	List<AttackLogic> m_PassiveAwakenAbilities;

    public AttackLogic [] GetAbilities(bool awakened)
    {
        return awakened ? m_AwakenedAbilities : m_NormalAbilities;
    }

    public AttackLogic GetSuperAbility(bool awakened)
    {
        int ind = (int)AttackLogic.AttackType.SuperAttack;
        return awakened ? m_AwakenedAbilities[ind] : m_NormalAbilities[ind];
    }

	public List<AttackLogic> GetPassiveAbilities(bool awakened)
	{
		return awakened ? m_PassiveAwakenAbilities : m_PassiveAbilities;
	}
    
	public AttackLogic GetAutoAbility(bool awakened)
    {
		int ind = (int)AttackLogic.AttackType.AutoAttack;
        return awakened ? m_AwakenedAbilities[ind] : m_NormalAbilities[ind];
    }

    public bool HasSuperAttack()
    {
        return m_NormalAbilities[(int)AttackLogic.AttackType.SuperAttack] != null;
    }

    private void PerformAttack(MonsterOnTheField monster, BattleManager bm, AttackLogic.AttackType at, UnityEngine.Events.UnityAction doneCallback)
    {
        AttackLogic attack = monster.m_MyMonster.IsAwakened() ? m_NormalAbilities[(int)at] : m_AwakenedAbilities[(int)at];
        if (attack != null)
            attack.PlayLogic(monster, bm, doneCallback);
        else
        {
            if (doneCallback != null)
                doneCallback();
        }
    }

    public void PerformAutoAttack(MonsterOnTheField monster, BattleManager bm, UnityEngine.Events.UnityAction doneCallback = null)
    {
        PerformAttack(monster, bm, AttackLogic.AttackType.AutoAttack, doneCallback);
    }

    public void PerformSuperAttack(MonsterOnTheField monster, BattleManager bm, UnityEngine.Events.UnityAction doneCallback = null)
    {
        PerformAttack(monster, bm, AttackLogic.AttackType.SuperAttack, doneCallback);
    }

    public Monster(Game.Models.MonsterMeta m) {
        m_Params = m;

        Init();
    }

    private void Init()
    {
        m_NormalAbilities = new AttackLogic[(int)AttackLogic.AttackType.Count];
        m_AwakenedAbilities = new AttackLogic[(int)AttackLogic.AttackType.Count];

        m_AssetInfo = new PrefabManager.AssetPathInfo(m_Params.Params.AssetFolder, m_Params.Params.AssetName);
        m_AssetInfo.m_Scale = m_Params.Params.m_Scale;
        var attacks = m_Params.Params.Attacks;
        if (attacks != null)
        {
            m_AllAttacks = new List<AttackLogic>(attacks.Length);

            for (int i = 0; i < attacks.Length; i++)
            {
                AttackLogic al = MonstersManager.GetAttackLogc(attacks[i]);
                if (al == null)
                    Debug.LogError("Attack wasn't found " + attacks[i] + " for mob " + m_Params.Params.Name);
                else
                    m_AllAttacks.Add(al);
            }

            for (int i = 0; i < m_AllAttacks.Count; i++)
            {
				if (!m_AllAttacks[i].m_AwakenedAbility)
				{
					if (m_AllAttacks[i].m_AttackType == AttackLogic.AttackType.Passive)
					{
						if (m_PassiveAbilities == null)
							m_PassiveAbilities = new List<AttackLogic>();
						if (m_PassiveAwakenAbilities == null)
							m_PassiveAwakenAbilities = new List<AttackLogic>();
						m_PassiveAbilities.Add(m_AllAttacks[i]);
						m_PassiveAwakenAbilities.Add(m_AllAttacks[i]);
					} else
					    m_AwakenedAbilities[(int)m_AllAttacks[i].m_AttackType] = m_NormalAbilities[(int)m_AllAttacks[i].m_AttackType] = m_AllAttacks[i];
				}
            }

            for (int i = 0; i < m_AllAttacks.Count; i++)
            {
				if (m_AllAttacks[i].m_AwakenedAbility)
				{
					if (m_AllAttacks[i].m_AttackType == AttackLogic.AttackType.Passive)
                    {
                        if (m_PassiveAwakenAbilities == null)
                            m_PassiveAwakenAbilities = new List<AttackLogic>();
                        m_PassiveAwakenAbilities.Add(m_AllAttacks[i]);
                    }
                    else
					    m_AwakenedAbilities[(int)m_AllAttacks[i].m_AttackType] = m_AllAttacks[i];
				}
            }
        }
    }

    public void Update(Game.Models.MonsterMeta m) {
        m_Params.Update(m);

        Init();
    }

    static bool IsAwakened(GeneralQuality q)
    {
        return q == GeneralQuality.Epic;
    }

    public int GetCharacteristics(MonstersManager.Characteristic characteristics, int grade, int level, GeneralQuality quality) {
        float stat = MonstersManager.GetMonsterCharacteristics(characteristics, m_Params.Params.MobType, grade, level, m_Params.Params.m_NaturalGrade);
        stat *= m_Params.CharacteristicKoeff[(int)characteristics];
        if (IsAwakened(quality))
        {
            float k = BasicParams.GetGradesInfo().GeneralInfo.Awakening * 0.01f / 3;
            if (k > 0.001f)
                stat *= (1 + k);
        }
        return HelpMethods.Round(stat);
    }

    private void ModelWasLoadedForActiveMonster(PrefabManager.AssetPathInfo assetInfo, PrefabManager.PrefabInfo info) {
        PrefabManager.PrefabInfoWithComponentAdd<HeroAnimation> ha = info as PrefabManager.PrefabInfoWithComponentAdd<HeroAnimation>;
        ha.m_Component.Init(this);

        ActiveMonster am = info.m_Owner as ActiveMonster;
        am.ModelWasLoaded(ha);
    }

	public string GetNameForLocalization()
    {
        return m_Params.Params.AssetName;
    }

#region Attack Logic

#endregion

    public void ActiveMonsterRequestedModel(ActiveMonster am) {
        m_AssetInfo.GetInstantiatedPrefabFromAssetInfo(PrefabManager.GetPrefabWithComponentAdd<HeroAnimation>, ModelWasLoadedForActiveMonster, am);
    }
}

