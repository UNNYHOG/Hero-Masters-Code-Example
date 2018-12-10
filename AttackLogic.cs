using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using Game.Utils.Json.Converters;
using Game.Utils.Strings;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using UnityEngine;
using Random = UnityEngine.Random;

[System.Serializable]
public class AttackLogic
{
	public string m_Name;
	#region classes
	public enum Destination
	{
		OneMonster,
		MyTeam,
		TargetTeam,
		Self,
		OneMonsterAndSelf,
		SelfModel,
		ChildTransform,
	}

	[System.Serializable]
	public abstract class ActionBase
	{
        [JsonIgnore]
		public AttackLogic m_AttackLogic;
		protected static List<ActionBase> EMPTY_LIST = new List<ActionBase>();

		[JsonProperty("delay", DefaultValueHandling = DefaultValueHandling.Populate)]
		[DefaultValue(0)]
		public float m_Delay;

		[JsonProperty("actionClass", DefaultValueHandling = DefaultValueHandling.Populate)]
		[DefaultValue("ActionBase")]
		public string m_ActionClass;

		[JsonProperty("cancelDelay", DefaultValueHandling = DefaultValueHandling.Populate)]
		[DefaultValue(0)]
		public float m_CancelDelay;//action is cancelled only if you die before the time. m_CancelDelay<=m_Delay

		[JsonProperty("probability", DefaultValueHandling = DefaultValueHandling.Populate)]
		[DefaultValue(100)]
		public int m_Probability = 100;//For effects to be added with 20% chance for example.

		[JsonProperty("repeat", DefaultValueHandling = DefaultValueHandling.Populate)]
		[DefaultValue(0)]
		public int m_Repeat;//0-just once

		[JsonProperty("period", DefaultValueHandling = DefaultValueHandling.Populate)]
		[DefaultValue(0)]
		public float m_RepeatPeriod;

		[JsonProperty("reverse")]
		public bool m_Reverse;

		[JsonProperty("destination", DefaultValueHandling = DefaultValueHandling.Populate)]
		[JsonConverter(typeof(StringEnumConverter))]
		[DefaultValue(Destination.OneMonster)]
		protected Destination m_Destination;

		//        public ActionBase()
		//        {
		//            m_Delay = m_CancelDelay = 0;
		//        }

		public virtual float PerformReverseAction(BattleManager bm, MonsterOnTheField monster, List<MonsterOnTheField> targets)
		{
			return 0f;
		}

		public virtual float PerformAction(BattleManager bm, AttackLogic attackLogic, MonsterOnTheField monster, List<MonsterOnTheField> targets)
		{
			return 0f;
		}

		public virtual void Init()
		{
		}

		static List<MonsterOnTheField> m_ListOfTargets = new List<MonsterOnTheField>(1);

		public void PerformAction(BattleManager bm, AttackLogic attackLogic, MonsterOnTheField monster, MonsterOnTheField target)
		{
			m_ListOfTargets.Add(target);
			PerformAction(bm, attackLogic, monster, m_ListOfTargets);
			m_ListOfTargets.Clear();
		}

		public virtual bool CanApplyEffectEnemy(MonsterOnTheField monster, MonsterOnTheField target, int flag)
		{
			return (BasicParams.CanApplyEffects(flag) && BasicParams.CanApplyEffect(monster, target));
		}

		public virtual bool CanApplyEffectFriend(MonsterOnTheField monster, MonsterOnTheField target, int flag)
		{
			return true;
		}

		public Destination GetEffectDestination()
		{
			return m_Destination;
		}

		public virtual void Print()
		{
			Debug.Log("m_Delay: " + m_Delay);
			Debug.Log("m_CancelDelay: " + m_CancelDelay);
			Debug.Log("m_Probability: " + m_Probability);
		}

		public virtual string LocalizeDescription(AttackLogic al, MyMonster monster)
		{
			return null;
		}
	}

	[System.Serializable]
	public class ActionMaterialPowerUp : ActionBase
	{
		[JsonProperty("es_powerup", DefaultValueHandling = DefaultValueHandling.Populate)]
        [DefaultValue(1)]
        public float m_ExpScale;

		public override string LocalizeDescription(AttackLogic al, MyMonster monster)
        {
			return LocalizeHelper.LocalizeString("Mat_PowerUp");
        }
	}

	public class ActionMaterialEvolve : ActionBase
    {
        public override string LocalizeDescription(AttackLogic al, MyMonster monster)
        {
            return LocalizeHelper.LocalizeString("Mat_Evolve");
        }
    }

	public class ActionClone : ActionBase
	{
		public ActionBase m_ActionToRepeat;

		public override float PerformAction(BattleManager bm, AttackLogic attackLogic, MonsterOnTheField monster, List<MonsterOnTheField> targets)
		{
			return m_ActionToRepeat.PerformAction(bm, attackLogic, monster, targets);
		}
	}

	[System.Serializable]
	public class ActionApplyableEffect : ActionBase
	{
		[JsonProperty("flag", DefaultValueHandling = DefaultValueHandling.Populate)]
		[DefaultValue(0)]
		public int m_RequiredFlag;

		public override bool CanApplyEffectEnemy(MonsterOnTheField monster, MonsterOnTheField target, int flag)
		{
			return base.CanApplyEffectEnemy(monster, target, flag) && (m_RequiredFlag == 0 || (m_RequiredFlag & flag) != 0);
		}

		public override bool CanApplyEffectFriend(MonsterOnTheField monster, MonsterOnTheField target, int flag)
		{
			return base.CanApplyEffectFriend(monster, target, flag) && (m_RequiredFlag == 0 || (m_RequiredFlag & flag) != 0);
		}

		public override string LocalizeDescription(AttackLogic al, MyMonster monster)
		{
			string str = string.Empty;
			if (m_Probability != 100)
				str += PrefabManager.String_Dash + string.Format(LocalizeHelper.LocalizeString("WithNChance"), m_Probability);

			if (m_RequiredFlag != 0)
			{
				switch (m_RequiredFlag)
				{
					case 1 << (int)ActionFlags.Critical:
						str += PrefabManager.String_Dash + LocalizeHelper.LocalizeString("IfCrit");
						break;
					default:
						str += "I DON'T KNOW THIS";
						break;
				}
			}

			return str;
		}
	}

	[System.Serializable]
	public class ActionChangeEnergyBar : ActionApplyableEffect
	{
		[JsonProperty("value", DefaultValueHandling = DefaultValueHandling.Populate)]
		[DefaultValue(100)]
		public int m_Value = 100;// Negative - decrease, 0 - drop to zero, positive - increase.. all in % [-100...100]

		public override float PerformAction(BattleManager bm, AttackLogic attackLogic, MonsterOnTheField monster, List<MonsterOnTheField> targets)
		{
			base.PerformAction(bm, attackLogic, monster, targets);

			switch (GetEffectDestination())
			{
				case Destination.OneMonster:
					{
						for (int i = 0; i < targets.Count; i++)
							targets[i].ChangeEnergyBar(m_Value);
						break;
					}
				case Destination.Self:
					{
						monster.ChangeEnergyBar(m_Value);
						break;
					}
				case Destination.OneMonsterAndSelf:
					{
						for (int i = 0; i < targets.Count; i++)
							targets[i].ChangeEnergyBar(m_Value);
						monster.ChangeEnergyBar(m_Value);
						break;
					}
				default:
					{
						Debug.LogError("m_Destination " + GetEffectDestination() + " wasn't implemented for " + GetType());
						break;
					}
			}
			return 0;
		}

		public override string LocalizeDescription(AttackLogic al, MyMonster monster)
		{
			return "DON'T USE IT";
		}
	}

	[System.Serializable]
	public class ActionChangeAttackBar : ActionApplyableEffect
	{
		[JsonProperty("value", DefaultValueHandling = DefaultValueHandling.Populate)]
		[DefaultValue(100)]
		public int m_Value = 100;// Negative - decrease, 0 - drop to zero, positive - increase.. all in % [-100...100]

		public override float PerformAction(BattleManager bm, AttackLogic attackLogic, MonsterOnTheField monster, List<MonsterOnTheField> targets)
		{
			base.PerformAction(bm, attackLogic, monster, targets);

			switch (GetEffectDestination())
			{
				case Destination.OneMonster:
					{
						for (int i = 0; i < targets.Count; i++)
							targets[i].ChangeAttackBar(m_Value);
						break;
					}
				case Destination.Self:
					{
						monster.ChangeAttackBar(m_Value);
						break;
					}
				case Destination.OneMonsterAndSelf:
					{
						for (int i = 0; i < targets.Count; i++)
							targets[i].ChangeAttackBar(m_Value);
						monster.ChangeAttackBar(m_Value);
						break;
					}
				default:
					{
						Debug.LogError("m_Destination " + GetEffectDestination() + " wasn't implemented for " + GetType());
						break;
					}
			}
			return 0;
		}

