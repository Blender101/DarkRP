using Sandbox.Rendering;

/// <summary>
/// Border Patrol ID scanner. While carrying it:
///   Primary attack (left click)  - check the aimed-at player's visa
///   Secondary attack (right click) - frisk them for contraband
///
/// All checks run on the host through <see cref="Player.CheckVisaOnAimedPlayer"/>
/// and <see cref="Player.FriskAimedPlayer"/>; results are pushed back to the
/// patrol via Notice popups (icon + label, ~5s) and shown on a small
/// diegetic <see cref="Sandbox.WorldPanel"/> attached to the gun's screen
/// (PASS / DENY).
///
/// Tool refuses to fire for non-patrol players, so it's safe to drop or trade.
/// </summary>
public sealed class BorderPatrolIdScannerTool : BaseCarryable
{
	public enum ScannerScreenState
	{
		Idle = 0,
		Pass = 1,
		Deny = 2
	}

	[Property, Range( 0.1f, 5f )]
	public float ScanCooldown { get; set; } = 0.75f;

	[Property]
	public SoundEvent ScanSound { get; set; }

	[Property, Range( 0.5f, 10f ), Category( "Screen" )]
	public float ScreenHoldSeconds { get; set; } = 3f;

	/// <summary>
	/// Local position of the diegetic screen relative to the held gun's
	/// world model (parented under the hold bone). Tweak in the inspector
	/// to line up with the toolgun's 4-square display.
	/// </summary>
	[Property, Category( "Screen" )]
	public Vector3 ScreenLocalPosition { get; set; } = new Vector3( 1.5f, 0f, 6f );

	[Property, Category( "Screen" )]
	public Angles ScreenLocalAngles { get; set; } = new Angles( 0f, 0f, 0f );

	[Property, Category( "Screen" )]
	public Vector2 ScreenPanelSize { get; set; } = new Vector2( 360f, 120f );

	[Property, Range( 0.005f, 0.2f ), Category( "Screen" )]
	public float ScreenRenderScale { get; set; } = 0.025f;

	[Sync( SyncFlags.FromHost )]
	public ScannerScreenState ScreenState { get; private set; }

	[Sync( SyncFlags.FromHost )]
	public TimeUntil ScreenClearTime { get; private set; }

	TimeUntil _localCooldown;
	TimeUntil _serverCooldown;

	GameObject _screenAnchor;

	bool CanScan => _localCooldown <= 0.0f;

	public override void OnControl( Player player )
	{
		base.OnControl( player );

		if ( !player.IsValid() || !player.GameObject.IsValid() )
			return;

		if ( !CanScan )
			return;

		if ( Input.Pressed( "attack1" ) )
		{
			_localCooldown = ScanCooldown;
			RequestCheckVisa();
			return;
		}

		if ( Input.Pressed( "attack2" ) )
		{
			_localCooldown = ScanCooldown;
			RequestFrisk();
		}
	}

	[Rpc.Host]
	void RequestCheckVisa()
	{
		var player = Owner;
		if ( !player.IsValid() || Rpc.Caller != player.Network.Owner )
			return;

		if ( !player.IsBorderPatrol )
			return;

		if ( _serverCooldown > 0.0f )
			return;

		_serverCooldown = ScanCooldown;
		var result = player.CheckVisaOnAimedPlayer();
		SetScreenState( result switch
		{
			true => ScannerScreenState.Pass,
			false => ScannerScreenState.Deny,
			_ => ScannerScreenState.Idle
		} );
		PlayScanEffects();
	}

	[Rpc.Host]
	void RequestFrisk()
	{
		var player = Owner;
		if ( !player.IsValid() || Rpc.Caller != player.Network.Owner )
			return;

		if ( !player.IsBorderPatrol )
			return;

		if ( _serverCooldown > 0.0f )
			return;

		_serverCooldown = ScanCooldown;
		player.FriskAimedPlayer();
		PlayScanEffects();
	}

	void SetScreenState( ScannerScreenState state )
	{
		if ( !Networking.IsHost )
			return;

		ScreenState = state;
		ScreenClearTime = state == ScannerScreenState.Idle ? 0f : ScreenHoldSeconds;
	}

	protected override void OnUpdate()
	{
		base.OnUpdate();

		EnsureScreenPanel();

		if ( Networking.IsHost && ScreenState != ScannerScreenState.Idle && ScreenClearTime <= 0f )
		{
			ScreenState = ScannerScreenState.Idle;
		}
	}

	protected override void OnDisabled()
	{
		base.OnDisabled();
		DestroyScreenPanel();
	}

	void EnsureScreenPanel()
	{
		var model = WorldModel;
		if ( !model.IsValid() )
		{
			DestroyScreenPanel();
			return;
		}

		if ( _screenAnchor.IsValid() && _screenAnchor.Parent == model )
			return;

		DestroyScreenPanel();

		_screenAnchor = new GameObject( true, "ScannerScreen" );
		_screenAnchor.SetParent( model, false );
		_screenAnchor.LocalPosition = ScreenLocalPosition;
		_screenAnchor.LocalRotation = ScreenLocalAngles;
		_screenAnchor.Flags |= GameObjectFlags.NotSaved | GameObjectFlags.NotNetworked;

		var worldPanel = _screenAnchor.AddComponent<WorldPanel>();
		worldPanel.PanelSize = ScreenPanelSize;
		worldPanel.LookAtCamera = false;
		worldPanel.RenderScale = ScreenRenderScale;

		var screen = _screenAnchor.AddComponent<IdScannerScreen>();
		screen.Tool = this;
	}

	void DestroyScreenPanel()
	{
		if ( !_screenAnchor.IsValid() )
			return;

		_screenAnchor.Destroy();
		_screenAnchor = null;
	}

	[Rpc.Broadcast]
	void PlayScanEffects()
	{
		if ( Application.IsDedicatedServer )
			return;

		if ( ScanSound is not null )
			GameObject.PlaySound( ScanSound );

		if ( ViewModel.IsValid() )
			ViewModel.RunEvent<ViewModel>( x => x.OnAttack() );
		else if ( WorldModel.IsValid() )
			WorldModel.RunEvent<WorldModel>( x => x.OnAttack() );
	}

	public override void DrawHud( HudPainter painter, Vector2 crosshair )
	{
		var color = CanScan ? new Color( 0.45f, 0.85f, 1f ) : new Color( 1f, 0.4f, 0.3f );

		painter.SetBlendMode( BlendMode.Lighten );
		painter.DrawCircle( crosshair, 6f, color );
		painter.DrawCircle( crosshair, 3f, color );
	}
}
