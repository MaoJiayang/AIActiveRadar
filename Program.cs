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
using IngameScript;

namespace IngameScript
{
    public partial class Program : MyGridProgram
    {
        private AI雷达 _radar;
        private 参数管理器 参数们;
        private IMyFlightMovementBlock _flightBlock;
        private List<IMyOffensiveCombatBlock> _combatBlocks;
        private HUD显示系统 _hud系统;
        private 陀螺仪瞄准系统 辅助瞄准;

        #region 性能统计变量
        private double 总运行时间毫秒;
        private int 运行次数;
        private double 最大运行时间毫秒;
        private StringBuilder 性能统计信息 = new StringBuilder();
        private string 性能统计信息缓存 = string.Empty;
        #endregion

        #region 状态变量
        private bool 辅助瞄准开启 = false;
        #endregion

        public Program()
        {
            // 1. 读取/写入自定义数据
            参数们 = new 参数管理器(Me);

            // 2. 搜索编组
            IMyBlockGroup 雷达组 = GridTerminalSystem.GetBlockGroupWithName(参数们.AI雷达组名);
            var flightBlocks = new List<IMyFlightMovementBlock>();
            雷达组.GetBlocksOfType(flightBlocks);
            var combatBlocks = new List<IMyOffensiveCombatBlock>();
            雷达组.GetBlocksOfType(combatBlocks);

            _flightBlock = flightBlocks.Count > 0 ? flightBlocks[0] : null;
            _combatBlocks = combatBlocks;

            if (_flightBlock != null && _combatBlocks != null && _combatBlocks.Count > 0)
            {
                _radar = new AI雷达(_flightBlock, _combatBlocks, 参数们);
                Runtime.UpdateFrequency = UpdateFrequency.Update1;
            }
            else
            {
                Echo("未找到AI飞行块或战斗块，脚本未启动。");
            }
            _hud系统 = new HUD显示系统(参数们);
            辅助瞄准 = new 陀螺仪瞄准系统(参数们);
            _hud系统.初始化(雷达组);
            辅助瞄准.初始化(雷达组);
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
            处理参数(argument);
            // 更新雷达
            bool 有新目标 = _radar.Update(Echo);
            // // 打印雷达自身信息
            // Echo(_radar.GetRadarStatus());

            // 打印所有目标id和距离
            var allTargets = _radar.GetAllRawTargets();
            var myPos = Me.GetPosition();
            Echo("目标列表:");
            foreach (var kv in allTargets)
            {
                var t = kv.Value;
                double dist = Vector3D.Distance(myPos, t.Position);
                Echo($"===ID:{t.Id}===\n距离:{dist:F1}m\n状态:{t.State}\n上次更新:{t.LastUpdateTick}");
            }
            Echo($"已激活HUD: {_hud系统.已初始化}");
            if(!_hud系统.已初始化)
            {
                Echo(_hud系统.初始化消息);
            }
            Echo($"辅助瞄准已{(辅助瞄准开启 ? "开启" : "关闭")}");
            // Me.CustomName = $"辅助瞄准: {(辅助瞄准开启 ? "开" : "关")}\n弹速: {参数们.武器弹速}m/s";
            if (!辅助瞄准.已初始化)
            {
                Echo(辅助瞄准.初始化消息);
            }
            else 辅助瞄准.参考驾驶舱.Update();
            if (_hud系统.已初始化)
            {
                // 准备数据：id->预测位置
                Dictionary<long, SimpleTargetInfo> raw = _radar.GetConfirmedTargetsPredicted();
                Dictionary<long, TargetTracker> trackers = _radar.GetConfirmedTargetTrackers();
                弹道显示信息 弹道显示 = null;
                Vector3D 弹道预测点 = Vector3D.Zero;
                if (raw.ContainsKey(_hud系统.视线选定目标ID))
                {
                    double 弹道拦截时间;
                    弹道预测点 = _radar.计算弹道(_hud系统.视线选定目标ID,
                                                参数们.武器弹速列表[参数们.当前所选弹速索引],
                                                _hud系统.参考驾驶舱,
                                                out 弹道拦截时间,
                                                弹药受重力影响: true);
                    // Echo($"拦截时间: {弹道拦截时间:F1}秒");
                    // double 组合误差 = trackers[_hud系统.视线选定目标ID].combinationError;
                    // double 线性权重 = trackers[_hud系统.视线选定目标ID].linearWeight;
                    // double 圆周权重 = trackers[_hud系统.视线选定目标ID].circularWeight;
                    // Echo($"组合误差: {组合误差:F1}米");
                    // Echo($"线性权重：{线性权重:F3}，圆周权重：{圆周权重:F3}");
                    弹道显示 = new 弹道显示信息(弹道预测点, 弹道拦截时间, trackers[_hud系统.视线选定目标ID]);
                    // Echo($"弹道预测落点：X {弹道预测点.X:F1} Y {弹道预测点.Y:F1} Z {弹道预测点.Z:F1}");
                    // Echo($"弹道历史记录数量：{trackers[_hud系统.视线选定目标ID].GetHistoryCount()}");
                    if (辅助瞄准开启 && 辅助瞄准.已初始化 && 弹道预测点 != Vector3D.Zero)
                    {
                        辅助瞄准.点瞄准(弹道预测点);
                    }
                }
                else 辅助瞄准.重置();
                _hud系统.更新视线选定目标ID(raw, 弹道预测点);
                _hud系统.绘制(raw, 弹道显示);
            }

            更新性能统计信息();
            if (运行次数 % 20 == 0) 性能统计信息缓存 = 性能统计信息.ToString();
            Echo(性能统计信息缓存);
        }
        private void 处理参数(string argument)
        {
            if (string.IsNullOrWhiteSpace(argument))
                return;
            if (argument == "toggle_aim")
            {
                辅助瞄准开启 = !辅助瞄准开启;
                辅助瞄准.重置();
                return;
            }
            if (argument == "switch_weapon")
            {
                参数们.当前所选弹速索引++;
                return;
            }

        }

        #region 性能统计
        /// <summary>
        /// 更新运行性能统计信息
        /// </summary>
        private void 更新性能统计信息()
        {
            // 更新运行时间统计
            double 上次运行时间毫秒 = Runtime.LastRunTimeMs;

            总运行时间毫秒 += 上次运行时间毫秒;
            运行次数++;
            if (运行次数 % 600 == 0)
            {
                // 每600次运行重置统计信息
                总运行时间毫秒 = 0;
                运行次数 = 0;
                最大运行时间毫秒 = 0;
            }
            if (上次运行时间毫秒 > 最大运行时间毫秒)
                最大运行时间毫秒 = 上次运行时间毫秒;
            // 计算平均运行时间
            double 平均运行时间毫秒 = 总运行时间毫秒 / 运行次数;

            // 清空并重新构建性能统计信息
            性能统计信息.Clear();
            性能统计信息.AppendLine("=== 性能统计 ===");
            性能统计信息.AppendLine($"上次运行: {上次运行时间毫秒:F3} ms");
            性能统计信息.AppendLine($"平均运行: {平均运行时间毫秒:F3} ms");
            性能统计信息.AppendLine($"最大运行: {最大运行时间毫秒:F3} ms");
            性能统计信息.AppendLine($"运行次数: {运行次数}");
            性能统计信息.AppendLine($"指令使用: {Runtime.CurrentInstructionCount}/{Runtime.MaxInstructionCount}");
            性能统计信息.AppendLine("================");
        }
        #endregion
    }
}