		public override string LocalizeDescription(AttackLogic al, MyMonster monster)
		{
			return "DON'T USE IT";
		}
	}

	[System.Serializable]
	public class ActionChangeAttackAndEnergyBar : ActionApplyableEffect
	{
		[JsonProperty("value", DefaultValueHandling = DefaultValueHandling.Populate)]
		[DefaultValue(100)]
		public int m_Value = 100;// Negative - decrease, 0 - drop to zero, positive - increase.. all in % [-100...100]

		public override float PerformAction(BattleManager bm, AttackLogic attackLogic, MonsterOnTheField monster, List<MonsterOnTheField> targets)
		{
			base.PerformAction(bm, attackLogic, monster, targets);

			int energy = HelpMethods.Round(m_Value * BasicParams.m_GameConstants.EnergyKoeffFromSpeed);
            //Debug.LogWarning("energy " + energy  + " > " + GetEffectDestination());
			switch (GetEffectDestination())
			{
				case Destination.OneMonster:
					{
						for (int i = 0; i < targets.Count; i++)
							targets[i].ChangeAttackAndEnergyBar(m_Value, energy);
						break;
					}
				case Destination.Self:
					{
						monster.ChangeAttackAndEnergyBar(m_Value, energy);
						break;
					}
				case Destination.MyTeam:
					{
						List<MonsterOnTheField> list = bm.GetFullTeam(monster.m_TeamNumber);
						for (int i = 0; i < list.Count; i++)
						{
							MonsterOnTheField mf = list[i];
							if (mf != null && !mf.IsDead())
								mf.ChangeAttackAndEnergyBar(m_Value, energy);
						}
						break;
					}
				case Destination.OneMonsterAndSelf:
					{
						for (int i = 0; i < targets.Count; i++)
							targets[i].ChangeAttackAndEnergyBar(m_Value, energy);
						monster.ChangeAttackAndEnergyBar(m_Value, energy);
						break;
					}
				default:
					{
						Debug.LogError("m_Destination " + GetEffectDestination() + " wasn't implemented for " + GetType());
						break;
					}
			}
			return 0;
		}

		public override string LocalizeDescription(AttackLogic al, MyMonster monster)
		{
			if (m_Value == 0)
				return LocalizeHelper.LocalizeString("ChangeAttackAndEnergy_0") + base.LocalizeDescription(al, monster);
			else
			{
				//int energy = Math.Abs(HelpMethods.Round(m_Value * BasicParams.m_GameConstants.EnergyKoeffFromSpeed));
				int energy = HelpMethods.Round(Math.Abs(m_Value));
				return string.Format(LocalizeHelper.LocalizeString("ChangeAttackAndEnergy"), BuffBase.GetLocalizedChangeText(m_Value), energy) + base.LocalizeDescription(al, monster);
			}
		}
	}

	[System.Serializable]
	public class ActionDispell : ActionApplyableEffect
	{
		[JsonProperty(JsonConstants.Count, DefaultValueHandling = DefaultValueHandling.Populate)]
		[DefaultValue(1)]
		public int m_Count;

		public override float PerformAction(BattleManager bm, AttackLogic attackLogic, MonsterOnTheField monster, List<MonsterOnTheField> targets)
		{
			base.PerformAction(bm, attackLogic, monster, targets);

			switch (GetEffectDestination())
			{
				case Destination.OneMonster:
					{
						for (int i = 0; i < targets.Count; i++)
						{
							if (targets[i].IsActivated())
								targets[i].DispellEffects(monster, m_Count);
						}
						break;
					}
				case Destination.Self:
					{
						monster.DispellNegativeEffects(m_Count);
						break;
					}
				case Destination.OneMonsterAndSelf:
					{
						for (int i = 0; i < targets.Count; i++)
						{
							if (targets[i].IsActivated())
								targets[i].DispellEffects(monster, m_Count);
						}
						monster.DispellNegativeEffects(m_Count);
						break;
					}
				default:
					{
						Debug.LogError("m_Destination " + GetEffectDestination() + " wasn't implemented for " + GetType());
						break;
					}
			}
			return 0;
		}

		public override string LocalizeDescription(AttackLogic al, MyMonster monster)
		{
			string fromStr = LocalizeHelper.LocalizeString(al.m_TargetType == TargetType.Enemy ? "FromEnemies" : "FromAllies");
			fromStr = string.Format(fromStr, al.m_TargetsCount == 0 ? LocalizeHelper.LocalizeString(JsonConstants.All) : m_Count.ToString());
            string str = string.Format(LocalizeHelper.LocalizeString("ActionDispells"), m_Count, LocalizeHelper.LocalizeString(al.m_TargetType == TargetType.Friend ? "Debuffs" : "Buffs"), fromStr);
			if (GetEffectDestination() == Destination.OneMonsterAndSelf)
				str += PrefabManager.String_Dash + LocalizeHelper.LocalizeString(JsonConstants.AndSelf);
			return str;
		}
	}

	[System.Serializable]
	public class ActionApplyBuff : ActionApplyableEffect
	{
		[JsonProperty(JsonConstants.Buff, DefaultValueHandling = DefaultValueHandling.Populate)]
		private BuffMeta m_Buff = null;

		public override float PerformReverseAction(BattleManager bm, MonsterOnTheField monster, List<MonsterOnTheField> targets)
		{
			base.PerformReverseAction(bm, monster, targets);

			if (monster.IsActivated())
			{
				switch (GetEffectDestination())
				{
					case Destination.OneMonster:
						{
							for (int i = 0; i < targets.Count; i++)
							{
								if (targets[i].IsActivated())
								{
									if (!targets[i].StopBuff(this))
										Debug.LogError("Couldn't Revense buff " + m_Buff.BufType);
								}
							}
							break;
						}
					case Destination.Self:
						{
							if (!monster.StopBuff(this))
								Debug.LogError("Couldn't Revense buff " + m_Buff.BufType + " > " + monster.m_MyMonster.GetName());
							break;
						}
					case Destination.OneMonsterAndSelf:
						{
							for (int i = 0; i < targets.Count; i++)
							{
								if (targets[i].IsActivated())
								{
									if (!targets[i].StopBuff(this))
										Debug.LogError("Couldn't Revense buff " + m_Buff.BufType);
								}
							}
							if (!monster.StopBuff(this))
								Debug.LogError("Couldn't Revense buff " + m_Buff.BufType);
							break;
						}
					default:
						{
							Debug.LogError("m_Destination " + GetEffectDestination() + " wasn't implemented for " + GetType());
							break;
						}
				}
			}
			return 0f;
		}

        public int m_FlagFromDamage = 0;

		private bool ApplyBuffToTarget(BattleManager bm, MonsterOnTheField monster, MonsterOnTheField target)
		{
			if (target.IsDead())
				return false;

			bool apply = target.CanApplyBuffs(monster) || m_Reverse;
			ActionFlags flag = ActionFlags.Count;
			if (apply)
			{
				if (BattleManager.AreEnemies(monster, target))
				{
					int rnd = HelpMethods.GetRandom();
					if (rnd < m_Probability)
					{
                        if (!CanApplyEffectEnemy(monster, target, m_FlagFromDamage))
						{
							apply = false;
							flag = ActionFlags.Resist;
						}
					}
					else
					{
						apply = false;
						flag = ActionFlags.Missed;
					}
				}
			}
			else
				flag = ActionFlags.Immune;

			if (apply)
			{
				BuffBase b = BuffBase.GetBuffByType(m_Buff.BufType);
				b.InitBuffParams(m_Buff);

				if (!b.CanStack())
				{
					for (int i = 0; i < target.m_AllBuffs.Count; i++)
					{
						BuffBase buff = target.m_AllBuffs[i];
						if (b.IsTheSame(buff))
						{
							buff.TryToUpdateDuration(b);
							b.ReturnClass();
							return true;
						}
					}
				}

				b.m_Creator = this;
				b.m_ShowIcon = !m_Reverse;
				b.ApplyToMonster(monster, target, bm);
				return true;
			}

			if (flag < ActionFlags.Count)
				target.ShowText(flag);
			return false;
		}

		public override float PerformAction(BattleManager bm, AttackLogic attackLogic, MonsterOnTheField monster, List<MonsterOnTheField> targets)
		{
			base.PerformAction(bm, attackLogic, monster, targets);
			if (monster.IsActivated())
			{
				switch (GetEffectDestination())
				{
					case Destination.OneMonster:
						{
							for (int i = 0; i < targets.Count; i++)
							{
								if (targets[i].IsActivated())
									ApplyBuffToTarget(bm, monster, targets[i]);
							}
							break;
						}
					case Destination.Self:
						{
							ApplyBuffToTarget(bm, monster, monster);
							break;
						}
					case Destination.OneMonsterAndSelf:
						{
							for (int i = 0; i < targets.Count; i++)
							{
								if (targets[i].IsActivated())
									ApplyBuffToTarget(bm, monster, targets[i]);
							}
							ApplyBuffToTarget(bm, monster, monster);
							break;
						}
					default:
						{
							Debug.LogError("m_Destination " + GetEffectDestination() + " wasn't implemented for " + GetType());
							break;
						}
				}
			}
			return 0f;
		}

