using Terraria;
using TShockAPI;

namespace Philosophyz
{
	public class PlayerInfo
	{
		public const string InfoKey = "pz-info";

		private readonly TSPlayer _player;

		private bool? _fakeSscStatus;

		public bool? FakeSscStatus
		{
			get { return _fakeSscStatus; }
			set
			{
				if (value == _fakeSscStatus)
					return;
				Philosophyz.SendInfo(_player, value ?? Main.ServerSideCharacter);
				_fakeSscStatus = value;
			}
		}

		public bool InSscRegion { get; set; }

		public bool BypassChange { get; set; }

		public PlayerData OriginData { get; set; }

		private PlayerInfo(TSPlayer player)
		{
			_player = player;

			FakeSscStatus = false;
		}

		public void SetBackupPlayerData()
		{
			var data = new PlayerData(_player);
			data.CopyCharacter(_player);

			OriginData = data;
		}

		public static PlayerInfo GetPlayerInfo(TSPlayer player)
		{
			var info = player.GetData<PlayerInfo>(InfoKey);
			if (info == null)
			{
				info = new PlayerInfo(player);
				player.SetData(InfoKey, info);
			}
			return info;
		}
	}
}
