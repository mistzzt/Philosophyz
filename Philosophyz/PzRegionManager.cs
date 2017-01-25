using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using MySql.Data.MySqlClient;
using Terraria;
using TShockAPI;
using TShockAPI.DB;

namespace Philosophyz
{
	public class PzRegion
	{
		public int Id { get; set; }

		public PlayerData PlayerData { get; set; }
	}

	internal class PzRegionManager
	{
		public List<PzRegion> PzRegions = new List<PzRegion>();

		private readonly IDbConnection _database;

		public PzRegionManager(IDbConnection db)
		{
			_database = db;

			var table = new SqlTable("PzRegions",
									 new SqlColumn("Region", MySqlDbType.Int32) { Primary = true },
									 new SqlColumn("Health", MySqlDbType.Int32),
									 new SqlColumn("MaxHealth", MySqlDbType.Int32),
									 new SqlColumn("Mana", MySqlDbType.Int32),
									 new SqlColumn("MaxMana", MySqlDbType.Int32),
									 new SqlColumn("Inventory", MySqlDbType.Text),
									 new SqlColumn("extraSlot", MySqlDbType.Int32),
									 new SqlColumn("spawnX", MySqlDbType.Int32),
									 new SqlColumn("spawnY", MySqlDbType.Int32),
									 new SqlColumn("skinVariant", MySqlDbType.Int32),
									 new SqlColumn("hair", MySqlDbType.Int32),
									 new SqlColumn("hairDye", MySqlDbType.Int32),
									 new SqlColumn("hairColor", MySqlDbType.Int32),
									 new SqlColumn("pantsColor", MySqlDbType.Int32),
									 new SqlColumn("shirtColor", MySqlDbType.Int32),
									 new SqlColumn("underShirtColor", MySqlDbType.Int32),
									 new SqlColumn("shoeColor", MySqlDbType.Int32),
									 new SqlColumn("hideVisuals", MySqlDbType.Int32),
									 new SqlColumn("skinColor", MySqlDbType.Int32),
									 new SqlColumn("eyeColor", MySqlDbType.Int32),
									 new SqlColumn("questsCompleted", MySqlDbType.Int32)
				);
			var creator = new SqlTableCreator(db,
											  db.GetSqlType() == SqlType.Sqlite
												  ? (IQueryBuilder)new SqliteQueryCreator()
												  : new MysqlQueryCreator());
			creator.EnsureTableStructure(table);
		}

		public void ReloadRegions()
		{
			PzRegions.Clear();

			using (var reader = _database.QueryReader("SELECT PzRegions.* FROM PzRegions, Regions WHERE PzRegions.Region = Regions.Id AND Regions.WorldID = @0", Main.worldID))
			{
				while (reader != null && reader.Read())
				{
					var region = new PzRegion
					{
						Id = reader.Get<int>("Region"),
						PlayerData = Read(reader)
					};

					PzRegions.Add(region);
				}
			}
		}

		public void AddRegion(int id, PlayerData data)
		{
			if (PzRegions.All(p => p.Id != id))
				AddRegionInternal(id, data);
			else
				UpdateRegion(id, data);
		}

		public void RemoveRegion(int id)
		{
			PzRegions.RemoveAll(p => p.Id == id);
			try
			{
				_database.Query("DELETE FROM PzRegions WHERE Region=@0;", id);
			}
			catch (Exception ex)
			{
				TShock.Log.Error(ex.ToString());
			}
		}

		private void UpdateRegion(int id, PlayerData playerData)
		{
			try
			{
				_database.Query(
					"UPDATE PzRegions SET Health = @0, MaxHealth = @1, Mana = @2, MaxMana = @3, Inventory = @4, spawnX = @6, spawnY = @7, hair = @8, hairDye = @9, hairColor = @10, pantsColor = @11, shirtColor = @12, underShirtColor = @13, shoeColor = @14, hideVisuals = @15, skinColor = @16, eyeColor = @17, questsCompleted = @18, skinVariant = @19, extraSlot = @20 WHERE Region = @5;",
					playerData.health,
					playerData.maxHealth,
					playerData.mana,
					playerData.maxMana,
					string.Join("~", playerData.inventory),
					id,
					-1,//player.TPlayer.SpawnX,
					-1,//player.TPlayer.SpawnY,
					-1,//player.TPlayer.hair,
					-1,//player.TPlayer.hairDye,
					-1,//TShock.Utils.EncodeColor(player.TPlayer.hairColor),
					-1,//TShock.Utils.EncodeColor(player.TPlayer.pantsColor),
					-1,//.Utils.EncodeColor(player.TPlayer.shirtColor),
					-1,//TShock.Utils.EncodeColor(player.TPlayer.underShirtColor),
					-1,//TShock.Utils.EncodeColor(player.TPlayer.shoeColor),
					-1,//TShock.Utils.EncodeBoolArray(player.TPlayer.hideVisual),
					-1,//.Utils.EncodeColor(player.TPlayer.skinColor),
					-1,//TShock.Utils.EncodeColor(player.TPlayer.eyeColor),
					-1,//player.TPlayer.anglerQuestsFinished,
					-1,//player.TPlayer.skinVariant,
					-1//player.TPlayer.extraAccessory ? 1 : 0
				);
			}
			catch (Exception ex)
			{
				TShock.Log.Error(ex.ToString());
			}
		}

