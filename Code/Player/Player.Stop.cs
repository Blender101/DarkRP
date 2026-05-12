using Sandbox.UI;

/// <summary>
/// Stop &amp; ID: Border Patrol uses <c>use</c> on a citizen to start a roadside stop (slow + action HUD).
/// Bribery: stopped citizen uses <c>use</c> while looking at the stopping officer to offer cash; officer accepts or declines (logged to system chat).
/// </summary>
public sealed partial class Player
{
	public const float BorderStopMaxRange = 220f;
	public const float BorderStopRunMultiplier = 0.68f;

	[Sync( SyncFlags.FromHost )] public Guid BorderStopOfficerId { get; set; }
	[Sync( SyncFlags.FromHost )] public Guid BorderStopCitizenId { get; set; }

	[Sync( SyncFlags.FromHost )] public Guid BorderBribeOfferCitizenId { get; set; }
	[Sync( SyncFlags.FromHost )] public int BorderBribeOfferAmount { get; set; }

	float? _preBorderStopRunSpeed;

	public bool IsBorderStopOfficer => BorderStopCitizenId != Guid.Empty;
	public bool IsBorderStopCitizen => BorderStopOfficerId != Guid.Empty;

	public void ClearBorderStopForPlayer( Player player )
	{
		if ( !Networking.IsHost || !player.IsValid() )
			return;

		if ( player.BorderStopCitizenId != Guid.Empty )
		{
			var citizen = For( player.BorderStopCitizenId );
			ClearBorderStopPair( player, citizen );
			return;
		}

		if ( player.BorderStopOfficerId != Guid.Empty )
		{
			var officer = For( player.BorderStopOfficerId );
			ClearBorderStopPair( officer, player );
		}
	}

	void ClearBorderStopPair( Player officer, Player citizen )
	{
		if ( !officer.IsValid() )
			return;

		officer.BorderStopCitizenId = Guid.Empty;
		officer.BorderBribeOfferCitizenId = Guid.Empty;
		officer.BorderBribeOfferAmount = 0;

		if ( citizen.IsValid() )
		{
			citizen.BorderStopOfficerId = Guid.Empty;
			citizen.RestoreBorderStopRunSpeedHost();
		}
	}

	void RestoreBorderStopRunSpeedHost()
	{
		if ( !Networking.IsHost || !Controller.IsValid() )
			return;

		if ( _preBorderStopRunSpeed.HasValue )
		{
			Controller.RunSpeed = _preBorderStopRunSpeed.Value;
			_preBorderStopRunSpeed = null;
		}
	}

	void ApplyBorderStopCitizenSlowdown()
	{
		if ( IsProxy || IsArrested || !Controller.IsValid() )
			return;

		if ( BorderStopOfficerId == Guid.Empty )
		{
			if ( _preBorderStopRunSpeed.HasValue )
			{
				Controller.RunSpeed = _preBorderStopRunSpeed.Value;
				_preBorderStopRunSpeed = null;
			}

			return;
		}

		_preBorderStopRunSpeed ??= Controller.RunSpeed;
		Controller.RunSpeed = _preBorderStopRunSpeed.Value * BorderStopRunMultiplier;
		if ( Controller.Body.IsValid() )
			Controller.Body.Velocity = Controller.Body.Velocity.ClampLength( Controller.RunSpeed );
	}

	void HandleBorderStopInput()
	{
		if ( !IsLocalPlayer || !Input.Pressed( "use" ) )
			return;

		if ( BorderStopOfficerId != Guid.Empty )
		{
			var officer = For( BorderStopOfficerId );
			if ( officer.IsValid()
				&& WorldPosition.Distance( officer.WorldPosition ) <= BorderStopMaxRange
				&& TraceUsePlayer() == officer )
			{
				RequestBorderBribeOffer();
				Input.Clear( "use" );
			}
		}

		if ( !IsBorderPatrol || BorderStopCitizenId != Guid.Empty )
			return;

		if ( BorderStopOfficerId != Guid.Empty )
			return;

		var target = TraceUsePlayer();
		if ( !target.IsValid() || target == this || target.IsBorderPatrol )
			return;

		if ( target.BorderStopOfficerId != Guid.Empty )
			return;

		if ( WorldPosition.Distance( target.WorldPosition ) > BorderStopMaxRange )
			return;

		RequestBeginBorderStop( target.PlayerId );
		Input.Clear( "use" );
	}

	Player TraceUsePlayer()
	{
		if ( !Controller.IsValid() )
			return null;

		var trace = Scene.Trace
			.Ray( EyeTransform.ForwardRay, BorderStopMaxRange )
			.IgnoreGameObjectHierarchy( GameObject )
			.WithoutTags( "playercontroller" )
			.UseHitboxes()
			.Run();

		if ( !trace.GameObject.IsValid() )
			return null;

		return trace.GameObject.Root.GetComponent<Player>();
	}

	[Rpc.Host]
	public void RequestBeginBorderStop( Guid citizenPlayerId )
	{
		if ( Rpc.Caller != Network.Owner || !IsBorderPatrol )
			return;

		var citizen = For( citizenPlayerId );
		if ( !citizen.IsValid() || citizen == this || citizen.IsBorderPatrol )
			return;

		if ( BorderStopCitizenId != Guid.Empty || citizen.BorderStopOfficerId != Guid.Empty )
			return;

		if ( WorldPosition.Distance( citizen.WorldPosition ) > BorderStopMaxRange )
			return;

		BorderStopCitizenId = citizenPlayerId;
		citizen.BorderStopOfficerId = PlayerId;

		if ( citizen.Network.Owner is { } cConn )
			Notices.SendNotice( cConn, "shield", Color.Orange, $"{DisplayName} stopped you for inspection.", 4 );
	}