		public override void Print()
		{
			m_Buff.Print();
		}

		public override string LocalizeDescription(AttackLogic al, MyMonster monster)
		{
			if (m_Reverse)
				return BuffBase.GetReverseDescription(m_Buff);
			string str = BuffBase.GetDescription(m_Buff, monster);
			if (m_Buff.HasParam(JsonConstants.StepsCount))
				str += PrefabManager.String_Dash + string.Format(LocalizeHelper.LocalizeString("ForNMoves"), m_Buff.GetInt(JsonConstants.StepsCount));
			return str + base.LocalizeDescription(al, monster);
		}

		[System.Serializable]
		public class BuffMeta
		{
			[JsonProperty(JsonConstants.BuffType, DefaultValueHandling = DefaultValueHandling.Populate)]
			[DefaultValue(BuffType.Stun)]
			public BuffType BufType { get; private set; }

			[JsonProperty(JsonConstants.AssetInfo, DefaultValueHandling = DefaultValueHandling.Populate)]
			public PrefabManager.AssetPathInfo AssetInfo { get; private set; }

			[JsonProperty(JsonConstants.DamageCalculator, DefaultValueHandling = DefaultValueHandling.Populate)]
			public DamageCalculator DamageCalculator { get; private set; }

			[JsonProperty(JsonConstants.Params, DefaultValueHandling = DefaultValueHandling.Populate)]
			private Dictionary<string, object> Params { get; set; }

			public int GetInt(string name)
			{
				object res;
				if (Params.TryGetValue(name, out res))
					return Convert.ToInt32(res);
				return 0;
			}

			public float GetFloat(string name)
			{
				object res;
				if (Params.TryGetValue(name, out res))
					return Convert.ToSingle(res);
				return 0;
			}

			public float GetFloat(string name, float defaultValue)
            {
                object res;
                if (Params.TryGetValue(name, out res))
                    return Convert.ToSingle(res);
				return defaultValue;
            }

			public bool HasParam(string name)
			{
				return Params.ContainsKey(name);
			}

			public string GetParam(string name)
			{
				object s;
				if (Params.TryGetValue(name, out s))
					return s as string;
				return null;
			}

			public void Print()
			{
				Debug.LogWarning("=====" + BufType + "=====");

				foreach (var param in Params)
				{
					Debug.LogWarning(param.Key + " => " + param.Value.ToString());
				}

				Debug.LogWarning("==========");
			}
		}
	}

	[System.Serializable]
	//    [JsonConverter(typeof(ActionAnimatorConverter))]
	public class ActionAnimatorTrigger : ActionBase
	{
		[JsonIgnore]
		private int m_Name;

		[JsonProperty("name")]
		private string m_OriginalName;

		[JsonProperty("animation", DefaultValueHandling = DefaultValueHandling.Populate)]
		[DefaultValue(CameraTracker.CameraAnimation.None)]
		private CameraTracker.CameraAnimation m_CameraAnimation;

		public override void Init()
		{
			base.Init();

			m_Name = Animator.StringToHash(m_OriginalName);
		}

		public override float PerformAction(BattleManager bm, AttackLogic attackLogic, MonsterOnTheField monster, List<MonsterOnTheField> targets)
		{
			if (monster.CanPlayAnimations())
				monster.m_Animation.ActivateTrigger(m_Name);
			if (m_CameraAnimation != CameraTracker.CameraAnimation.None)
				AnimationsManager.m_Instance.StartCoroutine(PlayCameraAnimation(monster));
			return base.PerformAction(bm, attackLogic, monster, targets);
		}

		IEnumerator PlayCameraAnimation(MonsterOnTheField monster)
		{
			if (HelpMethods.CanAnimateCamera(0.3f) || monster.GetBattleManager().AlwaysPlayCoolEffects())
			{
				int animId = CameraTracker.m_Instance.PlayAnimation(monster.GetParent(), m_CameraAnimation);
				yield return PrefabManager.Coroutine_WaitForSeconds1;
				while (CameraTracker.m_Instance.IsAnyScalerAnimation())
					yield return PrefabManager.m_WaitForEndOfFrame;
				CameraTracker.m_Instance.StopAnimation(animId);
			}
		}

		public override void Print()
		{
			base.Print();

			Debug.Log("m_Name: " + m_Name);
		}
	}

	[System.Serializable]
	//    [JsonConverter(typeof(ActionAnimatorConverter))]
	public class ActionAnimatorBool : ActionBase
	{
		[JsonIgnore]
		private int m_Name;

		[JsonProperty("name")]
		private string m_OriginalName;

		[JsonProperty("animatorValue")]
		private bool m_Value;

		[JsonProperty("anyState")]
		private bool m_AnyState;

		public override void Init()
		{
			base.Init();

			m_Name = Animator.StringToHash(m_OriginalName);
		}

		public override float PerformAction(BattleManager bm, AttackLogic attackLogic, MonsterOnTheField monster, List<MonsterOnTheField> targets)
		{
			if (monster.CanPlayAnimations())
			{
				if (m_AnyState)
					monster.m_Animation.ActivateTrigger(AnimationsManager.Animation_AnyState);
				monster.m_Animation.SetBool(m_Name, m_Value);
			}
			return base.PerformAction(bm, attackLogic, monster, targets);
		}

		public override void Print()
		{
			base.Print();

			Debug.Log("m_Value: " + m_Value);
			Debug.Log("m_Name: " + m_Name);
		}
	}

	[System.Serializable]
	public class ActionDamage : ActionBase
	{
		[JsonProperty("damageCalculator")]
		protected DamageCalculator m_DamageCalculator;

		[JsonProperty("actions", DefaultValueHandling = DefaultValueHandling.Populate)]
		[JsonConverter(typeof(ActionsConverter))]
		[DefaultValue(typeof(List<ActionBase>), "[]")]
		protected List<ActionBase> m_AdditionalEffects;

		[JsonProperty("lifesteal")]
		protected float m_LifeSteal;

		protected virtual int DamageMonster(BattleManager bm, AttackLogic attackLogic, MonsterOnTheField monster, MonsterOnTheField target)
		{
			if (!monster.IsActivated() || !target.IsActivated())
				return 0;

			int flag;
			int damage = bm.DamageMonster(monster, target, m_DamageCalculator, attackLogic, out flag);
			if (m_AdditionalEffects != null)
			{
				for (int i = 0; i < m_AdditionalEffects.Count; i++)
				{
					ActionBase ae = m_AdditionalEffects[i];
                    if (ae is ActionApplyBuff)
                    {
                        (ae as ActionApplyBuff).m_FlagFromDamage = flag;
                        ae.PerformAction(bm, attackLogic, monster, target);
                    }
					else
					{
						int rnd = HelpMethods.GetRandom();
                        //Debug.Log("rnd "  + rnd + " < " + ae.m_Probability + " ,> " + ae.GetEffectDestination());
						if (rnd < ae.m_Probability)
						{
							switch (ae.GetEffectDestination())
							{
								case Destination.OneMonster:
                                    if (ae.CanApplyEffectEnemy(monster, target, flag))
                                        ae.PerformAction(bm, attackLogic, monster, target);
									else
                                        target.ShowTextForAllFlags(BuffBase.GetFlag(ActionFlags.Resist));
										//flag |= BuffBase.GetFlag(ActionFlags.Resist);
									break;
								case Destination.Self:
									if (ae.CanApplyEffectFriend(monster, monster, flag))
										ae.PerformAction(bm, attackLogic, monster, monster);
									break;
								case Destination.OneMonsterAndSelf:
									if (ae.CanApplyEffectEnemy(monster, target, flag))
										ae.PerformAction(bm, attackLogic, monster, target);
									else
										//flag |= BuffBase.GetFlag(ActionFlags.Resist);
                                        target.ShowTextForAllFlags(BuffBase.GetFlag(ActionFlags.Resist));

									if (ae.CanApplyEffectFriend(monster, monster, flag))
										ae.PerformAction(bm, attackLogic, monster, monster);
									break;
								default:
									{
										Debug.LogError("m_Destination " + ae.GetEffectDestination() + " wasn't implemented for " + GetType());
										break;
									}
							}
						}
					}
				}
			}

			if (m_LifeSteal > 0.01f && damage > 0)
			{
				int lifeStealAmount = HelpMethods.Round(m_LifeSteal * damage);
				int flagHeal;
				bm.HealMonster(monster, monster, lifeStealAmount, out flagHeal);
			}
			return damage;
		}

