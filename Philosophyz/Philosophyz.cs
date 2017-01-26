using System;
using System.Linq;
using System.Reflection;
using Terraria;
using TerrariaApi.Server;
using TShockAPI;
using TShockAPI.Hooks;

namespace Philosophyz
{
	[ApiVersion(2, 0)]
	public class Philosophyz : TerrariaPlugin
	{
		private const string BypassStatus = "pz-bp";

		private const string OriginData = "pz-pre-dt";

		private const string InRegion = "pz-in-reg";

		public override string Name => Assembly.GetExecutingAssembly().GetName().Name;

		public override string Author => "MistZZT";

		public override string Description => "Dark";

		public override Version Version => Assembly.GetExecutingAssembly().GetName().Version;

		public Philosophyz(Main game) : base(game) { }

		internal PzRegionManager PzRegions;

		public override void Initialize()
		{
			ServerApi.Hooks.GameInitialize.Register(this, OnInit);
			ServerApi.Hooks.GamePostInitialize.Register(this, OnPostInit);
			ServerApi.Hooks.NetGreetPlayer.Register(this, OnGreet);
			ServerApi.Hooks.ServerLeave.Register(this, OnLeave);
			ServerApi.Hooks.NetSendData.Register(this, OnSendData);

			RegionHooks.RegionEntered += OnRegionEntered;
			RegionHooks.RegionLeft += OnRegionLeft;
			RegionHooks.RegionDeleted += OnRegionDeleted;
		}

		/// <summary>
		/// 如果WorldInfo发送时，Inregion=true则取消发送
		/// 所以发送前，inregion要为false
		/// </summary>
		/// <param name="args"></param>
		private static void OnSendData(SendDataEventArgs args)
		{
			if (args.MsgId != PacketTypes.WorldInfo)
				return;

			var player = TShock.Players.ElementAtOrDefault(args.remoteClient);

			if (player == null)
				return;

			if (!player.GetData<bool>(InRegion))
				return;

			args.Handled = true;
		}

		private static void OnLeave(LeaveEventArgs args)
		{
			if (args.Who < 0 || args.Who > Main.maxNetPlayers)
				return;

			var player = TShock.Players[args.Who];

			if (player == null)
				return;

			if (!player.GetData<bool>(InRegion))
				return;

			var data = player.GetData<PlayerData>(OriginData);
			var ssc = Main.ServerSideCharacter;

			if (!ssc)
			{
				Main.ServerSideCharacter = true;
				player.SendData(PacketTypes.WorldInfo);
			}

			data?.RestoreCharacter(player);

			if (!ssc)
			{
				Main.ServerSideCharacter = false;
				player.SendData(PacketTypes.WorldInfo);
			}
		}

		private static void OnGreet(GreetPlayerEventArgs args)
		{
			var player = TShock.Players[args.Who];

			player?.SetData(BypassStatus, false);
		}

		private void OnPostInit(EventArgs args)
		{
			PzRegions.ReloadRegions();
		}

		private void OnInit(EventArgs args)
		{
			Commands.ChatCommands.Add(new Command("pz.admin.manage", PzCmd, "pz"));
			Commands.ChatCommands.Add(new Command("pz.admin.toggle", ToggleBypass, "pztoggle"));
			Commands.ChatCommands.Add(new Command("pz.select", PzSelect, "pzselect"));

			PzRegions = new PzRegionManager(TShock.DB);
		}

		protected override void Dispose(bool disposing)
		{
			if (disposing)
			{
				RegionHooks.RegionEntered -= OnRegionEntered;
				RegionHooks.RegionLeft -= OnRegionLeft;
				RegionHooks.RegionDeleted -= OnRegionDeleted;
			}
			base.Dispose(disposing);
		}

		private void OnRegionDeleted(RegionHooks.RegionDeletedEventArgs args)
		{
			if (!PzRegions.PzRegions.Exists(p => p.Id == args.Region.ID))
				return;

			PzRegions.RemoveRegion(args.Region.ID);
		}

		private void OnRegionLeft(RegionHooks.RegionLeftEventArgs args)
		{
			if (!PzRegions.PzRegions.Exists(p => p.Id == args.Region.ID))
				return;

			if (!args.Player.GetData<bool>(InRegion))
				return;

			Change(args.Player, args.Player.GetData<PlayerData>(OriginData));
			args.Player.SetData(InRegion, false);
			args.Player.SendData(PacketTypes.WorldInfo); // 取消本地云存档状态
		}

