using Sandbox.UI;

/// <summary>
/// Border Patrol checkpoint terminal. Functionally a fancy <see cref="GateButton"/>:
/// press E to toggle the assigned border gate <see cref="RoleplayDoor"/>, but with
/// a worldspace screen (<c>BorderTerminalScreen</c>) that mirrors the gate state.
///
/// All state mutations run on the host; the screen reads the synced fields each frame
/// so every client sees the same readout.
/// </summary>
public sealed class BorderGateTerminal : Component, Component.IPressable
{
	[Property]
	public RoleplayDoor TargetDoor { get; set; }

	/// <summary>Title printed at the top of the terminal screen.</summary>
	[Property]
	public string TerminalLabel { get; set; } = "BORDER CHECKPOINT";

	[Property, Range( 0.1f, 30f )]
	public float CooldownSeconds { get; set; } = 1.5f;

	/// <summary>Optional buzz/beep played when the gate is toggled.</summary>
	[Property]
	public SoundEvent ActivationSound { get; set; }

	[Sync( SyncFlags.FromHost )]
	public TimeUntil Cooldown { get; set; }

	/// <summary>Last action banner shown on the screen (e.g. "GATE OPENED").</summary>
	[Sync( SyncFlags.FromHost )]
	public string LastActionLabel { get; set; } = "STANDBY";

	/// <summary>Name of the player who last toggled the gate, for the audit line.</summary>
	[Sync( SyncFlags.FromHost )]
	public string LastActor { get; set; } = "---";

	[Sync( SyncFlags.FromHost )]
	public TimeSince TimeSinceLastAction { get; set; }

	public bool IsOnline => TargetDoor.IsValid();
	public bool IsGateOpen => IsOnline && TargetDoor.IsDoorOpen();
	public bool IsCoolingDown => Cooldown > 0f;

	IPressable.Tooltip? IPressable.GetTooltip( IPressable.Event e )
	{
		var player = GetPlayer( e.Source.GameObject );
		if ( !player.IsValid() )
			return null;

		if ( !player.IsBorderPatrol )
			return new IPressable.Tooltip( "Authorized Personnel Only", "block", "Border Patrol only." );

		if ( !IsOnline )
			return new IPressable.Tooltip( "Terminal Offline", "block", "No gate is bound to this terminal." );

		var label = IsGateOpen ? "Close Gate" : "Open Gate";
		return new IPressable.Tooltip( label, "door_front", $"Toggle {TerminalLabel}." );
	}

	bool IPressable.CanPress( IPressable.Event e )
	{
		var player = GetPlayer( e.Source.GameObject );
		return player.IsValid() && player.IsBorderPatrol && IsOnline && !IsCoolingDown;
	}

	bool IPressable.Press( IPressable.Event e )
	{
		RequestToggle( e.Source.GameObject );
		return true;
	}

	[Rpc.Host]
	void RequestToggle( GameObject pressorObject )
	{
		var player = GetPlayer( pressorObject );
		if ( !player.IsValid() || Rpc.Caller != player.Network.Owner )
			return;

		if ( !player.IsBorderPatrol || !IsOnline || IsCoolingDown )
			return;

		Cooldown = CooldownSeconds;
		LastActor = player.DisplayName;
		TimeSinceLastAction = 0f;

		if ( IsGateOpen )
		{
			TargetDoor.TryClose( pressorObject );
			LastActionLabel = "GATE CLOSED";
		}
		else
		{
			TargetDoor.TryOpen( pressorObject );
			LastActionLabel = "GATE OPENED";
		}

		PlayActivation();
	}

	[Rpc.Broadcast]
	void PlayActivation()
	{
		if ( Application.IsDedicatedServer )
			return;

		if ( ActivationSound is not null )
			Sound.Play( ActivationSound, WorldPosition );
	}

	static Player GetPlayer( GameObject source )
	{
		return source?.Root.GetComponent<Player>();
	}
}