		protected virtual float MakeDamageAction(BattleManager bm, AttackLogic attackLogic, MonsterOnTheField monster, List<MonsterOnTheField> targets)
		{
			if (targets.Count >= 1 && m_Destination == Destination.TargetTeam)
			{
				List<MonsterOnTheField> list = bm.GetFullTeam(targets[0].m_TeamNumber);
				for (int i = 0; i < list.Count; i++)
				{
					MonsterOnTheField mf = list[i];
					if (mf != null && !mf.IsDead() && mf.CanBeAttacked())
						DamageMonster(bm, attackLogic, monster, mf);
				}
			}
			else
			{
				for (int i = 0; i < targets.Count; i++)
					DamageMonster(bm, attackLogic, monster, targets[i]);
			}
			return 0f;
		}

		public override float PerformAction(BattleManager bm, AttackLogic attackLogic, MonsterOnTheField monster, List<MonsterOnTheField> targets)
		{
			base.PerformAction(bm, attackLogic, monster, targets);
			return MakeDamageAction(bm, attackLogic, monster, targets);
		}

		public override string LocalizeDescription(AttackLogic al, MyMonster monster)
		{
			if (m_DamageCalculator == null)
				return null;

			string mainFormat = LocalizeHelper.LocalizeString("ActionDamage");
			string targets = al.m_TargetsCount == 0 ? LocalizeHelper.LocalizeString(JsonConstants.All) : al.m_TargetsCount.ToString();

			string ofATarget = m_DamageCalculator.GetLocalization(monster, this);
			string result = string.Format(mainFormat, targets, ofATarget);

			if (m_LifeSteal > 0.01f)
				result += PrefabManager.String_DotDash + string.Format(LocalizeHelper.LocalizeString("ActionDamage_lifesteal"), HelpMethods.RoundPercentage(m_LifeSteal));

			if (m_AdditionalEffects != null)
			{
				for (int i = 0; i < m_AdditionalEffects.Count; i++)
				{
					string s = m_AdditionalEffects[i].LocalizeDescription(al, monster);
#if DESC_LOGS
					Debug.Log("s> = " + m_AdditionalEffects[i].GetType() + " > " + s);
#endif
					if (s != null)
						result += PrefabManager.String_DotDash + s;
				}
			}

			return result;
		}

		public override void Print()
		{
			base.Print();

			Debug.Log("m_DamageCalculator: " + m_DamageCalculator);
		}
	}

	[System.Serializable]
	public class ActionHeal : ActionDamage
	{
		protected override int DamageMonster(BattleManager bm, AttackLogic attackLogic, MonsterOnTheField monster, MonsterOnTheField target)
		{
			int heal = 0;
			if (target != null && monster.IsActivated() && target.IsActivated())
			{
				int flag;
				heal = bm.HealMonster(monster, target, m_DamageCalculator, attackLogic, out flag);
				if (m_AdditionalEffects != null)
				{
					for (int i = 0; i < m_AdditionalEffects.Count; i++)
						m_AdditionalEffects[i].PerformAction(bm, attackLogic, monster, target);
				}
			}
			return heal;
		}

		protected override float MakeDamageAction(BattleManager bm, AttackLogic attackLogic, MonsterOnTheField monster, List<MonsterOnTheField> targets)
		{
			if (GetEffectDestination() == Destination.Self)
			{
				DamageMonster(bm, attackLogic, monster, monster);
			}
			else
			{
				if (GetEffectDestination() == Destination.MyTeam)
				{
					List<MonsterOnTheField> list = bm.GetFullTeam(monster.m_TeamNumber);
					base.MakeDamageAction(bm, attackLogic, monster, list);
				}
				else
				{
					base.MakeDamageAction(bm, attackLogic, monster, targets);
					if (GetEffectDestination() == Destination.OneMonsterAndSelf)
						DamageMonster(bm, attackLogic, monster, monster);
				}
			}
			return 0f;
		}

		public override string LocalizeDescription(AttackLogic al, MyMonster monster)
		{
			string mainFormat = LocalizeHelper.LocalizeString("ActionHeal");
			string targets = al.m_TargetsCount == 0 ? LocalizeHelper.LocalizeString(JsonConstants.All) : al.m_TargetsCount.ToString();

			string ofATarget = m_DamageCalculator.GetLocalization(monster, this);
			string self = (GetEffectDestination() == Destination.OneMonsterAndSelf) ? (PrefabManager.String_Dash + LocalizeHelper.LocalizeString(JsonConstants.AndSelf)) : string.Empty;
			string result = string.Format(mainFormat, targets, self, ofATarget);

			if (m_AdditionalEffects != null)
			{
				for (int i = 0; i < m_AdditionalEffects.Count; i++)
				{
					string s = m_AdditionalEffects[i].LocalizeDescription(al, monster);
#if DESC_LOGS
					Debug.Log("s> = " + m_AdditionalEffects[i].GetType() + " > " + s);
#endif
					if (s != null)
						result += PrefabManager.String_DotDash + s;
				}
			}

			return result;
		}
	}

	[System.Serializable]
	public class ActionRevive : ActionBase
	{
		[JsonProperty("damageCalculator")]
		protected DamageCalculator m_DamageCalculator;

		public override float PerformAction(BattleManager bm, AttackLogic attackLogic, MonsterOnTheField monster, List<MonsterOnTheField> targets)
		{
			base.PerformAction(bm, attackLogic, monster, targets);
			for (int i = 0; i < targets.Count; i++)
				targets[i].ReviveMonster(monster, m_DamageCalculator, true);

			return 0f;
		}

		public override string LocalizeDescription(AttackLogic al, MyMonster monster)
		{
			string mainFormat = LocalizeHelper.LocalizeString("ActionRevive");
			string targets = al.m_TargetsCount == 0 ? LocalizeHelper.LocalizeString(JsonConstants.All) : al.m_TargetsCount.ToString();

			string ofATarget = m_DamageCalculator.GetLocalization(monster, this);
			string result = string.Format(mainFormat, targets, ofATarget);

			return result;
		}
	}

    public class ActionShakeCamera : ActionBase
    {
        [JsonProperty(JsonConstants.Scale, DefaultValueHandling = DefaultValueHandling.Populate)]
        [DefaultValue(1)]
        public float m_ShakePower;

        [JsonProperty(JsonConstants.Duration, DefaultValueHandling = DefaultValueHandling.Populate)]
        [DefaultValue(1)]
        public float m_ShakeDuration;

        public override float PerformAction(BattleManager bm, AttackLogic attackLogic, MonsterOnTheField monster, List<MonsterOnTheField> targets)
        {
            CameraTracker.m_Instance.PlayShakeAnimation(m_ShakePower, m_ShakeDuration);
            return base.PerformAction(bm, attackLogic, monster, targets);
        }
    }

	public class ActionEffect : ActionBase
	{
		[JsonProperty("lookat", DefaultValueHandling = DefaultValueHandling.Populate)]
		[JsonConverter(typeof(StringEnumConverter))]
		[DefaultValue(Destination.OneMonster)]
		Destination m_LookAt = Destination.OneMonster;

		[JsonProperty("assetInfo")]
		PrefabManager.AssetPathInfo m_AssetInfo = null;

		[JsonProperty("childName", DefaultValueHandling = DefaultValueHandling.Populate)]
		[DefaultValue(null)]
		string m_ChildName = null;

		private void CreateEffectAt(BattleManager bm, MonsterOnTheField monster, List<MonsterOnTheField> targets, Transform trm)
		{
			PrefabManager.PrefabInfoWithInitializer pInfo;
			if (m_LookAt == Destination.OneMonster)
			{
				pInfo = m_AssetInfo.GetInstantiatedPrefabFromAssetInfo(PrefabManager.GetPrefabInfoWithInitializer, null, this, trm) as PrefabManager.PrefabInfoWithInitializer;
			}
			else
			{
				pInfo = m_AssetInfo.GetInstantiatedPrefabFromAssetInfo(PrefabManager.GetPrefabInfoWithInitializer, null, this, trm) as PrefabManager.PrefabInfoWithInitializer;
				Vector3 p;
				switch (m_LookAt)
				{
					case Destination.Self:
						p = monster.GetLocalPosition();
						break;
					case Destination.TargetTeam:
						p = bm.GetTeamCastPoint(targets[0].m_TeamNumber);
						break;
					default:
						Debug.LogError("m_Destination> " + m_Destination + " wasn't implemented for " + GetType());
						return;
				}

				if ((pInfo.m_Initializer is ObjLookDistance))
				{
					ObjLookDistance look = pInfo.m_Initializer as ObjLookDistance;
					look.SetLookAt(p);
				}
				else
				{
					Vector3 pp = pInfo.m_Transform.position;
					p = pp + PrefabManager.GetLookVectorProjection(p - pp, pInfo.m_Transform.up);
					pInfo.m_Transform.LookAt(p, pInfo.m_Transform.up);
				}
			}

			if ((pInfo.m_Initializer is ObjCameraFollower) && (HelpMethods.CanAnimateCamera(0.3f) || bm.AlwaysPlayCoolEffects()))
				AnimationsManager.m_Instance.StartCoroutine(PlayCameraFollowWithShowOff(monster, pInfo.m_Initializer as ObjCameraFollower));
		}

