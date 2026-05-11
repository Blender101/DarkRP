using Sandbox.Rendering;
using Sandbox.Utility;

/// <summary>
/// Shared base for the Border Patrol diegetic-screen tools (ID Scanner, Frisk Tool).
/// Uses the toolgun's viewmodel screen render target to draw a cool action UI:
///   Idle    - flashing ready indicator + title
///   Working - horizontal loading bar, TV-style noise, percentage; host runs the check
///   Pass    - green success state with custom label
///   Fail    - red failure state with custom label
///
/// Each subclass only needs to provide a title, a few labels, and the server-side
/// action that returns <c>bool?</c> (true=pass, false=fail, null=no-op).
/// All checks are still routed through <see cref="Player"/> on the host so the
/// client cannot spoof a result.
/// </summary>
public abstract class BorderPatrolScreenWeapon : ScreenWeapon
{
	public enum ScreenState
	{
		Idle = 0,
		Working = 1,
		Pass = 2,
		Fail = 3
	}

	[Property, Range( 0.1f, 5f ), Category( "Action" )]
	public float ActionCooldown { get; set; } = 1.2f;

	/// <summary>
	/// Duration of the loading animation on the screen before the result snaps in.
	/// </summary>
	[Property, Range( 0.1f, 3f ), Category( "Screen" )]
	public float WorkingDuration { get; set; } = 0.6f;

	/// <summary>
	/// How long the pass / fail result lingers on the screen before returning to idle.
	/// </summary>
	[Property, Range( 0.5f, 10f ), Category( "Screen" )]
	public float ResultHoldDuration { get; set; } = 2f;

	[Property, Category( "Action" )]
	public SoundEvent ActionSound { get; set; }

	TimeUntil _localCooldown;
	TimeUntil _serverCooldown;
	TimeUntil _localWorkingUntil;
	TimeUntil _localResultUntil;
	ScreenState _localResult;
	ScreenState _pendingResult;
	bool _hasPendingResult;

	bool CanAct => _localCooldown <= 0f && _localWorkingUntil <= 0f;

	/// <summary>
	/// Short label shown on the screen at idle (e.g. "SCAN", "FRISK").
	/// Matches the toolgun's "🥽 Weld" style - kept short so it fits centered.
	/// </summary>
	protected abstract string ScreenTitle { get; }

	/// <summary>
	/// Emoji prefix shown next to the title, like the toolgun's mode icons.
	/// </summary>
	protected virtual string ScreenIcon => "";

	/// <summary>
	/// Label shown during the loading animation (e.g. "SCANNING").
	/// </summary>
	protected virtual string WorkingLabel => "WORKING";

	protected virtual string PassLabel => "PASS";

	protected virtual string FailLabel => "DENY";

	/// <summary>
	/// Crosshair color when the tool is ready to fire.
	/// </summary>
	protected virtual Color ReadyColor => new( 0.45f, 0.85f, 1f );

	/// <summary>
	/// Server-authoritative action invoked from the host. Returns:
	///   true  - success / pass result
	///   false - failure / fail result
	///   null  - no-op (no target, etc.)
	/// </summary>
	protected abstract bool? PerformServerAction( Player player );

	public override void OnControl( Player player )
	{
		base.OnControl( player );

		if ( !player.IsValid() || !player.GameObject.IsValid() )
		{
			UpdateViewmodelScreen();
			ApplyCoilSpin();
			return;
		}

		if ( CanAct && Input.Pressed( "attack1" ) )
		{
			_localCooldown = ActionCooldown;
			_localWorkingUntil = WorkingDuration;
			_localResult = ScreenState.Idle;
			_localResultUntil = 0f;
			_hasPendingResult = false;
			_pendingResult = ScreenState.Idle;

			RequestAction();
		}

		// Same as <see cref="Toolgun"/> / <see cref="Physgun"/>: without this the
		// viewmodel screen render target is never repainted and stays blank.
		UpdateViewmodelScreen();
		ApplyCoilSpin();
	}

	[Rpc.Host]
	void RequestAction()
	{
		var player = Owner;
		if ( !player.IsValid() || Rpc.Caller != player.Network.Owner )
			return;

		if ( !player.IsBorderPatrol )
			return;

		if ( _serverCooldown > 0f )
			return;

		_serverCooldown = ActionCooldown;

		var result = PerformServerAction( player );
		var state = result switch
		{
			true => ScreenState.Pass,
			false => ScreenState.Fail,
			_ => ScreenState.Idle
		};

		using ( Rpc.FilterInclude( Rpc.Caller ) )
		{
			NotifyResult( (int)state );
		}

		PlayActionEffects();
	}

	[Rpc.Broadcast( NetFlags.HostOnly )]
	void NotifyResult( int state )
	{
		_pendingResult = (ScreenState)state;
		_hasPendingResult = true;
	}