		private void AddRegionInternal(int id, PlayerData playerData)
		{
			try
			{
				_database.Query(
					"INSERT INTO PzRegions(Region, Health, MaxHealth, Mana, MaxMana, Inventory, extraSlot, spawnX, spawnY, skinVariant, hair, hairDye, hairColor, pantsColor, shirtColor, underShirtColor, shoeColor, hideVisuals, skinColor, eyeColor, questsCompleted) VALUES(@0, @1, @2, @3, @4, @5, @6, @7, @8, @9, @10, @11, @12, @13, @14, @15, @16, @17, @18, @19, @20); ",
					id,
					playerData.health,
					playerData.maxHealth,
					playerData.mana,
					playerData.maxMana,
					string.Join("~", playerData.inventory),
					playerData.extraSlot,
					-1, //player.TPlayer.SpawnX,
					-1, //player.TPlayer.SpawnY,
					-1, //player.TPlayer.skinVariant,
					-1, //player.TPlayer.hair,
					-1, //player.TPlayer.hairDye,
					-1, //TShock.Utils.EncodeColor(player.TPlayer.hairColor),
					-1, //TShock.Utils.EncodeColor(player.TPlayer.pantsColor),
					-1, //TShock.Utils.EncodeColor(player.TPlayer.shirtColor),
					-1, //TShock.Utils.EncodeColor(player.TPlayer.underShirtColor),
					-1, //TShock.Utils.EncodeColor(player.TPlayer.shoeColor),
					-1, //TShock.Utils.EncodeBoolArray(player.TPlayer.hideVisual),
					-1, //TShock.Utils.EncodeColor(player.TPlayer.skinColor),
					-1, //TShock.Utils.EncodeColor(player.TPlayer.eyeColor),
					-1  //player.TPlayer.anglerQuestsFinished);
				);
			}
			catch (Exception ex)
			{
				TShock.Log.Error(ex.ToString());
			}
		}

		private static PlayerData Read(QueryResult reader)
		{
			var inventory = reader.Get<string>("Inventory").Split('~').Select(NetItem.Parse).ToList();
			if (inventory.Count < NetItem.MaxInventory)
			{
				//TODO: unhardcode this - stop using magic numbers and use NetItem numbers
				//Set new armour slots empty
				inventory.InsertRange(67, new NetItem[2]);
				//Set new vanity slots empty
				inventory.InsertRange(77, new NetItem[2]);
				//Set new dye slots empty
				inventory.InsertRange(87, new NetItem[2]);
				//Set the rest of the new slots empty
				inventory.AddRange(new NetItem[NetItem.MaxInventory - inventory.Count]);
			}

			var playerData = new PlayerData(null)
			{
				exists = true,
				health = reader.Get<int>("Health"),
				maxHealth = reader.Get<int>("MaxHealth"),
				mana = reader.Get<int>("Mana"),
				maxMana = reader.Get<int>("MaxMana"),
				inventory = inventory.ToArray(),
				extraSlot = reader.Get<int>("extraSlot"),
				spawnX = reader.Get<int>("spawnX"),
				spawnY = reader.Get<int>("spawnY"),
				skinVariant = reader.Get<int?>("skinVariant"),
				hair = reader.Get<int?>("hair"),
				hairDye = (byte)reader.Get<int>("hairDye"),
				hairColor = TShock.Utils.DecodeColor(reader.Get<int?>("hairColor")),
				pantsColor = TShock.Utils.DecodeColor(reader.Get<int?>("pantsColor")),
				shirtColor = TShock.Utils.DecodeColor(reader.Get<int?>("shirtColor")),
				underShirtColor = TShock.Utils.DecodeColor(reader.Get<int?>("underShirtColor")),
				shoeColor = TShock.Utils.DecodeColor(reader.Get<int?>("shoeColor")),
				hideVisuals = TShock.Utils.DecodeBoolArray(reader.Get<int?>("hideVisuals")),
				skinColor = TShock.Utils.DecodeColor(reader.Get<int?>("skinColor")),
				eyeColor = TShock.Utils.DecodeColor(reader.Get<int?>("eyeColor")),
				questsCompleted = reader.Get<int>("questsCompleted")
			};

			return playerData;
		}
	}
}