		IEnumerator PlayCameraFollowWithShowOff(MonsterOnTheField monster, ObjCameraFollower follower)
		{
			if (monster.m_TeamNumber == GameManager.EnemyTeamNumber)
				follower.m_Transform.localRotation = Quaternion.Euler(0, 180, 0);
			
			if (HelpMethods.CanAnimateCamera(0.3f) || monster.GetBattleManager().AlwaysPlayCoolEffects())
			{
				if (monster.HasVisualAssets())
					monster.m_Animation.AccelerateAnimationForMatrixEffects();
				int animId = CameraTracker.m_Instance.PlayAnimation(monster.GetParent(), CameraTracker.CameraAnimation.HeroShowoff);
				yield return PrefabManager.Coroutine_WaitForSeconds1;
				while (CameraTracker.m_Instance.IsAnyScalerAnimation())
					yield return PrefabManager.m_WaitForEndOfFrame;
				if (monster.HasVisualAssets())
					monster.m_Animation.ReturnNormalSpeed();
				CameraTracker.m_Instance.StopAnimation(animId, -1);
			}
			follower.StartCameraAnimation();
		}

		public override float PerformAction(BattleManager bm, AttackLogic attackLogic, MonsterOnTheField monster, List<MonsterOnTheField> targets)
		{
			base.PerformAction(bm, attackLogic, monster, targets);

			if (monster.CanPlayAnimations())
			{
				switch (m_Destination)
				{
					case Destination.OneMonster:
						{
							for (int i = 0; i < targets.Count; i++)
							{
								if (targets[i].CanPlayAnimations())
									CreateEffectAt(bm, monster, targets, targets[i].GetParent());
							}
							break;
						}
					case Destination.TargetTeam:
						{
							CreateEffectAt(bm, monster, targets, bm.GetTeamTransform(targets[0].m_TeamNumber));
							break;
						}
					case Destination.MyTeam:
						{
							CreateEffectAt(bm, monster, targets, bm.GetTeamTransform(monster.m_TeamNumber));
							break;
						}
					case Destination.Self:
						{
							CreateEffectAt(bm, monster, targets, monster.GetParent());
							break;
						}
					case Destination.SelfModel:
						{
							CreateEffectAt(bm, monster, targets, monster.m_Animation.m_Transform);
							break;
						}
					case Destination.OneMonsterAndSelf:
						{
							for (int i = 0; i < targets.Count; i++)
							{
								if (targets[i].CanPlayAnimations())
									CreateEffectAt(bm, monster, targets, targets[i].GetParent());
							}
							CreateEffectAt(bm, monster, targets, monster.GetParent());
							break;
						}
					case Destination.ChildTransform:
						{
							Transform trm = monster.m_Animation.m_ModelInfo.GetChildByName(m_ChildName);
							CreateEffectAt(bm, monster, targets, trm);
							break;
						}
					default:
						Debug.LogError("m_Destination " + m_Destination + " wasn't implemented for " + GetType());
						break;
				}
			}
			return 0f;
		}

		public override void Print()
		{
			base.Print();

			Debug.Log("m_Destination: " + m_Destination);
			Debug.Log("m_AssetInfo: " + m_AssetInfo.MyToString());
		}
	}

	[System.Serializable]
	public class ActionJumpToTarget : ActionBase
	{
		[JsonProperty("flightDuration")]
		float m_FlightDuration = 0;

		[JsonProperty("height")]
		float m_JumpHeight = 0;

		[JsonProperty("stayTime")]
		float m_StayTime = 0;

		[JsonProperty("run")]
		public bool m_PlayRun;

		public override float PerformAction(BattleManager bm, AttackLogic attackLogic, MonsterOnTheField monster, List<MonsterOnTheField> targets)
		{
			base.PerformAction(bm, attackLogic, monster, targets);
			if (!bm.CanPlayInstantly() && monster.CanPlayAnimations())
			{
				switch (m_Destination)
				{
					case Destination.OneMonster:
					case Destination.TargetTeam:
						{
							if (targets.Count == 1)
							{
								if (targets[0].CanPlayAnimations())
									AnimationsManager.m_Instance.StartCoroutine(JumpToPoint(bm, monster, targets[0].GetParent(), attackLogic.CanPlayCameraAnimations()));
							}
							else
							{
								if (targets.Count > 1)
								{
									if (targets[0].CanPlayAnimations())
										AnimationsManager.m_Instance.StartCoroutine(JumpToPoint(bm, monster, bm.GetTeamTransform(targets[0].m_TeamNumber), attackLogic.CanPlayCameraAnimations()));
								}
								else
									Debug.LogError("1>Wrong Amount of targets for ActionJumpToTarget " + targets.Count);
							}
							break;
						}

					//{
					//    if (targets.Count >= 1)
					//    {
					//        if (targets[0].CanPlayAnimations())
					//            AnimationsManager.m_Instance.StartCoroutine(JumpToPoint(bm, monster, bm.GetTeamTransform(targets[0].m_TeamNumber), attackLogic.CanPlayCameraAnimations()));
					//    }
					//    else
					//        Debug.LogError("2>Wrong Amount of targets for ActionJumpToTarget " + targets.Count);
					//    break;
					//}
					case Destination.MyTeam:
						{
							AnimationsManager.m_Instance.StartCoroutine(JumpToPoint(bm, monster, bm.GetTeamTransform(monster.m_TeamNumber), attackLogic.CanPlayCameraAnimations()));
							break;
						}
					default:
						Debug.LogError("m_Destination " + m_Destination + " wasn't implemented for " + GetType());
						break;
				}
			}
			return (m_FlightDuration + m_StayTime) / bm.GetTimeScale();
		}

		IEnumerator JumpToPoint(BattleManager bm, MonsterOnTheField monster, Transform pointTrm, bool anim)
		{
			Vector3 initialPos = monster.GetInitialPosition();
			float radius = initialPos.magnitude;
			float duration = m_FlightDuration / bm.GetTimeScale();

			float gravity = 8 * m_JumpHeight / (duration * duration);
			float t = GameManager.m_LastDeltaTime;
			Vector3 LookAt = pointTrm.position;
			Vector3 hDir = (LookAt - initialPos).normalized;
			Vector3 point = LookAt - hDir * 2;

			Vector3 upFinal;
			Vector3 up = Vector3.up;
#if UNITY_EDITOR
			if (bm.m_FlatSurface)
			{
				upFinal = Vector3.up;
			}
			else
#endif
			{
				upFinal = point.normalized;
				point = upFinal * radius;
			}


			Quaternion q = Quaternion.identity;

			int animId = anim && HelpMethods.CanAnimateCamera(0.5f) ? CameraTracker.m_Instance.PlayAnimation(monster.m_Animation.m_Transform, (m_JumpHeight > 3 && HelpMethods.CanAnimateCamera(0.3f)) ? CameraTracker.CameraAnimation.MoveBehindMatrix : CameraTracker.CameraAnimation.MoveBehind) : 0;

			//monster.m_Animation.m_Transform.LookAt(point, up);
			if (m_PlayRun)
				monster.SetRunningSpeed(1);
			while (t < duration)
			{
				if (monster.CanPlayAnimations())
				{
					float dt = t / duration;
					//up = Vector3.Lerp(upInit, upFinal, dt);
					Vector3 p = Vector3.Lerp(initialPos, point, dt);
#if UNITY_EDITOR
					if (bm.m_FlatSurface)
					{

					}
					else
#endif
					{
						up = p.normalized;
						p = up * (radius + HelpMethods.GetHeightForParabolicMove(gravity, duration, t));
					}
					q = Quaternion.LookRotation(PrefabManager.GetLookVectorProjection(LookAt - p, up), up);
					monster.SetPositionForAnimation(p, q);
					yield return PrefabManager.m_WaitForEndOfFrame;
					t += GameManager.m_LastDeltaTime;
				}
				else
				{
					CameraTracker.m_Instance.StopAnimation(animId);
					yield break;
				}
			}

			if (m_PlayRun)
				monster.SetRunningSpeed(0);
			q = Quaternion.LookRotation(PrefabManager.GetLookVectorProjection(LookAt - point, upFinal), upFinal);
			monster.SetPositionForAnimation(point, q);

			float wt = m_StayTime / bm.GetTimeScale();
			if (CheckTime(wt))
				yield return new WaitForSeconds(wt);

			CameraTracker.m_Instance.StopAnimation(animId, 0);

			if (monster.CanPlayAnimations())
				PrefabManager.MakePosAndRotIdentity(monster.m_Animation.m_Transform);
			//monster.SetPositionForAnimation(initialPos, initialRot);
		}

