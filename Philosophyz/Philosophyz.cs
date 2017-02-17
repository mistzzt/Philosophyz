using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Philosophyz.Hooks;
using Terraria;
using Terraria.GameContent.Events;
using Terraria.Social;
using TerrariaApi.Server;
using TShockAPI;
using TShockAPI.Hooks;

namespace Philosophyz
{
	[ApiVersion(2, 0)]
	public class Philosophyz : TerrariaPlugin
	{
		private const string BypassStatus = "pz-bp";

		public const string OriginData = "pz-pre-dt";

		public const string InRegion = "pz-in-reg";

		public override string Name => Assembly.GetExecutingAssembly().GetName().Name;

		public override string Author => "MistZZT";

		public override string Description => "Dark";

		public override Version Version => Assembly.GetExecutingAssembly().GetName().Version;

		public Philosophyz(Main game) : base(game)
		{
			Order = 0; // 最早
		}

		internal PzRegionManager PzRegions;

		public override void Initialize()
		{
			ServerApi.Hooks.GameInitialize.Register(this, OnInit);
			ServerApi.Hooks.GamePostInitialize.Register(this, OnPostInit);
			ServerApi.Hooks.NetGreetPlayer.Register(this, OnGreet);
			ServerApi.Hooks.NetSendData.Register(this, OnSendData, 8000);

			RegionHooks.RegionEntered += OnRegionEntered;
			RegionHooks.RegionLeft += OnRegionLeft;
			RegionHooks.RegionDeleted += OnRegionDeleted;
		}

		protected override void Dispose(bool disposing)
		{
			if (disposing)
			{
				ServerApi.Hooks.GameInitialize.Deregister(this, OnInit);
				ServerApi.Hooks.GamePostInitialize.Deregister(this, OnPostInit);
				ServerApi.Hooks.NetGreetPlayer.Deregister(this, OnGreet);
				ServerApi.Hooks.NetSendData.Deregister(this, OnSendData);

				RegionHooks.RegionEntered -= OnRegionEntered;
				RegionHooks.RegionLeft -= OnRegionLeft;
				RegionHooks.RegionDeleted -= OnRegionDeleted;
			}
			base.Dispose(disposing);
		}

		/// <summary>
		/// 根据worldinfo发送时的状态判定是否需要fakessc
		/// </summary>
		/// <param name="args"></param>
		private static void OnSendData(SendDataEventArgs args)
		{
			if (args.MsgId != PacketTypes.WorldInfo)
				return;

			if (args.remoteClient == -1)
			{
				var onData = PackInfo(true);
				var offData = PackInfo(false);

				foreach (var tsPlayer in TShock.Players.Where(p => p?.Active == true))
				{
					if (!SendDataHooks.InvokePreSendData(tsPlayer, true)) continue;
					tsPlayer.SendRawData(tsPlayer.GetData<bool>(InRegion) ? onData : offData);
					SendDataHooks.InvokePostSendData(tsPlayer, true);
				}

				args.Handled = true;
			}
			else
			{
				var player = TShock.Players.ElementAtOrDefault(args.remoteClient);

				if (player != null)
				{
					// 如果在区域内，收到了来自别的插件的发送请求
					// 保持默认 ssc = true 并发送(也就是不需要改什么)
					// 如果在区域外，收到了来自别的插件的发送请求
					// 需要 fake ssc = false 并发送
					SendInfo(player, player.GetData<bool>(InRegion));

					args.Handled = true;
				}
			}
		}

		private static void OnGreet(GreetPlayerEventArgs args)
		{
			var player = TShock.Players.ElementAtOrDefault(args.Who);
			if (player == null)
				return;

			player.SetData(BypassStatus, false);
			SendInfo(player, false);
		}

		private void OnPostInit(EventArgs args)
		{
			PzRegions.ReloadRegions();
		}

		private void OnInit(EventArgs args)
		{
			if (!TShock.ServerSideCharacterConfig.Enabled)
			{
				TShock.Log.ConsoleError("[Pz] 未开启SSC! 你可能选错了插件.");
				Dispose(true);
				throw new NotSupportedException("该插件不支持非SSC模式运行!");
			}

			Commands.ChatCommands.Add(new Command("pz.admin.manage", PzCmd, "pz"));
			Commands.ChatCommands.Add(new Command("pz.admin.toggle", ToggleBypass, "pztoggle"));
			Commands.ChatCommands.Add(new Command("pz.select", PzSelect, "pzselect"));

			PzRegions = new PzRegionManager(TShock.DB);
		}

