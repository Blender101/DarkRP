using Sandbox.UI;

/// <summary>
/// Trigger volume placed at a checkpoint that plays a short neutral beep
/// when a non-patrol player walks through. Pure audio cue for the patrol -
/// no alarms, no door automation, no visa logic.
/// </summary>
public sealed class ScannerBeep : Component, Component.ITriggerListener
{
	[Property]
	public SoundEvent BeepSound { get; set; }

	[Property, Range( 0.1f, 30f )]
	public float CooldownSeconds { get; set; } = 0.5f;

	readonly Dictionary<Guid, RealTimeSince> _recentlyBeeped = new();

	void ITriggerListener.OnTriggerEnter( Collider other )
	{
		if ( !Networking.IsHost )
			return;

		var go = other.GameObject?.Root;
		if ( !go.IsValid() )
			return;

		var player = go.GetComponent<Player>();
		if ( !player.IsValid() )
			return;

		// Patrol shouldn't beep their own scanner walking in/out of the booth.
		if ( player.IsBorderPatrol )
			return;

		if ( IsRecentlyBeeped( player ) )
			return;

		MarkBeeped( player );
		PlayBeep();
	}

	[Rpc.Broadcast]
	void PlayBeep()
	{
		if ( Application.IsDedicatedServer || BeepSound is null )
			return;

		GameObject.PlaySound( BeepSound );
	}

	bool IsRecentlyBeeped( Player player )
	{
		if ( !_recentlyBeeped.TryGetValue( player.Id, out var last ) )
			return false;

		return last < CooldownSeconds;
	}

	void MarkBeeped( Player player )
	{
		_recentlyBeeped[player.Id] = 0.0f;

		if ( _recentlyBeeped.Count <= 32 )
			return;

		foreach ( var (id, time) in _recentlyBeeped.ToArray() )
		{
			if ( time > CooldownSeconds * 2.0f )
				_recentlyBeeped.Remove( id );
		}
	}
}
