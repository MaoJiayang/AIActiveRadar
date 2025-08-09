using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI.Ingame;
using Sandbox.ModAPI.Interfaces;
using SpaceEngineers.Game.ModAPI.Ingame;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using VRage;
using VRage.Collections;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.GUI.TextPanel;
using VRage.Game.ModAPI.Ingame;
using VRage.Game.ModAPI.Ingame.Utilities;
using VRage.Game.ObjectBuilders.Definitions;
using VRageMath;

namespace IngameScript
{
    public partial class Program : MyGridProgram
    {
        private IMyChatBroadcastControllerComponent _chat;  // 聊天广播组件
        private AI雷达 _radar;
        private 参数管理器 _paramMgr;
        private IMyFlightMovementBlock _flightBlock;
        private IMyOffensiveCombatBlock _combatBlock;

        public Program()
        {
            // 1. 读取/写入自定义数据
            string config = Me.CustomData;
            if (string.IsNullOrWhiteSpace(config))
            {
                _paramMgr = new 参数管理器();
                Me.CustomData = _paramMgr.生成配置字符串();
            }
            else
            {
                _paramMgr = new 参数管理器(config);
            }

            // 2. 搜索AI块
            var flightBlocks = new List<IMyFlightMovementBlock>();
            GridTerminalSystem.GetBlocksOfType(flightBlocks, b => b.CubeGrid == Me.CubeGrid);
            var combatBlocks = new List<IMyOffensiveCombatBlock>();
            GridTerminalSystem.GetBlocksOfType(combatBlocks, b => b.CubeGrid == Me.CubeGrid);

            _flightBlock = flightBlocks.Count > 0 ? flightBlocks[0] : null;
            _combatBlock = combatBlocks.Count > 0 ? combatBlocks[0] : null;

            if (_flightBlock != null && _combatBlock != null)
            {
                _radar = new AI雷达(_flightBlock, _combatBlock, _paramMgr);
                Runtime.UpdateFrequency = UpdateFrequency.Update1;
            }
            else
            {
                Echo("未找到AI飞行块或战斗块，脚本未启动。");
            }
        }

        public void Save()
        {

        }

        public void Main(string argument, UpdateType updateSource)
        {
            if (_radar == null)
            {
                Echo("AI雷达未初始化。");
                return;
            }

            // 更新雷达
            _radar.Update();

            // 打印雷达自身信息
            Echo(_radar.GetRadarStatus());

            // 打印所有目标id和距离
            var allTargets = _radar.GetAllTargets();
            var myPos = Me.GetPosition();
            Echo("目标列表:");
            foreach (var kv in allTargets)
            {
                var t = kv.Value;
                double dist = Vector3D.Distance(myPos, t.Position);
                Echo($"ID:{t.Id} 距离:{dist:F1}m");
            }
        }
    }
}
