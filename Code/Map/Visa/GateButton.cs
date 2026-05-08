using Sandbox.UI;

/// <summary>
/// Government-only button that toggles a border gate <see cref="RoleplayDoor"/>.
/// Mirrors the shop interaction pattern: hover for tooltip, press E to use.
/// </summary>
public sealed class GateButton : Component, Component.IPressable
{
	[Property]
	public RoleplayDoor TargetDoor { get; set; }

	[Property, Range( 0.1f, 30f )]
	public float CooldownSeconds { get; set; } = 1.0f;

	[Sync( SyncFlags.FromHost )]
	TimeUntil Cooldown { get; set; }

	IPressable.Tooltip? IPressable.GetTooltip( IPressable.Event e )
	{
		var player = GetPlayer( e.Source.GameObject );
		if ( !player.IsValid() )
			return null;

		if ( !player.IsBorderPatrol )
			return new IPressable.Tooltip( "Authorized Personnel Only", "block", "Border Patrol controls this gate." );

		var label = TargetDoor.IsValid() && TargetDoor.IsDoorOpen() ? "Close Gate" : "Open Gate";
		return new IPressable.Tooltip( label, "door_front", "Toggle the border gate." );
	}

	bool IPressable.CanPress( IPressable.Event e )
	{
		var player = GetPlayer( e.Source.GameObject );
		return player.IsValid() && player.IsBorderPatrol && TargetDoor.IsValid() && Cooldown <= 0.0f;
	}

	bool IPressable.Press( IPressable.Event e )
	{
		ToggleGate( e.Source.GameObject );
		return true;
	}

	[Rpc.Host]
	void ToggleGate( GameObject pressorObject )
	{
		var player = GetPlayer( pressorObject );
		if ( !player.IsValid() || Rpc.Caller != player.Network.Owner )
			return;

		if ( !player.IsBorderPatrol || !TargetDoor.IsValid() || Cooldown > 0.0f )
			return;

		Cooldown = CooldownSeconds;

		if ( TargetDoor.IsDoorOpen() )
			TargetDoor.TryClose( pressorObject );
		else
			TargetDoor.TryOpen( pressorObject );
	}

	static Player GetPlayer( GameObject source )
	{
		return source?.Root.GetComponent<Player>();
	}
}
