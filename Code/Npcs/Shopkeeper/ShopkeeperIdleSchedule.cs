using Sandbox.Npcs.Shopkeeper;
using Sandbox.Npcs.Tasks;

namespace Sandbox.Npcs.Schedules;

/// <summary>
/// Idle behaviour for a stationary shopkeeper.
/// Tracks the nearest visible player with the head/eyes, otherwise glances
/// loosely around their stall. Occasionally mutters a barker line.
/// </summary>
public class ShopkeeperIdleSchedule : ScheduleBase
{
	protected override void OnStart()
	{
		var shopkeeper = Npc as ShopkeeperNpc;
		var nearest = FindNearestPlayer( shopkeeper?.TrackingRange ?? 400f );

		if ( nearest.IsValid() )
		{
			AddTask( new LookAt( nearest ) );
		}
		else
		{
			var forward = GameObject.WorldRotation.Forward.WithZ( 0 ).Normal;
			var yawOffset = Game.Random.Float( -45f, 45f );
			var lookDir = Rotation.FromAxis( Vector3.Up, yawOffset ) * forward;
			AddTask( new LookAt( GameObject.WorldPosition + lookDir * 100f ) );
		}

		TryAddBarker( shopkeeper );

		AddTask( new Wait( Game.Random.Float( 2f, 4f ) ) );
	}

	private void TryAddBarker( ShopkeeperNpc shopkeeper )
	{
		if ( shopkeeper?.BarkerLines is not { Count: > 0 } lines )
			return;

		var speech = Npc.Speech;
		if ( speech is null || !speech.CanSpeak )
			return;

		if ( Game.Random.Float() > 0.2f )
			return;

		var line = lines[Game.Random.Int( 0, lines.Count - 1 )];
		if ( string.IsNullOrWhiteSpace( line ) )
			return;

		AddTask( new Say( line, 2.5f ) );
	}

	private GameObject FindNearestPlayer( float range )
	{
		var senses = Npc.Senses;
		if ( senses is null )
			return null;

		GameObject best = null;
		float bestDist = range;

		foreach ( var player in senses.VisibleTargets )
		{
			if ( !player.IsValid() ) continue;

			var dist = Npc.WorldPosition.Distance( player.WorldPosition );
			if ( dist < bestDist )
			{
				bestDist = dist;
				best = player;
			}
		}

		return best;
	}
}
