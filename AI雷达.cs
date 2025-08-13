using Sandbox.ModAPI.Ingame;
using VRage.Game.ModAPI.Ingame;
using VRageMath;
using System.Collections.Generic;
using System.Linq;
using System;

namespace IngameScript
{
    /// <summary>
    /// 雷达目标信息结构
    /// </summary>
    public struct RadarTarget
    {
        public int Id;
        public Vector3D Position;
        public long LastUpdateTick;
        public int ConfirmationLevel; // 确认级别 0-未确认, 1-暂定, 2-已确认
        public TargetState State; // 目标状态

        public RadarTarget(int id, Vector3D position, long updateTime)
        {
            Id = id;
            Position = position;
            LastUpdateTick = updateTime;
            ConfirmationLevel = 0;
            State = TargetState.Detected;
        }
    }

    /// <summary>
    /// 目标状态枚举
    /// </summary>
    public enum TargetState
    {
        Detected,    // 刚检测到
        Tentative,   // 暂定目标
        Confirmed,   // 已确认目标
        Tracking,    // 正在跟踪
        Lost         // 丢失目标
    }

    /// <summary>
    /// AI雷达系统 - 基于现有AI索敌改造
    /// </summary>
    public class AI雷达
    {
        private IMyFlightMovementBlock 飞行块;
        private List<IMyOffensiveCombatBlock> 战斗块列表;
        private 参数管理器 参数们;
        
        // 目标管理
        private Dictionary<int, RadarTarget> 跟踪目标表 = new Dictionary<int, RadarTarget>();
        private Dictionary<int, TargetTracker> 目标跟踪器表 = new Dictionary<int, TargetTracker>();
        
        // 优先级轮换
        private OffensiveCombatTargetPriority[] 优先级序列 = new[] {
            OffensiveCombatTargetPriority.Closest,
            OffensiveCombatTargetPriority.Largest,
            OffensiveCombatTargetPriority.Smallest
        };
        private int 当前优先级索引 = 0;
        
        // 多攻击块管理
        private int 当前攻击块索引 = 0;
        
        // 计数器和参数
        private long 帧计数 = 0;
        public long 当前时间戳ms { get { return 帧转毫秒(帧计数); } }
        private int 下一个目标ID = 1;

        public AI雷达(IMyFlightMovementBlock flightBlock, List<IMyOffensiveCombatBlock> combatBlocks, 参数管理器 参数管理器)
        {
            飞行块 = flightBlock;
            战斗块列表 = combatBlocks ?? new List<IMyOffensiveCombatBlock>();
            参数们 = 参数管理器;
            InitAI();
        }

        /// <summary>
        /// 初始化AI系统
        /// </summary>
        private void InitAI()
        {
            if (飞行块 == null || 战斗块列表 == null || 战斗块列表.Count == 0)
                return;
                
            // 配置飞行AI
            if (飞行块 != null)
            {
                飞行块.SpeedLimit = 参数们.最大速度限制;
                飞行块.AlignToPGravity = false;
                飞行块.Enabled = false;
                飞行块.ApplyAction("ActivateBehavior_On");
            }

            // 配置所有战斗AI块
            for (int i = 0; i < 战斗块列表.Count; i++)
            {
                var 战斗块 = 战斗块列表[i];
                if (战斗块 != null)
                {
                    战斗块.TargetPriority = 参数们.目标优先级;
                    战斗块.UpdateTargetInterval = 参数们.战斗块更新间隔正常;
                    战斗块.Enabled = true;
                    战斗块.SelectedAttackPattern = 参数们.战斗块攻击模式;
                    
                    // 只激活第一个攻击块，其他的关闭
                    if (i == 当前攻击块索引)
                    {
                        战斗块.ApplyAction("ActivateBehavior_On");
                    }
                    // else
                    // {
                    //     战斗块.ApplyAction("ActivateBehavior_Off");
                    // }

                    // IMyAttackPatternComponent 攻击模式;
                    // if (战斗块.TryGetSelectedAttackPattern(out 攻击模式))
                    // {
                    //     IMyOffensiveCombatIntercept 拦截模式 = 攻击模式 as IMyOffensiveCombatIntercept;
                    //     if (拦截模式 != null)
                    //     {
                    //         拦截模式.GuidanceType = GuidanceType.Basic;
                    //     }
                    // }
                }
            }
        }

        /// <summary>
        /// 主更新方法
        /// </summary>
        /// <returns>是否检测到新目标</returns>
        public bool Update()
        {
            帧计数++;
            bool 有新目标 = false;

            // 1. 获取当前检测数据
            Vector3D 当前检测 = 获取当前检测数据();
            
            // 2. 数据关联与目标更新
            有新目标 = 处理检测数据(当前检测);
            
            // 3. 更新目标跟踪器
            更新目标跟踪器();
            
            // 4. 目标状态管理
            管理目标状态();
            
            // 6. 切换检测优先级
            切换检测优先级();
            
            return 有新目标;
        }