		private void OnRegionDeleted(RegionHooks.RegionDeletedEventArgs args)
		{
			if (!PzRegions.PzRegions.Exists(p => p.Id == args.Region.ID))
				return;

			PzRegions.RemoveRegion(args.Region.ID);
		}

		/// <summary>
		/// 离开区域时执行恢复存档并fakessc=false
		/// </summary>
		/// <param name="args"></param>
		private void OnRegionLeft(RegionHooks.RegionLeftEventArgs args)
		{
			if (!PzRegions.PzRegions.Exists(p => p.Id == args.Region.ID))
				return;

			if (!args.Player.GetData<bool>(InRegion))
				return;

			Change(args.Player, args.Player.GetData<PlayerData>(OriginData));
			args.Player.SetData(InRegion, false);

			SendInfo(args.Player, false);
		}

		/// <summary>
		/// 进入区域时fakessc=true
		/// 若有默认存档则开始更换
		/// </summary>
		/// <param name="args"></param>
		private void OnRegionEntered(RegionHooks.RegionEnteredEventArgs args)
		{
			var region = PzRegions.GetRegionById(args.Region.ID);
			if (region == null)
				return;

			if (args.Player.GetData<bool>(BypassStatus))
				return;

			SendInfo(args.Player, true); // 若有指令变换存档

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

			if (!args.Player.GetData<bool>(InRegion)) // 是否备份存档判断
			{
				var origin = new PlayerData(args.Player);
				origin.CopyCharacter(args.Player);

				args.Player.SetData(OriginData, origin);
			}

			Change(args.Player, data);

			args.Player.SetData(InRegion, true);

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
			data.RestoreCharacter(player);
		}

