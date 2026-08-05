using UnityEngine;
using LunaMultiplayer.KSP2.Network;

namespace LunaMultiplayer.KSP2.Core
{
    /// <summary>
    /// 游戏内 UI（Unity IMGUI，零外部依赖，在 KSP2 / Redux 下始终可渲染）。
    /// 按 <b>F7</b> 开关窗口。提供：
    ///   - 创建服务器并加入（host：在本机起中继服务端 + 自身 loopback 接入）
    ///   - 加入好友服务器（填好友 IP + 端口）
    ///   - 断开
    ///   - 实时连接状态 + 最近日志
    /// 网络层调用 <see cref="NetworkConnection"/>（Host/Connect/Disconnect），
    /// 服务端逻辑在 <see cref="RelayServer"/>（内嵌于本 mod 的中继模式）。
    /// </summary>
    public class Lmp2Ui : MonoBehaviour
    {
        private bool _show;
        private int _tab; // 0 = 创建服务器, 1 = 加入服务器
        private string _hostPort = "8800";
        private string _clientIp = "127.0.0.1";
        private string _clientPort = "8800";
        private Vector2 _logScroll;

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.F7))
                _show = !_show;
        }

        private void OnGUI()
        {
            if (!_show) return;
            // 固定 ID 防重复窗口；位置避开左上 KSP2 原生 HUD
            GUILayout.Window(987654, new Rect(24, 90, 380, 460), DrawWindow, "LunaMultiplayer KSP2  (F7 开关)");
        }

        private void DrawWindow(int id)
        {
            bool hosting = RelayServer.IsRunning;
            bool connected = NetworkConnection.IsConnected;

            string status = !hosting && !connected ? "○ 未连接"
                : (hosting ? "● 服务器运行中" : "") + (connected ? " ● 已连接" : "");
            GUILayout.Label(status);

            GUILayout.BeginHorizontal();
            if (GUILayout.Toggle(_tab == 0, "创建服务器")) _tab = 0;
            if (GUILayout.Toggle(_tab == 1, "加入服务器")) _tab = 1;
            GUILayout.EndHorizontal();

            if (_tab == 0)
            {
                GUILayout.Label("端口");
                _hostPort = GUILayout.TextField(_hostPort, 10);
                if (GUILayout.Button(hosting ? "关闭服务器" : "创建服务器并加入"))
                {
                    if (hosting)
                    {
                        NetworkConnection.Disconnect();
                        RelayServer.Stop();
                    }
                    else if (int.TryParse(_hostPort, out int p))
                    {
                        NetworkConnection.Host(p);
                    }
                }
                GUILayout.Label("创建后把你的公网/局域网 IP 告诉好友，好友用「加入服务器」连此端口即可。");
            }
            else
            {
                GUILayout.Label("服务器 IP");
                _clientIp = GUILayout.TextField(_clientIp, 40);
                GUILayout.Label("端口");
                _clientPort = GUILayout.TextField(_clientPort, 10);
                if (GUILayout.Button(connected ? "断开" : "连接"))
                {
                    if (connected)
                        NetworkConnection.Disconnect();
                    else if (int.TryParse(_clientPort, out int p))
                        NetworkConnection.Connect(_clientIp, p);
                }
            }

            GUILayout.Space(6);
            GUILayout.Label("日志:");
            _logScroll = GUILayout.BeginScrollView(_logScroll, GUILayout.Height(230));
            lock (Ksp2Logger.Lock)
            {
                int start = Mathf.Max(0, Ksp2Logger.Recent.Count - 60);
                for (int i = start; i < Ksp2Logger.Recent.Count; i++)
                    GUILayout.Label(Ksp2Logger.Recent[i]);
            }
            GUILayout.EndScrollView();
        }
    }
}