	protected override void OnUpdate()
	{
		base.OnUpdate();

		if ( _localWorkingUntil <= 0f && _localResult == ScreenState.Idle && _hasPendingResult )
		{
			if ( _pendingResult != ScreenState.Idle )
			{
				_localResult = _pendingResult;
				_localResultUntil = ResultHoldDuration;
			}
			_hasPendingResult = false;
		}

		if ( _localResultUntil <= 0f )
		{
			_localResult = ScreenState.Idle;
		}
	}

	[Rpc.Broadcast]
	void PlayActionEffects()
	{
		if ( Application.IsDedicatedServer )
			return;

		if ( ActionSound is not null )
			GameObject.PlaySound( ActionSound );

		if ( ViewModel.IsValid() )
			ViewModel.RunEvent<ViewModel>( x => x.OnAttack() );
		else if ( WorldModel.IsValid() )
			WorldModel.RunEvent<WorldModel>( x => x.OnAttack() );

		SpinCoil();
	}

	ScreenState CurrentScreenState
	{
		get
		{
			if ( _localWorkingUntil > 0f ) return ScreenState.Working;
			if ( _localResult != ScreenState.Idle && _localResultUntil > 0f ) return _localResult;
			return ScreenState.Idle;
		}
	}

	float WorkingProgress
	{
		get
		{
			if ( WorkingDuration <= 0f ) return 1f;
			var elapsed = WorkingDuration - (float)_localWorkingUntil;
			return (elapsed / WorkingDuration).Clamp( 0f, 1f );
		}
	}

	protected override void DrawScreenContent( Rect rect, HudPainter paint )
	{
		var state = CurrentScreenState;

		if ( state == ScreenState.Working )
		{
			DrawWorkingLoadingBar( rect, paint );
			return;
		}

		// Mirror toolgun screen exactly: dark cleared background, bold centered
		// text with an emoji icon. Color changes per state to sell the action.
		var (textColor, label) = state switch
		{
			ScreenState.Pass => (new Color( 0.45f, 1f, 0.55f ), $"✓ {PassLabel}"),
			ScreenState.Fail => (new Color( 1f, 0.40f, 0.40f ), $"✗ {FailLabel}"),
			_ => (Color.Orange, BuildIdleLabel()),
		};

		// Result state pulses for a punchier confirmation
		if ( state == ScreenState.Pass || state == ScreenState.Fail )
		{
			var pulse = MathF.Sin( Time.Now * 22f ) * 0.5f + 0.5f;
			textColor = Color.Lerp( textColor, Color.White, pulse * 0.35f );
		}

		var text = new TextRendering.Scope( label, textColor, 64 );
		text.LineHeight = 0.75f;
		text.FontName = "Poppins";
		text.TextColor = textColor;
		text.FontWeight = 700;

		var measured = text.Measure();
		float textW = measured.x;

		if ( textW <= rect.Width )
		{
			paint.DrawText( text, rect, TextFlag.Center );
		}
		else
		{
			DrawMarquee( rect, paint, text, textW, measured.y );
		}
	}

	string BuildIdleLabel()
	{
		var icon = string.IsNullOrEmpty( ScreenIcon ) ? "" : $"{ScreenIcon} ";
		return $"{icon}{ScreenTitle}";
	}

