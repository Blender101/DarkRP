/// <summary>
/// Visa data carried directly on a <see cref="Player"/>. Real visas are valid
/// until <see cref="ExpiryTime"/>; fake visas pass most scans but have a
/// per-check chance of being detected. All issuance and validity logic runs
/// on the host.
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

	/// <summary>True once the player has actually been issued a visa.</summary>
	public bool HasIssuedVisa => ExpiryTime != default;

	/// <summary>
	/// True only when an issued visa's expiry has passed. An unissued visa
	/// is "missing", not "expired".
	/// </summary>
	public bool IsExpired => HasIssuedVisa && DateTime.UtcNow >= ExpiryTime;

	/// <summary>
	/// Runs on the host. Returns false on the client so cheats can't
	/// roll the fake-detection dice locally to pre-screen visas.
	/// On a failed fake-detection roll the visa is permanently burned.
	/// </summary>
	public bool CheckValidity()
	{
		if ( !Networking.IsHost )
			return false;

		if ( !HasIssuedVisa || IsExpired || IsBurned )
			return false;

		if ( IsFake && Game.Random.Float() < FakeDetectionChance )
		{
			IsBurned = true;
			return false;
		}

		return true;
	}

	/// <summary>
	/// Returns the visa component attached to the given player, or null
	/// when the player object isn't valid. The component is auto-added
	/// on player spawn so this should normally not return null for a
	/// live player.
	/// </summary>
	public static VisaComponent For( Player player )
	{
		if ( !player.IsValid() )
			return null;

		return player.GameObject.GetComponent<VisaComponent>();
	}
}