		private void OnRegionEntered(RegionHooks.RegionEnteredEventArgs args)
		{
			var region = PzRegions.GetRegionById(args.Region.ID);
			if (region == null)
				return;

			if (args.Player.GetData<bool>(BypassStatus))
				return;

			if (!region.HasDefault)
				return;

			var data = new PlayerData(args.Player);
			data.CopyCharacter(args.Player);

			args.Player.SetData(OriginData, data);

			Change(args.Player, region.GetDefaultData());
			args.Player.SetData(InRegion, true); // 调换位置，因为发WorldInfo有判断
		}

		private static void ToggleBypass(CommandArgs args)
		{
			var current = args.Player.GetData<bool>(BypassStatus);

			args.Player.SetData(BypassStatus, !current);

			args.Player.SendSuccessMessage("调整跳过装备更换模式为{0}.", current ? "关闭" : "开启");
		}

		private void PzSelect(CommandArgs args)
		{
			var pwd = args.Parameters.Count == 0 ? "WOSHIJIADE" : args.Parameters[0];
			var select = args.Parameters.ElementAtOrDefault(1);

			if (pwd.Equals("WOSHIJIADE", StringComparison.InvariantCulture))
			{
				args.Player.SendInfoMessage("你可能是输入了假指令!");
				return;
			}

			if (!pwd.Equals("sjdUdfji23431,.32131243RNVj", StringComparison.InvariantCulture))
			{
				args.Player.SendInfoMessage("你可能是用了假指令!");
				return;
			}

			if (args.Player.CurrentRegion == null)
			{
				args.Player.SendInfoMessage("你可能在假地球上!");
				return;
			}

			var region = PzRegions.GetRegionById(args.Player.CurrentRegion.ID);
			if (region == null)
			{
				args.Player.SendInfoMessage("你可能在假地方!");
				return;
			}

			PlayerData data;
			if (string.IsNullOrWhiteSpace(select) || !region.PlayerDatas.TryGetValue(select, out data))
			{
				args.Player.SendInfoMessage("你可能做了假选择!");
				return;
			}

			if (!args.Player.GetData<bool>(InRegion))
			{
				var origin = new PlayerData(args.Player);
				origin.CopyCharacter(args.Player);

				args.Player.SetData(OriginData, origin);
			}

			args.Player.SetData(InRegion, false); // 为了发送数据包

			Change(args.Player, data);

			args.Player.SetData(InRegion, true); // 调换位置，因为发WorldInfo有判断

			args.Player.SendInfoMessage("当前人物切换为: {0}", select);
		}

