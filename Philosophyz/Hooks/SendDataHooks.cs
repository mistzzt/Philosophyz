using OTAPI;
using TShockAPI;

namespace Philosophyz.Hooks
{
	public class SendDataHooks
	{
		public delegate HookResult PzSendData(TSPlayer player, bool allMsg);

		public static PzSendData PreSendData;

		public static PzSendData PostSendData;

		internal static bool InvokePreSendData(TSPlayer player, bool allMsg = false)
		{
			var sd = PreSendData;
			var hookResult = sd != null ? new HookResult?(sd(player, allMsg)) : null;
			return hookResult == HookResult.Continue;
		}

		internal static void InvokePostSendData(TSPlayer player, bool allMsg = false)
		{
			var sd = PostSendData;
			sd?.Invoke(player, allMsg);
		}
	}
}