		public override void Print()
		{
			base.Print();

			Debug.Log("m_FlightDuration: " + m_FlightDuration);
			Debug.Log("m_JumpHeight: " + m_JumpHeight);
			Debug.Log("m_StayTime: " + m_StayTime);
		}
	}

	public class ActionLaunchBall : ActionDamage
	{
		[JsonProperty("flightDuration")]
		float m_FlightDuration = 0;

		[JsonProperty("damageAtTheEnd")]
		bool m_DamageAtTheEnd = false;

		[JsonProperty("launchPeriod")]
		float m_LaunchPeriod = 0;

		[JsonProperty("inverse")]
		bool m_Inverse = false;

		[JsonProperty("assetInfo")]
		PrefabManager.AssetPathInfo m_AssetInfo = null;

		public override void Print()
		{
			base.Print();

			Debug.Log("m_Destination: " + m_Destination);
			Debug.Log("m_FlightDuration: " + m_FlightDuration);
			Debug.Log("m_DamageAtTheEnd: " + m_DamageAtTheEnd);
			Debug.Log("m_LaunchPeriod: " + m_LaunchPeriod);
			Debug.Log("m_AssetInfo: " + m_AssetInfo.MyToString());
		}

		IEnumerator AnimateTargetsAttack(BattleManager bm, AttackLogic attackLogic, MonsterOnTheField monster, List<MonsterOnTheField> targets)
		{
			float period = m_LaunchPeriod / bm.GetTimeScale();
			bool canAnimateCamera = targets.Count == 1 && m_Repeat == 0 && attackLogic.CanPlayCameraAnimations();
			for (int i = 0; i < targets.Count - 1; i++)
			{
				AnimationsManager.m_Instance.StartCoroutine(AnimateSingleTargetAttack(bm, attackLogic, monster, targets[i], canAnimateCamera));
				if (CheckTime(period))
					yield return new WaitForSeconds(period);
			}

			yield return AnimationsManager.m_Instance.StartCoroutine(AnimateSingleTargetAttack(bm, attackLogic, monster, targets[targets.Count - 1], canAnimateCamera));
		}

		IEnumerator AnimateSingleTargetAttack(BattleManager bm, AttackLogic attackLogic, MonsterOnTheField monster, MonsterOnTheField target, bool canAnimateCamera)
		{
			if (m_Inverse)
				DamageMonster(bm, attackLogic, monster, target);

			float duration = m_FlightDuration / bm.GetTimeScale();
			if (monster.CanPlayAnimations() && target.CanPlayAnimations())
				yield return AnimationsManager.LaunchBall(m_AssetInfo, bm, monster.GetLocalPointForCast(), target.GetLocalPointForCast(), duration, true, this, m_Inverse, canAnimateCamera);
			else
				if (CheckTime(duration))
				yield return new WaitForSeconds(duration);
			if (!m_Inverse)
				DamageMonster(bm, attackLogic, monster, target);
		}

		IEnumerator AnimateTargetsAttackOneBall(BattleManager bm, AttackLogic attackLogic, MonsterOnTheField monster, List<MonsterOnTheField> targets)
		{
			if (m_Inverse)
				base.MakeDamageAction(bm, attackLogic, monster, targets);
			float duration = m_FlightDuration / bm.GetTimeScale();
			if (monster.CanPlayAnimations())
				yield return AnimationsManager.LaunchBall(m_AssetInfo, bm, monster.GetLocalPointForCast(), bm.GetTeamCastPointBall(targets[0].m_TeamNumber), duration, true, this, m_Inverse, !m_Inverse);
			else
				if (CheckTime(duration))
				yield return new WaitForSeconds(duration);
			if (!m_Inverse)
				base.MakeDamageAction(bm, attackLogic, monster, targets);
		}

		IEnumerator LaunchBallsWithDelay(BattleManager bm, MonsterOnTheField monster, List<MonsterOnTheField> targets)
		{
			float duration = m_FlightDuration / bm.GetTimeScale();
			float period = m_LaunchPeriod / bm.GetTimeScale();
			for (int i = 0; i < targets.Count - 1; i++)
			{
				if (targets[i].CanPlayAnimations())
					AnimationsManager.LaunchBallMethod(m_AssetInfo, bm, monster.GetLocalPointForCast(), targets[i].GetLocalPointForCast(), duration, true, this, m_Inverse);
				if (CheckTime(period))
					yield return new WaitForSeconds(period);
			}

			MonsterOnTheField lastTarget = targets[targets.Count - 1];
			if (lastTarget.CanPlayAnimations())
				AnimationsManager.LaunchBallMethod(m_AssetInfo, bm, monster.GetLocalPointForCast(), lastTarget.GetLocalPointForCast(), duration, true, this, m_Inverse);
		}

		protected override float MakeDamageAction(BattleManager bm, AttackLogic attackLogic, MonsterOnTheField monster, List<MonsterOnTheField> targets)
		{
			if (!bm.CanPlayInstantly())
			{
				if (monster.CanPlayAnimations())
				{
					if (!m_DamageAtTheEnd)
					{
						switch (m_Destination)
						{
							case Destination.OneMonster:
								{
									if (m_LaunchPeriod > 0.001f)
										AnimationsManager.m_Instance.StartCoroutine(LaunchBallsWithDelay(bm, monster, targets));
									else
									{
										for (int i = 0; i < targets.Count; i++)
											if (targets[i].CanPlayAnimations())
												AnimationsManager.LaunchBallMethod(m_AssetInfo, bm, monster.GetLocalPointForCast(), targets[i].GetLocalPointForCast(), m_FlightDuration, true, this, m_Inverse);
									}
									break;
								}
							case Destination.TargetTeam:
								{
									if (targets[0].CanPlayAnimations())
										AnimationsManager.LaunchBallMethod(m_AssetInfo, bm, monster.GetLocalPointForCast(), bm.GetTeamCastPoint(targets[0].m_TeamNumber), m_FlightDuration, true, this, m_Inverse);
									break;
								}
							case Destination.MyTeam:
								{
									AnimationsManager.LaunchBallMethod(m_AssetInfo, bm, monster.GetLocalPointForCast(), bm.GetTeamCastPoint(monster.m_TeamNumber), m_FlightDuration, true, this, m_Inverse);
									break;
								}
							default:
								Debug.LogError("m_Destination " + m_Destination + " wasn't implemented for " + GetType());
								break;
						}
					}
					else
					{
						switch (m_Destination)
						{
							case Destination.OneMonster:
								{
									AnimationsManager.m_Instance.StartCoroutine(AnimateTargetsAttack(bm, attackLogic, monster, targets));
									break;
								}
							case Destination.TargetTeam:
								{
									AnimationsManager.m_Instance.StartCoroutine(AnimateTargetsAttackOneBall(bm, attackLogic, monster, targets));
									break;
								}
							default:
								Debug.LogError("m_Destination " + m_Destination + " wasn't implemented for " + GetType());
								break;
						}
					}
				}
			}
			else
			{
				//No animation mode
				if (m_DamageAtTheEnd)
					base.MakeDamageAction(bm, attackLogic, monster, targets);
			}

			return m_LaunchPeriod * (targets.Count - 1) + m_FlightDuration;
		}
	}
#endregion

	enum TargetType
	{
		Enemy,
		Friend,
		DeadFriend,
		FriendNotMe,
		FriendBoss,
	}

	public enum AttackType
	{
		AutoAttack,
		SuperAttack,
		Passive,
		Leader,
		Count,
	}

	[JsonProperty("attackType", DefaultValueHandling = DefaultValueHandling.Populate)]
	[DefaultValue(AttackType.AutoAttack)]
	[JsonConverter(typeof(StringEnumConverter))]
	public AttackType m_AttackType;

	[JsonProperty(JsonConstants.TargetsCount, DefaultValueHandling = DefaultValueHandling.Populate)]
	[DefaultValue(1)]
	int m_TargetsCount;//0 - all

	[JsonProperty("canMultiAttack", DefaultValueHandling = DefaultValueHandling.Populate)]
	[DefaultValue(true)]
	bool m_CanMultiAttack;//If you attack 4 targets and there are only 2 on the field, will you attack twice each of them?

	[JsonProperty("targetType", DefaultValueHandling = DefaultValueHandling.Populate)]
	[JsonConverter(typeof(StringEnumConverter))]
	[DefaultValue(TargetType.Enemy)]
	TargetType m_TargetType;

	//[JsonProperty("targetSelection", DefaultValueHandling = DefaultValueHandling.Populate)]
	//[JsonConverter(typeof(StringEnumConverter))]
	//[DefaultValue(TargetSelection.Random)]
	//TargetSelection m_TargetSelection;

	[JsonProperty("actions")]
	[JsonConverter(typeof(ActionsConverter))]
	List<ActionBase> m_AllActions;