		private void PzCmd(CommandArgs args)
		{
			var cmd = args.Parameters.Count == 0 ? "HELP" : args.Parameters[0].ToUpperInvariant();

			switch (cmd)
			{
				case "ADD":
					#region add
					if (args.Parameters.Count < 3)
					{
						args.Player.SendErrorMessage("语法无效! 正确语法: /pz add <区域名> <存档名> [玩家名]");
						return;
					}

					var regionName = args.Parameters[1];
					var name = args.Parameters[2];
					var playerName = args.Parameters.ElementAtOrDefault(3);

					if (name.Length > 10)
					{
						args.Player.SendErrorMessage("存档名的长度不能超过10!");
						return;
					}

					var region = TShock.Regions.GetRegionByName(regionName);
					if (region == null)
					{
						args.Player.SendErrorMessage("区域名无效!");
						return;
					}
					TSPlayer player = null;
					if (!string.IsNullOrWhiteSpace(playerName))
					{
						var players = TShock.Utils.FindPlayer(playerName);
						if (players.Count == 0)
						{
							args.Player.SendErrorMessage("未找到玩家!");
							return;
						}
						if (players.Count > 1)
						{
							TShock.Utils.SendMultipleMatchError(args.Player, players.Select(p => p.Name));
							return;
						}
						player = players[0];
					}
					player = player ?? args.Player;
					var data = new PlayerData(null);
					data.CopyCharacter(player);

					PzRegions.AddRegion(region.ID);
					PzRegions.AddCharacter(region.ID, name, data);
					args.Player.SendSuccessMessage("添加区域完毕.");
					#endregion
					break;
				case "LIST":
					#region list
					int pageNumber;
					if (!PaginationTools.TryParsePageNumber(args.Parameters, 1, args.Player, out pageNumber))
						return;
					var names = from pz in PzRegions.PzRegions
							select TShock.Regions.GetRegionByID(pz.Id).Name + ": " + string.Join(", ", pz.PlayerDatas.Keys);
					PaginationTools.SendPage(args.Player, pageNumber, PaginationTools.BuildLinesFromTerms(names),
						new PaginationTools.Settings
						{
							HeaderFormat = "应用区域 ({0}/{1}):",
							FooterFormat = "键入 {0}pz list {{0}} 以获取下一页应用区域.".SFormat(Commands.Specifier),
							NothingToDisplayString = "当前没有可用应用区域."
						});
					#endregion
					break;
				case "REMOVE":
					#region remove
					if (args.Parameters.Count == 1)
					{
						args.Player.SendErrorMessage("语法无效! 正确语法: /pz remove <区域名>");
						return;
					}
					regionName = string.Join(" ", args.Parameters.Skip(1));
					region = TShock.Regions.GetRegionByName(regionName);
					if (region == null)
					{
						args.Player.SendErrorMessage("区域名无效!");
						return;
					}

					PzRegions.RemoveRegion(region.ID);
					args.Player.SendSuccessMessage("删除区域及存档完毕.");
					#endregion
					break;
				case "REMOVECHAR":
					#region removeChar
					if (args.Parameters.Count < 3)
					{
						args.Player.SendErrorMessage("语法无效! 正确语法: /pz removechar <区域名> <存档名>");
						return;
					}
					regionName = args.Parameters[1];
					name = args.Parameters[2];
					region = TShock.Regions.GetRegionByName(regionName);
					if (region == null)
					{
						args.Player.SendErrorMessage("区域名无效!");
						return;
					}

					PzRegions.RemoveCharacter(region.ID, name);
					args.Player.SendSuccessMessage("删除存档完毕.");
					#endregion
					break;
				case "DEFAULT":
					#region default
					if (args.Parameters.Count < 3)
					{
						args.Player.SendErrorMessage("语法无效! 正确语法: /pz default <区域名> <存档名>");
						return;
					}
					regionName = args.Parameters[1];
					name = args.Parameters[2];
					region = TShock.Regions.GetRegionByName(regionName);
					if (region == null)
					{
						args.Player.SendErrorMessage("区域名无效!");
						return;
					}

					var pzregion = PzRegions.GetRegionById(region.ID);
					if (pzregion == null)
					{
						args.Player.SendErrorMessage("该区域并卟是Pz区域!");
						return;
					}
					if (!pzregion.PlayerDatas.ContainsKey(name))
					{
						args.Player.SendErrorMessage("区域内未找到符合条件的存档!");
						return;
					}

					PzRegions.SetDefaultCharacter(region.ID, name);
					args.Player.SendSuccessMessage("设定存档完毕.");
					#endregion
					break;
				case "DELDEFAULT":
					#region deldefault
					if (args.Parameters.Count == 1)
					{
						args.Player.SendErrorMessage("语法无效! 正确语法: /pz deldefault <区域名>");
						return;
					}
					regionName = string.Join(" ", args.Parameters.Skip(1));
					region = TShock.Regions.GetRegionByName(regionName);
					if (region == null)
					{
						args.Player.SendErrorMessage("区域名无效!");
						return;
					}

					pzregion = PzRegions.GetRegionById(region.ID);
					if (pzregion == null)
					{
						args.Player.SendErrorMessage("该区域并卟是Pz区域!");
						return;
					}

					PzRegions.SetDefaultCharacter(region.ID, null);
					args.Player.SendSuccessMessage("移除默认存档完毕.");
					#endregion
					break;
				case "SHOW":
				case "RESTORE":
					args.Player.SendErrorMessage("暂不支持该功能.");
					break;
				case "HELP":
					#region help
					if (!PaginationTools.TryParsePageNumber(args.Parameters, 1, args.Player, out pageNumber))
						return;
					var help = new[]
					{
						"add <区域名> <存档名> [玩家名(默认为自己)] - - 增加区域内存档",
						"remove <区域名> - - 删除区域内所有存档",
						"removechar <区域名> <存档名> - - 删除区域内存档",
						"default <区域名> <存档名> - - 设置单一存档默认值",
						"deldefault <区域名> - - 删除单一存档默认值",
						"list [页码] - - 显示所有区域",
						"help [页码] - - 显示子指令帮助"
					};
					PaginationTools.SendPage(args.Player, pageNumber, help,
						new PaginationTools.Settings
						{
							HeaderFormat = "应用区域指令帮助 ({0}/{1}):",
							FooterFormat = "键入 {0}pz help {{0}} 以获取下一页应用区域帮助.".SFormat(Commands.Specifier),
							NothingToDisplayString = "当前没有可用帮助."
						});
					#endregion
					break;
				default:
					args.Player.SendErrorMessage("语法无效! 键入 /pz help 以获取帮助.");
					return;
			}
		}

		private static void Change(TSPlayer player, PlayerData data)
		{
			var ssc = Main.ServerSideCharacter;

			try
			{
				if (!ssc)
				{
					Main.ServerSideCharacter = true;
					player.SendData(PacketTypes.WorldInfo);
				}

				data.RestoreCharacter(player);
			}
			finally
			{
				if (!ssc)
				{
					Main.ServerSideCharacter = false;
				}
			}
		}
	}
}