        /// <summary>
        /// 获取当前检测数据
        /// </summary>
        /// <returns>当前检测数据（优先级: 位置）</returns>
        private Vector3D 获取当前检测数据()
        {
            // 获取AI块当前检测的目标
            var 路径点列表 = new List<IMyAutopilotWaypoint>();
            飞行块.GetWaypoints(路径点列表);
            if(路径点列表.Count == 0)
            {
                // 如果没有路径点，返回0
                return Vector3D.Zero;
            }
            var 路径点 = 路径点列表[路径点列表.Count - 1];
            var m = 路径点.Matrix;
            return new Vector3D(m.M41, m.M42, m.M43);
        }

        /// <summary>
        /// 处理检测数据，进行目标关联
        /// </summary>
        private bool 处理检测数据(Vector3D 当前检测)
        {
            bool 有新目标 = false;
            if (当前检测 == Vector3D.Zero) return false;
            // 检查是否与已有目标关联
            int 关联目标ID = _查找关联目标(当前检测);
            
            if (关联目标ID != -1)
            {
                // 更新已有目标
                _更新已有目标(关联目标ID, 当前检测);
            }
            else
            {
                // 创建新目标
                _创建新目标(当前检测);
                有新目标 = true;
            }
            
            return 有新目标;
        }

        /// <summary>
        /// 查找与给定位置关联的目标ID
        /// </summary>
        private int _查找关联目标(Vector3D 位置)
        {
            double 最小距离 = 参数们.目标关联距离阈值;
            int 最佳匹配ID = -1;
            
            foreach (var 目标项 in 跟踪目标表)
            {
                var 目标 = 目标项.Value;
                // if (目标.State == TargetState.Lost)
                //     continue;
                    
                // 计算位置距离
                double 距离 = Vector3D.Distance(目标.Position, 位置);
                long 时间差 = 帧计数 - 目标.LastUpdateTick;

                if (距离 < 1e-6)
                {
                    // 为坐标不变的目标保持一个最小更新频率
                    if (时间差 < 参数们.目标丢失超时帧数 - 3)
                        return -114514;
                    else return 目标.Id;
                }
                    
                // 如果有跟踪器，使用预测位置进行更精确的关联
                if (目标跟踪器表.ContainsKey(目标.Id))
                {
                    var 跟踪器 = 目标跟踪器表[目标.Id];

                    var 预测位置 = 跟踪器.PredictFutureTargetInfo(帧转毫秒(时间差));
                    距离 = Vector3D.Distance(预测位置.Position, 位置);
                }

                if (距离 < 最小距离)
                {
                    最小距离 = 距离;
                    最佳匹配ID = 目标.Id;
                }
            }
            
            return 最佳匹配ID;
        }

        /// <summary>
        /// 更新已有目标
        /// </summary>
        private void _更新已有目标(int 目标ID, Vector3D 位置)
        {
            if (!跟踪目标表.ContainsKey(目标ID))
                return;
                
            var 目标 = 跟踪目标表[目标ID];
            
            // 更新目标信息
            目标.Position = 位置;
            目标.LastUpdateTick = 帧计数;
            
            // 提升确认级别
            if (目标.ConfirmationLevel < 参数们.目标确认帧数)
            {
                目标.ConfirmationLevel++;
                if (目标.ConfirmationLevel >= 参数们.目标确认帧数)
                {
                    目标.State = TargetState.Confirmed;
                }
                else
                {
                    目标.State = TargetState.Tentative;
                }
            }
            else
            {
                目标.State = TargetState.Tracking;
            }
            
            跟踪目标表[目标ID] = 目标;
        }

        /// <summary>
        /// 创建新目标
        /// </summary>
        private void _创建新目标(Vector3D 位置)
        {
            int 新ID = 下一个目标ID++;
            var 新目标 = new RadarTarget(新ID, 位置, 帧计数);
            新目标.ConfirmationLevel = 1;
            新目标.State = TargetState.Detected;
            
            跟踪目标表[新ID] = 新目标;
            
            // 创建对应的目标跟踪器
            var 跟踪器 = new TargetTracker(参数们.目标历史最大长度);
            目标跟踪器表[新ID] = 跟踪器;
        }

        /// <summary>
        /// 更新目标跟踪器
        /// </summary>
        private void 更新目标跟踪器()
        {
            foreach (var 目标项 in 跟踪目标表.ToList())
            {
                int ID = 目标项.Key;
                var 目标 = 目标项.Value;
                
                if (目标.LastUpdateTick != 帧计数) // 只更新当前帧刷新了的目标
                    continue;
                if (目标跟踪器表.ContainsKey(ID))
                {
                    var 跟踪器 = 目标跟踪器表[ID];
                    if (当前时间戳ms - 跟踪器.GetLatestTimeStamp() > 100)
                        跟踪器.UpdateTarget(目标.Position, 当前时间戳ms);
                }
            }
        }

