using System.Globalization;
using Jotunn.Entities;
using Jotunn.Managers;
using UnityEngine;

namespace ImmersiveMapper.TraderBeacons
{
    /// <summary>Console commands for testing. tb_test reveals nothing; tb_camps and tb_resetfound need devcommands + admin.</summary>
    internal static class DebugCommands
    {
        public static void Register()
        {
            CommandManager.Instance.AddConsoleCommand(new TestCommand());
            CommandManager.Instance.AddConsoleCommand(new CampsCommand());
            CommandManager.Instance.AddConsoleCommand(new ResetFoundCommand());
        }

        public static void RPC_CampsReply(long sender, string text)
        {
            foreach (string line in text.Split('\n'))
            {
                Console.instance?.Print(line);
            }
        }

        private sealed class TestCommand : ConsoleCommand
        {
            private const float TestSeconds = 120f;

            public override string Name => "tb_test";

            public override string Help => "[meters=800] [smoke|fireworks] - show a trader signal ahead of you for 2 minutes, to check how visible it is";

            public override void Run(string[] args)
            {
                Player player = Player.m_localPlayer;
                if (player == null)
                {
                    return;
                }
                float meters = 800f;
                if (args.Length > 0 && !float.TryParse(args[0], NumberStyles.Float, CultureInfo.InvariantCulture, out meters))
                {
                    meters = 800f;
                }
                BeaconSignal.Mode mode = BeaconSignal.Mode.Auto;
                if (args.Length > 1)
                {
                    mode = args[1].StartsWith("s") ? BeaconSignal.Mode.Smoke : args[1].StartsWith("f") ? BeaconSignal.Mode.Fireworks : BeaconSignal.Mode.Auto;
                }
                Transform view = GameCamera.instance != null ? GameCamera.instance.transform : player.transform;
                Vector3 forward = Vector3.ProjectOnPlane(view.forward, Vector3.up).normalized;
                Vector3 position = player.transform.position + forward * meters;
                BeaconSignal.Create("test", position, mode, TestSeconds);
                Console.instance.Print($"TraderBeacons: test signal ({mode}) {meters:0} m ahead for {TestSeconds:0} s.");
                Camera camera = GameCamera.instance != null ? GameCamera.instance.m_camera : null;
                string sight = $"fog {(RenderSettings.fog ? "on" : "off")}, {RenderSettings.fogMode}, density {RenderSettings.fogDensity:0.#####}"
                    + (camera != null ? $", camera draws up to {camera.farClipPlane:0} m" : "");
                Console.instance.Print($"TraderBeacons: {sight}");
                Plugin.Log.LogInfo($"tb_test {meters:0} m {mode}: {sight}; fog as if {BeaconConfig.FogAsIf.Value:0} m");
            }
        }

        private sealed class CampsCommand : ConsoleCommand
        {
            public override string Name => "tb_camps";

            public override string Help => "List trader camps and candidate spots with distance and bearing (spoiler, admin only)";

            public override bool IsCheat => true;

            public override void Run(string[] args)
            {
                ZRoutedRpc.instance?.InvokeRoutedRPC(Rpc.CampsRequest);
            }
        }

        private sealed class ResetFoundCommand : ConsoleCommand
        {
            public override string Name => "tb_resetfound";

            public override string Help => "Forget which traders you have found, so their signals start again (admin only)";

            public override bool IsCheat => true;

            public override void Run(string[] args)
            {
                ZRoutedRpc.instance?.InvokeRoutedRPC(Rpc.ResetFound);
            }
        }
    }
}
