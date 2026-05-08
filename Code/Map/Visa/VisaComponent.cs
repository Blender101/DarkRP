using Sandbox.UI;

/// <summary>
/// Visa data carried on a <see cref="VisaCard"/> in the player's inventory.
/// Real visas are valid until <see cref="ExpiryTime"/>; fake visas pass most
/// scans but have a per-check chance of being detected. All issuance and
/// validity logic runs on the host.
/// </summary>
public sealed class VisaComponent : Component
{
	public const float FakeDetectionChance = 0.20f;

	[Property, Sync( SyncFlags.FromHost )]
	public bool IsFake { get; set; }

	[Property, Sync( SyncFlags.FromHost )]
	public DateTime ExpiryTime { get; set; }

	[Property, Sync( SyncFlags.FromHost )]
	public string IssuerName { get; set; } = "Border Authority";

	/// <summary>
	/// Set on the host once a fake visa has been detected (by a scanner
	/// or a patrol /checkvisa). Subsequent validity checks always fail,
	/// preventing players from rerolling the 20% detection chance.
	/// </summary>
	[Sync( SyncFlags.FromHost )]
	public bool IsBurned { get; set; }

	public bool IsExpired => DateTime.UtcNow >= ExpiryTime;

	/// <summary>
	/// Runs on the host. Returns false on the client so cheats can't
	/// roll the fake-detection dice locally to pre-screen visas.
	/// On a failed fake-detection roll the visa is permanently burned.
	/// </summary>
	public bool CheckValidity()
	{
		if ( !Networking.IsHost )
			return false;

		if ( IsExpired || IsBurned )
			return false;

		if ( IsFake && Game.Random.Float() < FakeDetectionChance )
		{
			IsBurned = true;
			return false;
		}

		return true;
	}

	/// <summary>
	/// Returns the visa carried by the given player, by scanning their inventory
	/// for a <see cref="VisaCard"/>. Returns null when they have no card.
	/// </summary>
	public static VisaComponent For( Player player )
	{
		var card = CardFor( player );
		return card.IsValid() ? card.Visa : null;
	}

	/// <summary>
	/// Returns the first <see cref="VisaCard"/> in the player's inventory, if any.
	/// </summary>
	public static VisaCard CardFor( Player player )
	{
		if ( !player.IsValid() )
			return null;

		return player.GameObject.GetComponentInChildren<VisaCard>( true );
	}
}