        /// <summary>
        /// 管理目标状态
        /// </summary>
        private void 管理目标状态()
        {
            var 待删除列表 = new List<int>();
            foreach (var 目标项 in 跟踪目标表.ToList())
            {
                int ID = 目标项.Key;
                var 目标 = 目标项.Value;

                long 未更新帧数 = 帧计数 - 目标.LastUpdateTick;

                // 检查目标是否超时
                if (目标.State == TargetState.Tentative && 未更新帧数 > 参数们.暂定目标超时帧数)
                {
                    目标.State = TargetState.Lost;
                }
                else if (目标.State != TargetState.Tentative && 未更新帧数 > 参数们.目标丢失超时帧数)
                {
                    目标.State = TargetState.Lost;
                }
                else if (目标.State == TargetState.Lost && 未更新帧数 < 参数们.目标丢失超时帧数)
                {
                    目标.State = TargetState.Tentative;
                }
                跟踪目标表[ID] = 目标;

                // 准备待删除目标
                if (目标.State == TargetState.Lost)
                {
                    if (未更新帧数 > 参数们.目标丢失超时帧数 * 2) // 双倍超时时间后完全删除
                    {
                        待删除列表.Add(目标项.Key);
                    }
                }
            }

            foreach (int ID in 待删除列表)
            {
                跟踪目标表.Remove(ID);
                目标跟踪器表.Remove(ID);
            }
        }


        /// <summary>
        /// 切换检测优先级
        /// </summary>
        private void 切换检测优先级()
        {
            if (战斗块列表 == null || 战斗块列表.Count == 0)
                return;

            // 切换到下一个攻击块
            当前攻击块索引 = (当前攻击块索引 + 1) % 战斗块列表.Count;

            // 激活新的攻击块并设置优先级
            var 当前战斗块 = 战斗块列表[当前攻击块索引];
            if (当前战斗块 != null)
            {
                当前战斗块.ApplyAction("ActivateBehavior_On");
                
                // 基于攻击块索引和帧计数来计算优先级，确保不同攻击块在不同时间获得不同优先级
                // 使用攻击块索引作为偏移，加上基于帧计数的增量，避免重复模式
                int 优先级计算值 = (当前攻击块索引 + (int)(帧计数 / 战斗块列表.Count)) % 优先级序列.Length;
                当前战斗块.TargetPriority = 优先级序列[优先级计算值];
                
                // 更新当前优先级索引用于状态显示
                当前优先级索引 = 优先级计算值;
            }
        }

        /// <summary>
        /// 将帧数转换为毫秒时间戳
        /// </summary>
        /// <param name="帧"></param>
        /// <returns></returns>
        public long 帧转毫秒(long 帧)
        {
            return (long)Math.Round(帧 * 参数们.时间常数 * 1000);
        }
        #region 对外接口

        /// <summary>
        /// 获取所有跟踪中的目标
        /// </summary>
        /// <returns>目标字典，键为目标ID</returns>
        public Dictionary<int, RadarTarget> GetAllRawTargets()
        {
            return new Dictionary<int, RadarTarget>(跟踪目标表);
        }

        /// <summary>
        /// 获取所有已确认的目标
        /// </summary>
        public Dictionary<int, RadarTarget> GetConfirmedTargets()
        {
            var 已确认目标 = new Dictionary<int, RadarTarget>();
            foreach (var 目标项 in 跟踪目标表)
            {
                if (目标项.Value.State == TargetState.Confirmed || 目标项.Value.State == TargetState.Tracking)
                {
                    已确认目标[目标项.Key] = 目标项.Value;
                }
            }
            return 已确认目标;
        }

        /// <summary>
        /// 获取指定ID的目标信息
        /// </summary>
        public RadarTarget? GetTarget(int targetId)
        {
            if (跟踪目标表.ContainsKey(targetId))
            {
                return 跟踪目标表[targetId];
            }
            return null;
        }

        /// <summary>
        /// 获取目标的上次更新距离当前的帧数
        /// </summary>
        public long GetTargetUpdateAge(int targetId)
        {
            if (跟踪目标表.ContainsKey(targetId))
            {
                return 帧计数 - 跟踪目标表[targetId].LastUpdateTick;
            }
            return -1;
        }