	[Rpc.Host]
	public void RequestBorderStopRelease()
	{
		if ( Rpc.Caller != Network.Owner || !IsBorderPatrol )
			return;

		var citizen = For( BorderStopCitizenId );
		ClearBorderStopPair( this, citizen );
		if ( Network.Owner is { } oConn )
			Notices.SendNotice( oConn, "check_circle", Color.Green, "Stop ended.", 2 );
		if ( citizen.IsValid() && citizen.Network.Owner is { } cConn )
			Notices.SendNotice( cConn, "check_circle", Color.Green, "You are free to go.", 3 );
	}

	[Rpc.Host]
	public void RequestStopFriskCitizen()
	{
		if ( Rpc.Caller != Network.Owner || !IsBorderPatrol )
			return;

		var citizen = For( BorderStopCitizenId );
		if ( !citizen.IsValid() )
		{
			ClearBorderStopForPlayer( this );
			return;
		}

		if ( WorldPosition.Distance( citizen.WorldPosition ) > BorderStopMaxRange + 40f )
			return;

		FriskPlayer( citizen );
	}

	[Rpc.Host]
	public void RequestStopScanCitizen()
	{
		if ( Rpc.Caller != Network.Owner || !IsBorderPatrol )
			return;

		var citizen = For( BorderStopCitizenId );
		if ( !citizen.IsValid() )
		{
			ClearBorderStopForPlayer( this );
			return;
		}

		if ( WorldPosition.Distance( citizen.WorldPosition ) > BorderStopMaxRange + 40f )
			return;

		CheckVisaOnPlayer( citizen );
	}

	[Rpc.Host]
	public void RequestStopDetainCitizen()
	{
		if ( Rpc.Caller != Network.Owner || !IsBorderPatrol || !CanArrestPlayers )
			return;

		var citizen = For( BorderStopCitizenId );
		if ( !citizen.IsValid() || citizen.IsArrested )
			return;

		if ( WorldPosition.Distance( citizen.WorldPosition ) > BorderStopMaxRange + 40f )
			return;

		citizen.BeginArrest( this );
	}

	[Rpc.Host]
	public void RequestBorderBribeOffer()
	{
		if ( Rpc.Caller != Network.Owner )
			return;

		if ( BorderStopOfficerId == Guid.Empty )
			return;

		var officer = For( BorderStopOfficerId );
		if ( !officer.IsValid() || officer.BorderBribeOfferCitizenId != Guid.Empty )
			return;

		if ( WorldPosition.Distance( officer.WorldPosition ) > BorderStopMaxRange )
			return;

		if ( TraceUsePlayer() != officer )
			return;

		var amount = Math.Clamp( (int)(Money * 0.12f), 25, 2500 );
		if ( amount > Money )
			return;

		officer.BorderBribeOfferCitizenId = PlayerId;
		officer.BorderBribeOfferAmount = amount;

		if ( officer.Network.Owner is { } oConn )
			Notices.SendNotice( oConn, "payments", Color.Yellow, $"{DisplayName} offered you ${amount:n0} (bribe). Check Stop menu.", 5 );
		if ( Network.Owner is { } cConn )
			Notices.SendNotice( cConn, "payments", Color.White, $"You offered ${amount:n0}.", 3 );
	}

	[Rpc.Host]
	public void RequestAcceptBorderBribe()
	{
		if ( Rpc.Caller != Network.Owner || !IsBorderPatrol )
			return;

		var citizen = For( BorderBribeOfferCitizenId );
		if ( !citizen.IsValid() || BorderBribeOfferAmount <= 0 )
		{
			BorderBribeOfferCitizenId = Guid.Empty;
			BorderBribeOfferAmount = 0;
			return;
		}

		var amount = BorderBribeOfferAmount;
		if ( !citizen.TryTakeMoney( amount ) )
		{
			BorderBribeOfferCitizenId = Guid.Empty;
			BorderBribeOfferAmount = 0;
			Notices.SendNotice( Network.Owner, "block", Color.Red, "They could not pay.", 3 );
			return;
		}

		GiveMoney( amount );
		BorderBribeOfferCitizenId = Guid.Empty;
		BorderBribeOfferAmount = 0;

		Scene.Get<Chat>()?.AddSystemText(
			$"[ADMIN] Border bribe: {citizen.DisplayName} paid {DisplayName} ${amount:n0}.", "payments" );

		ClearBorderStopPair( this, citizen );

		if ( Network.Owner is { } oConn )
			Notices.SendNotice( oConn, "check_circle", Color.Green, $"Accepted ${amount:n0}. Stop cleared.", 4 );
		if ( citizen.Network.Owner is { } cConn )
			Notices.SendNotice( cConn, "check_circle", Color.Orange, "The officer accepted. You're free to go.", 4 );
	}

	[Rpc.Host]
	public void RequestDeclineBorderBribe()
	{
		if ( Rpc.Caller != Network.Owner || !IsBorderPatrol )
			return;

		var citizen = For( BorderBribeOfferCitizenId );
		BorderBribeOfferCitizenId = Guid.Empty;
		BorderBribeOfferAmount = 0;

		Scene.Get<Chat>()?.AddSystemText(
			$"[ADMIN] Border bribe declined: {DisplayName} refused an offer from {(citizen.IsValid() ? citizen.DisplayName : "unknown")}.", "block" );

		if ( citizen.IsValid() && citizen.Network.Owner is { } cConn )
			Notices.SendNotice( cConn, "block", Color.Orange, "The officer declined your offer.", 3 );
	}
}
