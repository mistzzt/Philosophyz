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

			RegionHooks.RegionEntered += OnRegionEntered;
			RegionHooks.RegionLeft += OnRegionLeft;
			RegionHooks.RegionDeleted += OnRegionDeleted;
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
			Commands.ChatCommands.Add(new Command("pz.manage", PzCmd, "pz"));
			Commands.ChatCommands.Add(new Command("pz.toggle", ToggleBypass, "pztoggle"));

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
			if (PzRegions.PzRegions.All(p => p.Id != args.Region.ID))
				return;

			PzRegions.RemoveRegion(args.Region.ID);
		}

		private void OnRegionLeft(RegionHooks.RegionLeftEventArgs args)
		{
			if (PzRegions.PzRegions.All(p => p.Id != args.Region.ID))
				return;

			if (!args.Player.GetData<bool>(InRegion))
				return;

			// send origin data
		}

		private void OnRegionEntered(RegionHooks.RegionEnteredEventArgs args)
		{
			if (PzRegions.PzRegions.All(p => p.Id != args.Region.ID))
				return;

			if (args.Player.GetData<bool>(BypassStatus))
				return;

			var data = new PlayerData(args.Player);
			data.CopyCharacter(args.Player);

			args.Player.SetData(OriginData, data);
			args.Player.SetData(InRegion, true);
			
			// send region data
		}

		private static void ToggleBypass(CommandArgs args)
		{
			var current = args.Player.GetData<bool>(BypassStatus);

			args.Player.SetData(BypassStatus, !current);

			args.Player.SendSuccessMessage("调整跳过装备更换模式为{0}.", current ? "关闭" : "开启");
		}

		private void PzCmd(CommandArgs args)
		{
			var cmd = args.Parameters.Count == 0 ? "HELP" : args.Parameters[0].ToUpperInvariant();

			switch (cmd)
			{
				case "ADD":
					if (args.Parameters.Count == 1)
					{
						args.Player.SendErrorMessage("语法无效! 正确语法: /pz add <区域名> [玩家名]");
						return;
					}

					var regionName = args.Parameters[1];
					var playerName = args.Parameters.ElementAtOrDefault(2);

					var region = TShock.Regions.GetRegionByName(regionName);
					if (region == null)
					{
						args.Player.SendErrorMessage("区域名无效!");
						return;
					}
					TSPlayer player = null;
					if (!string.IsNullOrWhiteSpace(playerName))
					{
						var players = TShock.Utils.FindPlayer(args.Parameters[0]);
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
					PzRegions.AddRegion(region.ID, data);
					break;
				case "LIST":
					int pageNumber;
					if (!PaginationTools.TryParsePageNumber(args.Parameters, 1, args.Player, out pageNumber))
						return;
					var names = PzRegions.PzRegions.Select(p => p.Id).Select(TShock.Regions.GetRegionByID).Select(r => r.Name);
					PaginationTools.SendPage(args.Player, pageNumber, PaginationTools.BuildLinesFromTerms(names),
						new PaginationTools.Settings
						{
							HeaderFormat = "应用区域 ({0}/{1}):",
							FooterFormat = "键入 {0}pz list {{0}} 以获取下一页应用区域.".SFormat(Commands.Specifier),
							NothingToDisplayString = "当前没有可用应用区域."
						});
					break;
				case "REMOVE":
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
					break;
				case "SHOW":
				case "RESTORE":
					args.Player.SendErrorMessage("暂不支持该功能.");
					break;
				case "HELP":
					if (!PaginationTools.TryParsePageNumber(args.Parameters, 1, args.Player, out pageNumber))
						return;
					var help = new[]
					{
						"add <区域名> [玩家名(默认为自己)]",
						"remove <区域名>",
						"list [页码]",
						"help [页码]"
					};
					PaginationTools.SendPage(args.Player, pageNumber, PaginationTools.BuildLinesFromTerms(help),
						new PaginationTools.Settings
						{
							HeaderFormat = "应用区域指令帮助 ({0}/{1}):",
							FooterFormat = "键入 {0}pz help {{0}} 以获取下一页应用区域帮助.".SFormat(Commands.Specifier),
							NothingToDisplayString = "当前没有可用帮助."
						});
					break;
				default:
					args.Player.SendErrorMessage("语法无效! 键入 /pz help 以获取帮助.");
					return;
			}
		}
	}
}
