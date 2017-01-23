using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Terraria;
using TerrariaApi.Server;
using TShockAPI;
using TShockAPI.Hooks;

namespace Philosophyz
{
	[ApiVersion(2, 0)]
	public class Philosophyz : TerrariaPlugin
	{
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

			RegionHooks.RegionEntered += OnRegionEntered;
			RegionHooks.RegionLeft += OnRegionLeft;
			RegionHooks.RegionDeleted += OnRegionDeleted;
		}

		private void OnPostInit(EventArgs args)
		{
			PzRegions.ReloadRegions();
		}

		private void OnInit(EventArgs args)
		{
			Commands.ChatCommands.Add(new Command("pz.manage", PzCmd, "pz"));

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

			//
		}

		private void OnRegionLeft(RegionHooks.RegionLeftEventArgs args)
		{
			if (PzRegions.PzRegions.All(p => p.Id != args.Region.ID))
				return;

			//
		}

		private void OnRegionEntered(RegionHooks.RegionEnteredEventArgs args)
		{
			if (PzRegions.PzRegions.All(p => p.Id != args.Region.ID))
				return;

			//
		}

		private void PzCmd(CommandArgs args)
		{
			var cmd = args.Parameters.Count == 0 ? "HELP" : args.Parameters[0].ToUpperInvariant();

			switch (cmd)
			{
				case "ADD":

				case "LIST":
				case "REMOVE":
				case "SHOW":
				case "RESTORE":
				case "HELP":
					break;
				default:
					args.Player.SendErrorMessage("语法无效! 键入 /pz help 以获取帮助.");
					return;
			}
		}
	}
}
