public sealed partial class Player
{
	bool _isRestoringRoleplayData;

	public void LoadRoleplayData()
	{
		if ( !Networking.IsHost || SteamId <= 0 )
			return;

		if ( !PlayerRoleplayStorage.TryLoad( SteamId, out var data, out var loadedFromBackup ) )
			return;

		_isRestoringRoleplayData = true;
		try
		{
			Money = Math.Max( 0, data.Money );
			HasClaimedDiscordBonus = data.HasClaimedDiscordBonus;
			if ( TryNormalizeRoleplayName( data.RoleplayName, out var roleplayName, out _ ) )
			{
				SetRoleplayName( roleplayName );
			}
		}
		finally
		{
			_isRestoringRoleplayData = false;
		}

		EnsureValidJobDefinition();

		if ( loadedFromBackup )
		{
			SaveRoleplayData();
		}
	}

	public void SaveRoleplayData()
	{
		if ( !Networking.IsHost || _isRestoringRoleplayData || SteamId <= 0 )
			return;

		PlayerRoleplayStorage.Save( SteamId, new PlayerRoleplaySaveData
		{
			Money = Money,
			RoleplayName = PlayerData?.DisplayName,
			HasClaimedDiscordBonus = HasClaimedDiscordBonus
		} );
	}
}
