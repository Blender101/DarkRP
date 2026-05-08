using Sandbox.UI;

/// <summary>
/// Marker component placed on a player's GameObject while they are
/// carrying a contraband shipment. Replicates from the host so other
/// clients can see who is holding a package.
/// </summary>
public sealed class Contraband : Component
{
	[Property, Sync( SyncFlags.FromHost )]
	public string ItemName { get; set; } = "Contraband";

	[Property, Sync( SyncFlags.FromHost )]
	public int PurchasePrice { get; set; }

	public static bool IsCarrying( Player player )
	{
		return player.IsValid() && player.GameObject.GetComponent<Contraband>() is not null;
	}
}