		/// <summary>
		/// ssc状态写入
		/// </summary>
		/// <param name="ssc">状态</param>
		private static byte[] PackInfo(bool ssc)
		{
			var memoryStream = new MemoryStream();
			var binaryWriter = new BinaryWriter(memoryStream);
			var position = binaryWriter.BaseStream.Position;
			binaryWriter.BaseStream.Position += 2L;
			binaryWriter.Write((byte)PacketTypes.WorldInfo);

			binaryWriter.Write((int)Main.time);
			BitsByte bb3 = 0;
			bb3[0] = Main.dayTime;
			bb3[1] = Main.bloodMoon;
			bb3[2] = Main.eclipse;
			binaryWriter.Write(bb3);
			binaryWriter.Write((byte)Main.moonPhase);
			binaryWriter.Write((short)Main.maxTilesX);
			binaryWriter.Write((short)Main.maxTilesY);
			binaryWriter.Write((short)Main.spawnTileX);
			binaryWriter.Write((short)Main.spawnTileY);
			binaryWriter.Write((short)Main.worldSurface);
			binaryWriter.Write((short)Main.rockLayer);
			binaryWriter.Write(Main.worldID);
			binaryWriter.Write(Main.worldName);
			binaryWriter.Write(Main.ActiveWorldFileData.UniqueId.ToString());
			binaryWriter.Write((byte)Main.moonType);
			binaryWriter.Write((byte)WorldGen.treeBG);
			binaryWriter.Write((byte)WorldGen.corruptBG);
			binaryWriter.Write((byte)WorldGen.jungleBG);
			binaryWriter.Write((byte)WorldGen.snowBG);
			binaryWriter.Write((byte)WorldGen.hallowBG);
			binaryWriter.Write((byte)WorldGen.crimsonBG);
			binaryWriter.Write((byte)WorldGen.desertBG);
			binaryWriter.Write((byte)WorldGen.oceanBG);
			binaryWriter.Write((byte)Main.iceBackStyle);
			binaryWriter.Write((byte)Main.jungleBackStyle);
			binaryWriter.Write((byte)Main.hellBackStyle);
			binaryWriter.Write(Main.windSpeedSet);
			binaryWriter.Write((byte)Main.numClouds);
			for (var k = 0; k < 3; k++)
			{
				binaryWriter.Write(Main.treeX[k]);
			}
			for (var l = 0; l < 4; l++)
			{
				binaryWriter.Write((byte)Main.treeStyle[l]);
			}
			for (var m = 0; m < 3; m++)
			{
				binaryWriter.Write(Main.caveBackX[m]);
			}
			for (var n = 0; n < 4; n++)
			{
				binaryWriter.Write((byte)Main.caveBackStyle[n]);
			}
			if (!Main.raining)
			{
				Main.maxRaining = 0f;
			}
			binaryWriter.Write(Main.maxRaining);
			BitsByte bb4 = 0;
			bb4[0] = WorldGen.shadowOrbSmashed;
			bb4[1] = NPC.downedBoss1;
			bb4[2] = NPC.downedBoss2;
			bb4[3] = NPC.downedBoss3;
			bb4[4] = Main.hardMode;
			bb4[5] = NPC.downedClown;
			bb4[6] = ssc;
			bb4[7] = NPC.downedPlantBoss;
			binaryWriter.Write(bb4);
			BitsByte bb5 = 0;
			bb5[0] = NPC.downedMechBoss1;
			bb5[1] = NPC.downedMechBoss2;
			bb5[2] = NPC.downedMechBoss3;
			bb5[3] = NPC.downedMechBossAny;
			bb5[4] = Main.cloudBGActive >= 1f;
			bb5[5] = WorldGen.crimson;
			bb5[6] = Main.pumpkinMoon;
			bb5[7] = Main.snowMoon;
			binaryWriter.Write(bb5);
			BitsByte bb6 = 0;
			bb6[0] = Main.expertMode;
			bb6[1] = Main.fastForwardTime;
			bb6[2] = Main.slimeRain;
			bb6[3] = NPC.downedSlimeKing;
			bb6[4] = NPC.downedQueenBee;
			bb6[5] = NPC.downedFishron;
			bb6[6] = NPC.downedMartians;
			bb6[7] = NPC.downedAncientCultist;
			binaryWriter.Write(bb6);
			BitsByte bb7 = 0;
			bb7[0] = NPC.downedMoonlord;
			bb7[1] = NPC.downedHalloweenKing;
			bb7[2] = NPC.downedHalloweenTree;
			bb7[3] = NPC.downedChristmasIceQueen;
			bb7[4] = NPC.downedChristmasSantank;
			bb7[5] = NPC.downedChristmasTree;
			bb7[6] = NPC.downedGolemBoss;
			bb7[7] = BirthdayParty.PartyIsUp;
			binaryWriter.Write(bb7);
			BitsByte bb8 = 0;
			bb8[0] = NPC.downedPirates;
			bb8[1] = NPC.downedFrost;
			bb8[2] = NPC.downedGoblins;
			bb8[3] = Sandstorm.Happening;
			bb8[4] = DD2Event.Ongoing;
			bb8[5] = DD2Event.DownedInvasionT1;
			bb8[6] = DD2Event.DownedInvasionT2;
			bb8[7] = DD2Event.DownedInvasionT3;
			binaryWriter.Write(bb8);
			binaryWriter.Write((sbyte)Main.invasionType);
			binaryWriter.Write(SocialAPI.Network != null ? SocialAPI.Network.GetLobbyId() : 0UL);
			binaryWriter.Write(Sandstorm.IntendedSeverity);

			var currentPosition = (int)binaryWriter.BaseStream.Position;
			binaryWriter.BaseStream.Position = position;
			binaryWriter.Write((short)currentPosition);
			binaryWriter.BaseStream.Position = currentPosition;
			var data = memoryStream.ToArray();

			binaryWriter.Close();

			return data;
		}

		private static void SendInfo(TSPlayer player, bool ssc)
		{
			if (!SendDataHooks.InvokePreSendData(player)) return;
			player.SendRawData(PackInfo(ssc));
			SendDataHooks.InvokePostSendData(player);
		}

		/// <summary>
		/// 变换存档
		/// 需要区域被设定。这也就是说，ssc是开着的
		/// </summary>
		/// <param name="player">玩家引用</param>
		/// <param name="data">存档信息</param>
		public static void ChangeCharacter(TSPlayer player, PlayerData data)
		{
			if (!player.GetData<bool>(InRegion)) // 是否备份存档判断
			{
				var origin = new PlayerData(player);
				origin.CopyCharacter(player);

				player.SetData(OriginData, origin);
			}

			Change(player, data);

			player.SetData(InRegion, true);
		}

		
	}
}
