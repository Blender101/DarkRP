using Sandbox.Npcs.Schedules;

namespace Sandbox.Npcs.Shopkeeper;

/// <summary>
/// Stationary shopkeeper NPC. Stands in place, idle-animates,
/// turns to face the nearest nearby player, and occasionally mutters a barker line.
///
/// The NPC itself only handles "being a person" - the actual shop interaction
/// (DrugShop, DrugBuyer, VisaDealer, etc.) lives on a separate IPressable component
/// on the same GameObject so pressing E on the body still works.
/// </summary>
public class ShopkeeperNpc : Npc, Component.IDamageable
{
	[Property, ClientEditable, Range( 1, 1000 ), Sync]
	public float Health { get; set; } = 100f;

	/// <summary>
	/// When true the NPC can't be killed - useful for important shopkeepers
	/// that should always be available to players.
	/// </summary>
	[Property]
	public bool Invincible { get; set; } = true;

	/// <summary>
	/// How far away a player can be and still be tracked by the shopkeeper.
	/// Anything beyond this and the NPC looks roughly forward.
	/// </summary>
	[Property, Range( 64, 2048 )]
	public float TrackingRange { get; set; } = 400f;

	/// <summary>
	/// Optional lines the shopkeeper occasionally mutters while idling.
	/// </summary>
	[Property]
	public List<string> BarkerLines { get; set; } = new();

	public override ScheduleBase GetSchedule()
	{
		return GetSchedule<ShopkeeperIdleSchedule>();
	}

	void IDamageable.OnDamage( in DamageInfo damage )
	{
		if ( IsProxy || Invincible )
			return;

		Health -= damage.Damage;
		if ( Health < 1 )
		{
			Die( damage );
		}
	}
}