	/// <summary>
	/// Loading bar + TV-style noise while the host runs the check. Uses the same
	/// render target as the toolgun screen (see <see cref="ScreenWeapon"/>).
	/// </summary>
	void DrawWorkingLoadingBar( Rect rect, HudPainter paint )
	{
		var accent = new Color( 1f, 0.78f, 0.20f );
		var accentDim = new Color( 0.55f, 0.42f, 0.08f );

		var icon = string.IsNullOrEmpty( ScreenIcon ) ? "" : $"{ScreenIcon} ";
		var title = $"{icon}{WorkingLabel}";
		var titleScope = new TextRendering.Scope( title, accent, 48f );
		titleScope.LineHeight = 0.75f;
		titleScope.FontName = "Poppins";
		titleScope.FontWeight = 700;
		titleScope.TextColor = accent;

		var titleRect = new Rect( rect.Left, rect.Top + rect.Height * 0.06f, rect.Width, rect.Height * 0.32f );
		paint.DrawText( titleScope, titleRect, TextFlag.Center );

		float padX = rect.Width * 0.07f;
		float barY = rect.Top + rect.Height * 0.48f;
		float barW = rect.Width - padX * 2f;
		float barH = rect.Height * 0.22f;
		float barX = rect.Left + padX;

		var barBg = new Rect( barX, barY, barW, barH );
		paint.DrawRect( barBg, new Color( 0.02f, 0.03f, 0.05f, 0.92f ) );

		var border = accentDim.WithAlpha( 0.9f );
		paint.DrawLine( new Vector2( barX, barY ), new Vector2( barX + barW, barY ), 2f, border );
		paint.DrawLine( new Vector2( barX, barY + barH ), new Vector2( barX + barW, barY + barH ), 2f, border );
		paint.DrawLine( new Vector2( barX, barY ), new Vector2( barX, barY + barH ), 2f, border );
		paint.DrawLine( new Vector2( barX + barW, barY ), new Vector2( barX + barW, barY + barH ), 2f, border );

		// Wobbly fill edge from noise so the bar feels "alive"
		float fill = WorkingProgress;
		float edgeWobble = (Noise.Perlin( Time.Now * 22f, 3.7f ) - 0.5f) * 0.06f;
		fill = (fill + edgeWobble).Clamp( 0f, 1f );

		if ( fill > 0.002f )
		{
			var fillRect = new Rect( barX + 1f, barY + 1f, (barW - 2f) * fill, barH - 2f );
			paint.DrawRect( fillRect, accent.WithAlpha( 0.88f ) );

			// Bright leading edge
			float leadX = barX + (barW - 2f) * fill;
			paint.SetBlendMode( BlendMode.Lighten );
			paint.DrawRect( new Rect( leadX - 3f, barY + 1f, 5f, barH - 2f ), Color.White.WithAlpha( 0.55f ) );
			paint.SetBlendMode( BlendMode.Normal );

			DrawBarStaticNoise( paint, fillRect, accent, dense: true );
		}

		// Faint static in the empty portion of the track
		float fillW = (barW - 2f) * fill;
		var emptyRect = new Rect( barX + 1f + fillW, barY + 1f, (barW - 2f) * (1f - fill), barH - 2f );
		if ( emptyRect.Width > 2f )
			DrawBarStaticNoise( paint, emptyRect, accentDim, dense: false );

		// Segments (retro blocks)
		const int segments = 12;
		for ( int s = 1; s < segments; s++ )
		{
			float sx = barX + (barW / segments) * s;
			paint.DrawLine( new Vector2( sx, barY + 2f ), new Vector2( sx, barY + barH - 2f ), 1f, new Color( 0f, 0f, 0f, 0.35f ) );
		}

		// Percentage with a little noise jitter on the number
		int pct = (int)(WorkingProgress * 100f + (Noise.Perlin( Time.Now * 35f, 11f ) - 0.5f) * 4f);
		pct = Math.Clamp( pct, 0, 100 );
		var pctScope = new TextRendering.Scope( $"{pct}%", accent, 28f );
		pctScope.FontName = "Consolas";
		pctScope.FontWeight = 600;
		pctScope.TextColor = accent;
		var pctRect = new Rect( barX, barY + barH + 6f, barW, rect.Bottom - (barY + barH) - 4f );
		paint.DrawText( pctScope, pctRect, TextFlag.Center );
	}

	/// <summary>
	/// Sparse TV-style snow inside <paramref name="area"/> using value noise.
	/// </summary>
	void DrawBarStaticNoise( HudPainter paint, Rect area, Color tint, bool dense )
	{
		if ( area.Width < 1f || area.Height < 1f )
			return;

		int cols = dense ? 56 : 28;
		int rows = dense ? 8 : 4;
		float cellW = area.Width / cols;
		float cellH = area.Height / rows;
		float t = Time.Now * (dense ? 28f : 14f );

		for ( int cx = 0; cx < cols; cx++ )
		{
			for ( int ry = 0; ry < rows; ry++ )
			{
				float fx = (cx + 0.5f) / cols;
				float fy = (ry + 0.5f) / rows;
				float n = Noise.Perlin( fx * 14f + t, fy * 9f + cx * 0.07f );
				float thresh = dense ? 0.62f : 0.78f;
				if ( n < thresh )
					continue;

				float a = (n - thresh) / (1f - thresh) * (dense ? 0.45f : 0.22f);
				var c = Color.Lerp( Color.White, tint, 0.35f ).WithAlpha( a );
				paint.DrawRect( new Rect( area.Left + cx * cellW, area.Top + ry * cellH, cellW + 1f, cellH + 1f ), c );
			}
		}
	}

	void DrawMarquee( Rect rect, HudPainter paint, TextRendering.Scope text, float textW, float textH )
	{
		const float scrollSpeed = 80f;
		const float gap = 60f;
		float cycle = textW + gap;
		float offset = (Time.Now * scrollSpeed) % cycle;

		float y = rect.Top + (rect.Height - textH) * 0.5f;
		float x = rect.Width - offset;

		paint.DrawText( text, new Rect( x, y, textW, textH ), TextFlag.SingleLine | TextFlag.Left );
		paint.DrawText( text, new Rect( x - cycle, y, textW, textH ), TextFlag.SingleLine | TextFlag.Left );
	}

	public override void DrawHud( HudPainter painter, Vector2 crosshair )
	{
		var color = CanAct ? ReadyColor : new Color( 1f, 0.4f, 0.3f );

		painter.SetBlendMode( BlendMode.Lighten );
		painter.DrawCircle( crosshair, 6f, color );
		painter.DrawCircle( crosshair, 3f, color );
	}
}