        /// <summary>
        /// 获取所有已确认目标的预测信息
        /// </summary>
        /// <returns>字典，键为目标ID，值为预测的目标信息</returns>
        public Dictionary<int, SimpleTargetInfo> GetConfirmedTargetsPredicted()
        {
            var 预测目标字典 = new Dictionary<int, SimpleTargetInfo>();
            
            foreach (var 目标项 in 跟踪目标表)
            {
                var 目标 = 目标项.Value;
                
                // 只处理已确认和正在跟踪的目标
                if (目标.State == TargetState.Confirmed || 目标.State == TargetState.Tracking)
                {
                    // 计算预测时间（当前帧数 - 上次更新时间）
                    long 预测帧数 = 帧计数 - 目标.LastUpdateTick;
                    long 预测时间ms = 帧转毫秒(预测帧数);
                    
                    // 如果有对应的跟踪器，进行预测
                    if (目标跟踪器表.ContainsKey(目标.Id))
                    {
                        var 跟踪器 = 目标跟踪器表[目标.Id];
                        var 预测信息 = 跟踪器.PredictFutureTargetInfo(预测时间ms, false);
                        预测目标字典[目标.Id] = 预测信息;
                    }
                    else
                    {
                        // 没有跟踪器时，返回当前位置作为预测位置
                        var 当前信息 = new SimpleTargetInfo(
                            目标.Position, 
                            Vector3D.Zero, 
                            Vector3D.Zero, 
                            当前时间戳ms
                        );
                        预测目标字典[目标.Id] = 当前信息;
                    }
                }
            }
            
            return 预测目标字典;
        }

        public Dictionary<int, TargetTracker> GetConfirmedTargetTrackers()
        {
            var 已确认目标跟踪器 = new Dictionary<int, TargetTracker>();
            foreach (var 目标项 in 跟踪目标表)
            {
                // 只添加已确认和正在跟踪的目标
                if (目标项.Value.State == TargetState.Confirmed || 目标项.Value.State == TargetState.Tracking)
                {
                    if (目标跟踪器表.ContainsKey(目标项.Key))
                    {
                        已确认目标跟踪器[目标项.Key] = 目标跟踪器表[目标项.Key];
                    }
                }
            }
            return 已确认目标跟踪器;
        }

        /// <summary>
        /// 预测目标未来位置
        /// </summary>
        /// <param name="targetId">目标ID</param>
        /// <param name="futureTimeMs">预测时间（毫秒）</param>
        /// <returns>预测的目标信息，如果目标不存在返回null</returns>
        public SimpleTargetInfo? PredictTargetPosition(int targetId, long futureTimeMs)
        {
            if (目标跟踪器表.ContainsKey(targetId))
            {
                return 目标跟踪器表[targetId].PredictFutureTargetInfo(futureTimeMs, false);
            }
            return null;
        }

        /// <summary>
        /// 获取目标跟踪器
        /// </summary>
        public TargetTracker GetTargetTracker(int targetId)
        {
            if (目标跟踪器表.ContainsKey(targetId))
            {
                return 目标跟踪器表[targetId];
            }
            return null;
        }

        /// <summary>
        /// 获取雷达状态信息
        /// </summary>
        public string GetRadarStatus()
        {
            int 检测中 = 0, 暂定 = 0, 已确认 = 0, 跟踪中 = 0, 丢失 = 0;
            
            foreach (var 目标 in 跟踪目标表.Values)
            {
                switch (目标.State)
                {
                    case TargetState.Detected: 检测中++; break;
                    case TargetState.Tentative: 暂定++; break;
                    case TargetState.Confirmed: 已确认++; break;
                    case TargetState.Tracking: 跟踪中++; break;
                    case TargetState.Lost: 丢失++; break;
                }
            }

            // 获取当前激活攻击块的优先级
            string 当前优先级 = "无";
            if (战斗块列表 != null && 当前攻击块索引 < 战斗块列表.Count && 战斗块列表[当前攻击块索引] != null)
            {
                当前优先级 = 战斗块列表[当前攻击块索引].TargetPriority.ToString();
            }
            
            return $"雷达状态: 检测中:{检测中} 暂定:{暂定} 已确认:{已确认} 跟踪中:{跟踪中} 丢失:{丢失}\n" +
                   $"当前帧数: {帧计数}\n" +
                   $"攻击块总数: {(战斗块列表?.Count ?? 0)}\n" +
                   $"当前攻击块: {当前攻击块索引}\n" +
                   $"当前优先级: {当前优先级}";
        }

        /// <summary>
        /// 获取当前跟踪目标总数
        /// </summary>
        public int GetActiveTargetCount()
        {
            int 活跃目标数 = 0;
            foreach (var 目标 in 跟踪目标表.Values)
            {
                if (目标.State != TargetState.Lost)
                {
                    活跃目标数++;
                }
            }
            return 活跃目标数;
        }

        /// <summary>
        /// 清空所有目标
        /// </summary>
        public void ClearAllTargets()
        {
            跟踪目标表.Clear();
            目标跟踪器表.Clear();
            下一个目标ID = 1;
        }

        #endregion
    }
}