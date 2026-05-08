public sealed class PlayerRoleplaySaveData
{
	public int Version { get; set; } = 1;
	public int Money { get; set; }
	public string RoleplayName { get; set; }
	public bool HasClaimedDiscordBonus { get; set; }
	public DateTime SavedAtUtc { get; set; } = DateTime.UtcNow;
}
