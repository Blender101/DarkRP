using Sandbox.UI;

/// <summary>
/// Border Patrol checkpoint terminal. Functionally a fancy <see cref="GateButton"/>:
/// press E to toggle the assigned border gate <see cref="RoleplayDoor"/>, but with
/// a worldspace screen (<c>BorderTerminalScreen</c>) that mirrors the gate state.
///
/// All state mutations run on the host; the screen reads the synced fields each frame
/// so every client sees the same readout.
///
/// <para>
/// When <see cref="UseNearestGovernmentDoor"/> is enabled (default on the prefab),
/// the terminal ignores the serialized <see cref="TargetDoor"/> reference and instead
/// binds to the closest <see cref="RoleplayDoor"/> with <see cref="RoleplayDoor.IsGovernment"/>
/// within <see cref="NearestDoorSearchRadius"/>. That avoids duplicated prefab instances
/// all pointing at the same door by mistake.
/// </para>
/// </summary>
public sealed class BorderGateTerminal : Component, Component.IPressable
{
	[Property]
	public RoleplayDoor TargetDoor { get; set; }

	/// <summary>
	/// When true, the closest government <see cref="RoleplayDoor"/> within
	/// <see cref="NearestDoorSearchRadius"/> is used instead of <see cref="TargetDoor"/>.
	/// </summary>
	[Property, Group( "Door link" )]
	public bool UseNearestGovernmentDoor { get; set; } = true;

	[Property, Group( "Door link" ), Range( 128f, 20000f )]
	public float NearestDoorSearchRadius { get; set; } = 4000f;

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

	/// <summary>
	/// Encoded ring buffer of the last <see cref="MaxLogEntries"/> events.
	/// Entries are joined with '\n', each entry is "secondsAgo|actor|event".
	/// Decoded on the screen via <see cref="GetLogEntries"/>.
	/// </summary>
	[Sync( SyncFlags.FromHost )]
	public string LogEntries { get; set; } = "";

	public const int MaxLogEntries = 10;

	/// <summary>
	/// Finds the closest enabled terminal in the scene (used for patrol audit lines).
	/// </summary>
	public static BorderGateTerminal FindNearest( Vector3 origin, float maxDistance )
	{
		if ( Game.ActiveScene is null )
			return null;

		var maxSq = maxDistance * maxDistance;
		BorderGateTerminal best = null;
		var bestDistSq = float.MaxValue;

		foreach ( var terminal in Game.ActiveScene.GetAllComponents<BorderGateTerminal>() )
		{
			if ( !terminal.IsValid() || !terminal.Enabled )
				continue;

			var dSq = Vector3.DistanceBetweenSquared( origin, terminal.WorldPosition );
			if ( dSq > maxSq )
				continue;

			if ( dSq < bestDistSq )
			{
				bestDistSq = dSq;
				best = terminal;
			}
		}

		return best;
	}

	RoleplayDoor _linkedDoor;

	protected override void OnStart()
	{
		RefreshLinkedDoor();
	}

	void RefreshLinkedDoor()
	{
		if ( !UseNearestGovernmentDoor )
		{
			_linkedDoor = null;
			return;
		}

		_linkedDoor = FindNearestGovernmentDoor();
	}

	RoleplayDoor EffectiveDoor => UseNearestGovernmentDoor ? _linkedDoor : TargetDoor;

	public bool IsOnline => EffectiveDoor.IsValid();
	public bool IsGateOpen => IsOnline && EffectiveDoor.IsDoorOpen();
	public bool IsCoolingDown => Cooldown > 0f;

	RoleplayDoor FindNearestGovernmentDoor()
	{
		var origin = WorldPosition;
		var radiusSq = NearestDoorSearchRadius * NearestDoorSearchRadius;
		RoleplayDoor best = null;
		var bestDistSq = float.MaxValue;

		foreach ( var door in Scene.GetAllComponents<RoleplayDoor>() )
		{
			if ( !door.IsValid() || !door.IsGovernment )
				continue;

			var dSq = Vector3.DistanceBetweenSquared( origin, door.WorldPosition );
			if ( dSq > radiusSq )
				continue;

			if ( dSq < bestDistSq )
			{
				bestDistSq = dSq;
				best = door;
			}
		}

		return best;
	}

	IPressable.Tooltip? IPressable.GetTooltip( IPressable.Event e )
	{
		// The terminal's worldspace screen already shows gate status and the
		// [ PRESS E ] prompt, so don't double up by drawing the central HUD tooltip.
		return null;
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

		var door = EffectiveDoor;
		if ( !player.IsBorderPatrol || !door.IsValid() || IsCoolingDown )
			return;

		Cooldown = CooldownSeconds;
		LastActor = player.DisplayName;
		TimeSinceLastAction = 0f;

		if ( door.IsDoorOpen() )
		{
			door.TryClose( pressorObject );
			LastActionLabel = "GATE CLOSED";
			AppendLog( player.DisplayName, "CLOSED GATE" );
		}
		else
		{
			door.TryOpen( pressorObject );
			LastActionLabel = "GATE OPENED";
			AppendLog( player.DisplayName, "OPENED GATE" );
		}

		PlayActivation();
	}

	/// <summary>
	/// Host-side: append a new line to the audit log ring buffer.
	/// </summary>
	public void AppendLog( string actor, string action )
	{
		if ( !Networking.IsHost )
			return;

		actor = string.IsNullOrWhiteSpace( actor ) ? "Unknown" : SanitizeField( actor );
		action = SanitizeField( action );

		var lines = string.IsNullOrEmpty( LogEntries )
			? new List<string>()
			: LogEntries.Split( '\n', StringSplitOptions.RemoveEmptyEntries ).ToList();

		var now = (int)Math.Round( (float)Time.Now );
		lines.Insert( 0, $"{now}|{actor}|{action}" );

		while ( lines.Count > MaxLogEntries )
			lines.RemoveAt( lines.Count - 1 );

		LogEntries = string.Join( "\n", lines );
	}

	static string SanitizeField( string value )
	{
		if ( string.IsNullOrEmpty( value ) )
			return "";
		return value.Replace( '|', '/' ).Replace( '\n', ' ' );
	}

	public readonly record struct LogEntry( float SecondsAgo, string Actor, string Event );

	/// <summary>
	/// Decodes the synced <see cref="LogEntries"/> string into typed entries
	/// in newest-first order. Safe to call on the client every frame.
	/// </summary>
	public IEnumerable<LogEntry> GetLogEntries()
	{
		if ( string.IsNullOrEmpty( LogEntries ) )
			yield break;

		foreach ( var line in LogEntries.Split( '\n', StringSplitOptions.RemoveEmptyEntries ) )
		{
			var parts = line.Split( '|', 3 );
			if ( parts.Length < 3 )
				continue;

			if ( !float.TryParse( parts[0], out var stamp ) )
				continue;

			yield return new LogEntry( Time.Now - stamp, parts[1], parts[2] );
		}
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
