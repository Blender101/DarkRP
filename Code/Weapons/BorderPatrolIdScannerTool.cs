using Sandbox.Rendering;

/// <summary>
/// Border Patrol ID scanner. While carrying it:
///   Primary attack (left click)  - check the aimed-at player's visa
///   Secondary attack (right click) - frisk them for contraband
///
/// All checks run on the host through <see cref="Player.CheckVisaOnAimedPlayer"/>
/// and <see cref="Player.FriskAimedPlayer"/>; results are pushed back to the
/// patrol via Notice popups (icon + label, ~5s).
///
/// Tool refuses to fire for non-patrol players, so it's safe to drop or trade.
/// </summary>
public sealed class BorderPatrolIdScannerTool : BaseCarryable
{
	[Property, Range( 0.1f, 5f )]
	public float ScanCooldown { get; set; } = 0.75f;

	[Property]
	public SoundEvent ScanSound { get; set; }

	TimeUntil _localCooldown;
	TimeUntil _serverCooldown;

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
		player.CheckVisaOnAimedPlayer();
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
