using Sandbox.UI;

/// <summary>
/// Marker component placed on a player's GameObject while they are
/// carrying a contraband shipment. Replicates from the host so other
/// clients can see who is holding a package.
///
/// Each shipment has a delivery window (<see cref="Expiry"/>) and a
/// preferred drop-off (<see cref="BuyerId"/>). When the window expires
/// there is a short <see cref="GraceWindowSeconds"/> period where the
/// buyer still accepts the shipment but only pays half. Past that the
/// shipment spoils and the component is removed.
/// </summary>
public sealed class Contraband : Component
{
	public const float GraceWindowSeconds = 30f;

	[Property, Sync( SyncFlags.FromHost )]
	public string ItemName { get; set; } = "Contraband";

	[Property, Sync( SyncFlags.FromHost )]
	public int PurchasePrice { get; set; }

	/// <summary>
	/// Counts down to delivery deadline. Negative values indicate the
	/// shipment is in the grace window (until <see cref="-GraceWindowSeconds"/>)
	/// or fully spoiled (past that).
	/// </summary>
	[Sync( SyncFlags.FromHost )]
	public TimeUntil Expiry { get; set; }

	/// <summary>
	/// GameObject id of the preferred <see cref="DrugBuyer"/> for this shipment.
	/// Used to drive the carrier's waypoint HUD. The buyer will still accept
	/// the shipment even if this id is empty/invalid (e.g. the buyer was destroyed).
	/// </summary>
	[Sync( SyncFlags.FromHost )]
	public Guid BuyerId { get; set; }

	[Sync( SyncFlags.FromHost )]
	public bool DeclaredAtBorder { get; set; }

	public bool IsInGracePeriod => Expiry <= 0f && Expiry > -GraceWindowSeconds;
	public bool IsSpoiled => Expiry <= -GraceWindowSeconds;

	public static bool IsCarrying( Player player )
	{
		return player.IsValid() && player.GameObject.GetComponent<Contraband>() is not null;
	}

	protected override void OnUpdate()
	{
		if ( !Networking.IsHost )
			return;

		if ( !IsSpoiled )
			return;

		var player = GameObject.GetComponent<Player>();
		if ( player.IsValid() )
		{
			Notices.SendNotice( player.Network.Owner, "schedule", Color.Red,
				$"{ItemName} spoiled — your shipment is gone.", 5 );
		}

		Destroy();
	}

	/// <summary>
	/// Resolves the buyer this shipment is bound to. Falls back to any
	/// <see cref="DrugBuyer"/> in the scene if the original was destroyed.
	/// </summary>
	public DrugBuyer ResolveBuyer()
	{
		if ( Game.ActiveScene is null )
			return null;

		if ( BuyerId != Guid.Empty )
		{
			var go = Game.ActiveScene.Directory.FindByGuid( BuyerId );
			var buyer = go?.GetComponent<DrugBuyer>();
			if ( buyer.IsValid() )
				return buyer;
		}

		return Game.ActiveScene.GetAllComponents<DrugBuyer>().FirstOrDefault();
	}
}