	public List<ActionBase> GetAllActions()
	{
		return m_AllActions;
	}

	[JsonProperty("awake", DefaultValueHandling = DefaultValueHandling.Populate)]
	[DefaultValue(false)]
	public bool m_AwakenedAbility;

	enum TargetFilter
	{
		None,
		Lower,
		Equal,
		Higher,
	}

	[JsonProperty("mobType", DefaultValueHandling = DefaultValueHandling.Populate)]
	[JsonConverter(typeof(StringEnumConverter))]
	[DefaultValue(MonstersManager.MobType.None)]
	MonstersManager.MobType m_FavouriteMobType;

	[JsonProperty("hpFilter", DefaultValueHandling = DefaultValueHandling.Populate)]
	[JsonConverter(typeof(StringEnumConverter))]
	[DefaultValue(TargetFilter.None)]
	TargetFilter m_HPFiler;

	[JsonProperty("lineFilter", DefaultValueHandling = DefaultValueHandling.Populate)]
    [JsonConverter(typeof(StringEnumConverter))]
	[DefaultValue(TargetFilter.Lower)]
    TargetFilter m_LineFilter;

	[JsonProperty("elementFilter", DefaultValueHandling = DefaultValueHandling.Populate)]
	[DefaultValue(true)]
	bool m_ElementFilter;

    [JsonProperty("icon", DefaultValueHandling = DefaultValueHandling.Populate)]
    public string m_Icon;

	[JsonProperty("stunFilter", DefaultValueHandling = DefaultValueHandling.Populate)]
	[DefaultValue(false)]
    bool m_StunFilter;

    [JsonProperty("meleeAttack", DefaultValueHandling = DefaultValueHandling.Populate)]
    [DefaultValue(false)]
    bool m_MeleeAttack;

	[JsonProperty("buffsFilter", DefaultValueHandling = DefaultValueHandling.Populate)]
	[JsonConverter(typeof(StringEnumConverter))]
	[DefaultValue(TargetFilter.None)]
	TargetFilter m_BuffsFilter;

	[JsonProperty("debuffsFilter", DefaultValueHandling = DefaultValueHandling.Populate)]
	[JsonConverter(typeof(StringEnumConverter))]
	[DefaultValue(TargetFilter.None)]
	TargetFilter m_DebuffsFilter;

	[JsonProperty("cd", DefaultValueHandling = DefaultValueHandling.Populate)]
	[DefaultValue(4)]
	public int m_SpellCooldown;

	public void Update(AttackLogic logic)
	{
		m_AttackType = logic.m_AttackType;
		m_TargetsCount = logic.m_TargetsCount;
		m_CanMultiAttack = logic.m_CanMultiAttack;
		m_TargetType = logic.m_TargetType;
		m_FavouriteMobType = logic.m_FavouriteMobType;
		m_HPFiler = logic.m_HPFiler;
		m_ElementFilter = logic.m_ElementFilter;
		m_BuffsFilter = logic.m_BuffsFilter;
		m_DebuffsFilter = logic.m_DebuffsFilter;
		m_AllActions = logic.m_AllActions;
	}

	public void Print()
	{
		Debug.Log("=====");

		Debug.Log("m_AttackType: " + m_AttackType);
		Debug.Log("m_TargetsCount: " + m_TargetsCount);
		Debug.Log("m_CanMultiAttack: " + m_CanMultiAttack);
		Debug.Log("m_TargetType: " + m_TargetType);
		Debug.Log("m_FavouriteMobType: " + m_FavouriteMobType);
		Debug.Log("m_HPFiler: " + m_HPFiler);
		Debug.Log("m_ElementFilter: " + m_ElementFilter);

		if (m_AllActions != null)
		{
			foreach (var action in m_AllActions)
			{
				Debug.Log("+++");
				action.Print();
				Debug.Log("+++");
			}
		}

		Debug.Log("=====");
	}

	public void PlayLogic(MonsterOnTheField monster, BattleManager bm, UnityEngine.Events.UnityAction doneCallback = null)
	{
		if (bm.CanPlayInstantly())
			PlayLogicInstantly(monster, bm, doneCallback);
		else
			AnimationsManager.m_Instance.StartCoroutine(PlayLogicPrivate(monster, bm, doneCallback));
	}

	int CompareMobType(MonstersManager.MobType favourite, MonstersManager.MobType r1, MonstersManager.MobType r2)
	{
		if (r1 == r2)
			return 0;
		if (r1 == favourite)
			return -1;
		if (r2 == favourite)
			return 1;
		return 0;
	}

	int CompareRelation(BasicParams.Relation r1, BasicParams.Relation r2)
	{
		if (r1 == r2)
			return 0;

		switch (r1)
		{
			case BasicParams.Relation.Advantage:
				return -1;
			case BasicParams.Relation.Disadvantage:
				return 1;
			default:
				return r2 == BasicParams.Relation.Advantage ? 1 : -1;
		}
	}

	List<MonsterOnTheField> GetTargets(MonsterOnTheField monster, BattleManager bm, UnityEngine.Events.UnityAction doneCallback)
	{
		int team = (m_TargetType == TargetType.Enemy) ? BattleManager.GetEnemyTeam(monster.m_TeamNumber) : monster.m_TeamNumber;
		List<MonsterOnTheField> targets = new List<MonsterOnTheField>(bm.GetFullTeam(team));//TODO optimize?

		for (int i = targets.Count - 1; i >= 0; i--)
		{
			bool remove = false;
			if (targets[i] == null)
				remove = true;
			else
			{
				switch (m_TargetType)
				{
					case TargetType.DeadFriend:
						remove = !targets[i].IsDead();
						break;
					case TargetType.FriendBoss:
						remove = !targets[i].IsBossType();
						break;
					default:
						remove = !targets[i].CanBeAttacked();
						break;
				}
			}

			if (remove)
				targets.RemoveAt(i);
		}

		if (m_TargetType == TargetType.FriendNotMe)
		{
			if (targets.Count > 1)
			{
				for (int i = 0; i < targets.Count; i++)
				{
					if (targets[i] == monster)
					{
						targets.RemoveAt(i);
						break;
					}
				}
			}
		}

		BuffProvoke provoke = monster.GetProvocation();
		if (provoke != null && provoke.m_Parent.IsActivated())
		{
			targets.Clear();
			targets.Add(provoke.m_Parent);
		}
		else
		{
            //Battle Lines
            //if (m_TargetsCount == 1 && m_TargetType == TargetType.Enemy && targets.Count > 1)
			//if (m_LineFilter == TargetFilter.Lower && m_TargetType == TargetType.Enemy)
            if (m_TargetsCount != 0)
            {
                if (m_LineFilter == TargetFilter.Lower)
    			{
    				int minLine = 999;
    				for (int i = 0; i < targets.Count; i++)
    					minLine = Mathf.Min(minLine, targets[i].m_BattleLine);

    				for (int i = targets.Count - 1; i >= 0; i--)
    				{
    					if (targets[i].m_BattleLine > minLine)
    						targets.RemoveAt(i);
    				}
    			}
			
				for (int i = targets.Count - 1; i >= 0; i--)
				{
					if (targets.Count == 1)
						break;

					if (!targets[i].DoesMakeSenseToAttackHim())
					{
						//Debug.LogError("CANCEL SENSE " + targets[i].m_MyMonster.GetName());
						targets.RemoveAt(i);
					}
				}

				if (m_StunFilter)
				{
					for (int i = targets.Count - 1; i >= 0; i--)
                    {
                        if (targets.Count == 1)
                            break;

						if (targets[i].IsRemovedFromBattle())
						{
							//Debug.LogError("CANCEL STUNNED " + targets[i].m_MyMonster.GetName());
							targets.RemoveAt(i);
						}
                    }
				}
			
    			//if (m_LineFilter == TargetFilter.Higher && m_TargetType == TargetType.Enemy)
                if (m_LineFilter == TargetFilter.Higher)
    			{
    				int maxLine = 0;
    				for (int i = 0; i < targets.Count; i++)
    					maxLine = Mathf.Max(maxLine, targets[i].m_BattleLine);

    				for (int i = targets.Count - 1; i >= 0; i--)
    				{
    					if (targets[i].m_BattleLine < maxLine)
    						targets.RemoveAt(i);
    				}
    			}
            }

			if (m_TargetsCount != 0 && targets.Count != m_TargetsCount)
			{
				for (int i = targets.Count - 1; i >= 0; i--)
				{
					if (!targets[i].CanBeDamaged())
						targets.RemoveAt(i);
				}

				if (targets.Count > m_TargetsCount)
				{
					//Debug.Log("Count = " + targets.Count + " <>can = " + m_TargetsCount);
					if (m_FavouriteMobType != MonstersManager.MobType.None)
					{
						targets.Sort((x, y) => CompareMobType(m_FavouriteMobType, x.m_MyMonster.GetMonsterType(), y.m_MyMonster.GetMonsterType()));

						bool isSame0 = m_FavouriteMobType == targets[0].m_MyMonster.GetMonsterType();

						for (int i = targets.Count - 1; i >= m_TargetsCount; i--)
						{
							bool isSame = m_FavouriteMobType == targets[i].m_MyMonster.GetMonsterType();
							if (isSame0 == isSame)
								break;
							targets.RemoveAt(i);
						}
						//Debug.LogWarning("Applying mob type " + m_FavouriteMobType + " > " + targets.Count);
					}

					if (m_HPFiler != TargetFilter.None)
					{
						if (targets.Count > m_TargetsCount)
						{
							targets.Sort((x, y) => (x.GetHitpointsRatio()).CompareTo(y.GetHitpointsRatio()));
							switch (m_HPFiler)
							{
								case TargetFilter.Lower:
									targets.RemoveRange(m_TargetsCount, targets.Count - m_TargetsCount);
									break;
								case TargetFilter.Higher:
									targets.RemoveRange(0, targets.Count - m_TargetsCount);
									break;
							}
						}
						//Debug.LogWarning("Applying HP " + m_HPFiler + " > " + targets.Count);
					}

					if (m_BuffsFilter != TargetFilter.None)
					{
						if (targets.Count > m_TargetsCount)
						{
							targets.Sort((x, y) => (x.GetPositiveEffectsCount()).CompareTo(y.GetPositiveEffectsCount()));
							switch (m_BuffsFilter)
							{
								case TargetFilter.Lower:
									targets.RemoveRange(m_TargetsCount, targets.Count - m_TargetsCount);
									break;
								case TargetFilter.Higher:
									targets.RemoveRange(0, targets.Count - m_TargetsCount);
									break;
							}
						}
					}

					if (m_DebuffsFilter != TargetFilter.None)
					{
						if (targets.Count > m_TargetsCount)
						{
							targets.Sort((x, y) => (x.GetNegativeEffectsCount()).CompareTo(y.GetNegativeEffectsCount()));
							switch (m_DebuffsFilter)
							{
								case TargetFilter.Lower:
									targets.RemoveRange(m_TargetsCount, targets.Count - m_TargetsCount);
									break;
								case TargetFilter.Higher:
									targets.RemoveRange(0, targets.Count - m_TargetsCount);
									break;
							}
						}
					}

					if (m_ElementFilter)
					{
						if (targets.Count > m_TargetsCount)
						{
							targets.Sort((x, y) => CompareRelation(BasicParams.GetRelation(monster.m_MyMonster, x.m_MyMonster), BasicParams.GetRelation(monster.m_MyMonster, y.m_MyMonster)));

							BasicParams.Relation relation0 = BasicParams.GetRelation(monster.m_MyMonster, targets[0].m_MyMonster);

							for (int i = targets.Count - 1; i >= m_TargetsCount; i--)
							{
								BasicParams.Relation r = BasicParams.GetRelation(monster.m_MyMonster, targets[i].m_MyMonster);
								if (r == relation0)
									break;
								targets.RemoveAt(i);
							}
							//Debug.LogWarning("Applying Relation " + monster.m_MyMonster.GetElementType() + " > " + targets.Count);
						}
					}

					while (targets.Count > m_TargetsCount)
					{
						int rnd = Random.Range(0, targets.Count);
						targets.RemoveAt(rnd);
					}
				}
			}

			if (targets.Count != 0)
			{
				if (m_CanMultiAttack)
				{
					int n = 0;
					while (targets.Count < m_TargetsCount)
						targets.Add(targets[n++]);
				}
			}
		}

		return targets;
	}

	public int m_AttackIndex;
	static private int AttackCount = 0;

	public void PlayLogicInstantly(MonsterOnTheField monster, BattleManager bm, UnityEngine.Events.UnityAction doneCallback = null)
	{
        if (m_MeleeAttack && bm.IsAnyOneInFrontOfMe(monster))
        {
            monster.AttackWasCancelled(doneCallback);
            return;
        }
		m_AttackIndex = ++AttackCount;
		List<MonsterOnTheField> targets = GetTargets(monster, bm, doneCallback);

		if (targets.Count != 0)
		{
			for (int i = 0; i < m_AllActions.Count; i++)
				m_AllActions[i].PerformAction(bm, this, monster, targets);
		}
		else
		{
#if !NO_LOGS
			Debug.LogWarning("NO targets were found: " + monster.m_TeamNumber + " <> " + m_TargetType);
#endif
		}
		if (doneCallback != null)
			doneCallback();
	}

	public bool CanPlayCameraAnimations()
	{
		return m_AttackType == AttackType.SuperAttack;
	}

	IEnumerator PlayLogicPrivate(MonsterOnTheField monster, BattleManager bm, UnityEngine.Events.UnityAction doneCallback)
	{
        if (m_MeleeAttack && bm.IsAnyOneInFrontOfMe(monster))
        {
            monster.AttackWasCancelled(doneCallback);
            yield break;
        }
		m_AttackIndex = ++AttackCount;
		List<MonsterOnTheField> targets = GetTargets(monster, bm, doneCallback);

		if (targets.Count != 0)
		{
			Vector3 forward;
			if (monster.CanPlayAnimations() && m_TargetType == TargetType.Enemy && monster.m_Animation.CanLookAtTargets())
			{
				if (m_TargetsCount == 1)
					forward = targets[0].GetInitialPosition();
				else
					forward = bm.GetTeamCastPoint(targets[0].m_TeamNumber);
				Vector3 pos = monster.GetInitialPosition();
				pos = pos + PrefabManager.GetLookVectorProjection(forward - pos, monster.m_Animation.m_Transform.up);
				monster.m_Animation.m_Transform.LookAt(pos, monster.m_Animation.m_Transform.up);
			}

			if (CanPlayCameraAnimations())
				CameraTracker.m_Instance.PlayAnimation(monster, monster, CameraTracker.CameraAnimation.Zoom);

			//Debug.LogWarning("Perform " + m_Name + " > " + targets.Count + " > " + m_AllActions.Count);
			float timeScale = bm.GetTimeScale();
			float delay = 0;
			float lastWaitTime = 0;
			for (int i = 0; i < m_AllActions.Count; i++)
			{
				ActionBase act = m_AllActions[i];
				//if (!monster.CanMakeActions())
				float dt = act.m_Delay - delay;
				//Debug.Log("Wait for: " + dt + " . " + Time.realtimeSinceStartup);
				float wt = 0;
				if (dt > 0.001f)
				{
					wt = dt / timeScale;
					if (CheckTime(wt))
						yield return new WaitForSeconds(wt);
					delay += dt;
				}
				//Debug.Log("PerformAction " + act.GetType() + " > " + lastWaitTime + " . " + Time.realtimeSinceStartup);
				lastWaitTime -= wt;
				lastWaitTime = Math.Max(act.PerformAction(bm, this, monster, targets), lastWaitTime);
			}

			if (CheckTime(lastWaitTime))
				yield return new WaitForSeconds(lastWaitTime);

			for (int i = 0; i < m_AllActions.Count; i++)
			{
				if (m_AllActions[i].m_Reverse)
					m_AllActions[i].PerformReverseAction(bm, monster, targets);
			}

			//if (monster.HasVisualAssets())
			//monster.m_Animation.m_Transform.localRotation = Quaternion.identity;
		}
		else
			Debug.LogWarning("NO targets were found: " + monster.m_TeamNumber + " <> " + m_TargetType);
		//Debug.LogError("DONE " + m_Name);
		if (doneCallback != null)
			doneCallback();
		yield break;
	}

	static bool CheckTime(float time)
	{
		return time > GameManager.m_LastDeltaTime;
	}

	public string LocalizeDescription(MyMonster monster)
	{
		string desc = string.Empty;
		for (int i = 0; i < m_AllActions.Count; i++)
		{
			if (!m_AllActions[i].m_Reverse)
			{
				string s = m_AllActions[i].LocalizeDescription(this, monster);
#if DESC_LOGS
                Debug.Log("s = " + m_AllActions[i].GetType() + " > " + s);
#endif
				if (s != null)
					desc += s + PrefabManager.String_Dot;
			}
		}

		for (int i = 0; i < m_AllActions.Count; i++)
		{
			if (m_AllActions[i].m_Reverse)
			{
				string s = m_AllActions[i].LocalizeDescription(this, monster);
#if DESC_LOGS
                Debug.Log("2s = " + m_AllActions[i].GetType() + " > " + s);
#endif
                if (s != null)
                    desc += PrefabManager.String_Dash + s + PrefabManager.String_Dot;
            }
        }

        if (m_TargetType == TargetType.Enemy && m_LineFilter != TargetFilter.Lower) {
            desc += PrefabManager.String_Dash + LocalizeHelper.LocalizeString("lineFilter_" + m_LineFilter.ToString());
        }
        return desc;
    }
}
