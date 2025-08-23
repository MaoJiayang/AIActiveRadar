using System;
using VRage.Game.ModAPI.Ingame;
using Sandbox.ModAPI.Ingame;
using VRageMath;
using System.Collections.Generic;

namespace IngameScript
{
    /// <summary>
    /// 导弹参数管理器 - 统一管理所有超参数
    /// </summary>
    public class 参数管理器
    {
        #region 编组名称
        public string AI雷达组名 = "AI雷达";
        public string 参考驾驶舱标签 = "REF";
        #endregion
        
        #region 目标跟踪器参数

        /// <summary>
        /// 目标历史记录最大长度
        /// </summary>
        public int 目标历史最大长度 { get; set; } = 8;

        #endregion

        #region AI参数

        /// <summary>
        /// 最大速度限制
        /// </summary>
        public float 最大速度限制 { get; set; } = 299792458f;

        /// <summary>
        /// 战斗块更新目标间隔(正常状态)
        /// </summary>
        public int 战斗块更新间隔正常 { get; set; } = 0;

        /// <summary>
        /// 战斗块攻击模式
        /// </summary>
        public int 战斗块攻击模式 { get; set; } = 3; // 拦截模式

        #endregion

        #region 雷达参数
        // 算法参数
        public double 时间常数 { get; set; } = 1.0 / 60.0; // 秒
        public double 目标关联距离阈值 { get; set; } = 100.0; // 米
        public int 目标确认帧数 { get; set; } = 2; // 需要连续确认的帧数
        public int 目标丢失超时帧数 { get; set; } = 91;
        public int 暂定目标超时帧数 { get; set; } = 91;
        public int 跟踪器更新最低间隔 { get; set; } = 20;
        #endregion

        #region 火控参数
        public List<double> 武器弹速列表 { get; private set; } = new List<double> { 500.0 }; // 米/秒
        // 添加私有字段用于保护索引设置
        private int _当前所选弹速索引;
        public int 当前所选弹速索引
        {
            get
            {
                return _当前所选弹速索引;
            }
            set
            {
                if (武器弹速列表 != null && 武器弹速列表.Count > 0)
                {
                    // 对索引进行取余，确保在列表范围内
                    _当前所选弹速索引 = ((value % 武器弹速列表.Count) + 武器弹速列表.Count) % 武器弹速列表.Count;
                }
                else
                {
                    _当前所选弹速索引 = 0;
                }
            }
        }
        #endregion

        #region 辅助瞄准参数

        /// <summary>
        /// 外环PID参数结构
        /// </summary>
        public class PID参数
        {
            public double P系数 { get; set; }
            public double I系数 { get; set; }
            public double D系数 { get; set; }

            public PID参数(double p, double i, double d)
            {
                P系数 = p;
                I系数 = i;
                D系数 = d;
            }
        }

        /// <summary>
        /// 外环PID参数
        /// </summary>
        public PID参数 外环参数 { get; set; } = new PID参数(5, 0, 0);

        /// <summary>
        /// 内环PID参数
        /// </summary>
        public PID参数 内环参数 { get; set; } = new PID参数(21, 0.01, 0.9);
        /// <summary>
        /// 辅助瞄准更新间隔，单位：帧
        /// </summary>
        public int 辅助瞄准更新间隔 { get; set; } = 4;
        public double 角度容差 { get; set; } =  Math.PI / 180.0 * 0.02; // 0.02 度
        public double 常驻滚转转速 { get; set; } = 0;
        public double 获取PID时间常数()
        {
            return 时间常数 * 辅助瞄准更新间隔;
        }
        #endregion

        #region 构造函数

        /// <summary>
        /// 默认构造函数，使用默认参数
        /// </summary>
        public 参数管理器(IMyTerminalBlock block)
        {
            // 初始化参数管理器（可以从Me.CustomData读取配置）
            string 自定义数据 = block.CustomData;
            if (!string.IsNullOrWhiteSpace(自定义数据))
            {
                解析配置字符串(自定义数据);
                block.CustomData = 生成配置字符串();
            }
            else block.CustomData = 生成配置字符串();
        }

        /// <summary>
        /// 从自定义数据字符串加载参数配置
        /// </summary>
        /// <param name="配置字符串">包含参数配置的字符串</param>
        public 参数管理器(string 配置字符串)
        {
            解析配置字符串(配置字符串);
        }

        #endregion

        #region 配置解析方法

        /// <summary>
        /// 从配置字符串解析参数
        /// </summary>
        /// <param name="配置字符串">配置字符串</param>
        private void 解析配置字符串(string 配置字符串)
        {
            if (string.IsNullOrWhiteSpace(配置字符串))
                return;

            string[] 行数组 = 配置字符串.Split('\n');
            foreach (string 行 in 行数组)
            {
                string 处理行 = 行.Trim();
                if (string.IsNullOrEmpty(处理行) || 处理行.StartsWith("//") || 处理行.StartsWith("#"))
                    continue;

                string[] 键值对 = 处理行.Split('=');
                if (键值对.Length != 2)
                    continue;

                string 键 = 键值对[0].Trim();
                string 值 = 键值对[1].Trim();

                尝试设置参数(键, 值);
            }
        }
        private PID参数 解析PID参数(string 参数值)
        {
            // 支持格式: "P,I,D"
            var arr = 参数值.Split(',');
            if (arr.Length == 3)
            {
                double p = double.Parse(arr[0]);
                double i = double.Parse(arr[1]);
                double d = double.Parse(arr[2]);
                return new PID参数(p, i, d);
            }
            return new PID参数(0, 0, 0);
        }
        private List<double> 解析弹速列表(string 参数值)
        {
            var result = new List<double>();
            // 支持格式: "{<value1>,<value2>,<value3>}"
            var arr = 参数值.Trim('{', '}').Split(',');
            foreach (var item in arr)
            {
                double value;
                if (double.TryParse(item, out value))
                {
                    result.Add(value);
                }
            }
            return result;
        }
        /// <summary>
        /// 尝试设置指定的参数
        /// </summary>
        /// <param name="参数名">参数名</param>
        /// <param name="参数值">参数值字符串</param>
        private void 尝试设置参数(string 参数名, string 参数值)
        {
            try
            {
                switch (参数名)
                {
                    case "目标历史最大长度":
                        目标历史最大长度 = int.Parse(参数值);
                        break;
                    case "最大速度限制":
                        最大速度限制 = float.Parse(参数值);
                        break;
                    case "战斗块攻击模式":
                        战斗块攻击模式 = int.Parse(参数值);
                        break;
                    case "战斗块更新间隔正常":
                        战斗块更新间隔正常 = int.Parse(参数值);
                        break;
                    case "目标关联距离阈值":
                        目标关联距离阈值 = double.Parse(参数值);
                        break;
                    case "目标确认帧数":
                        目标确认帧数 = int.Parse(参数值);
                        break;
                    case "目标丢失超时帧数":
                        目标丢失超时帧数 = int.Parse(参数值);
                        break;
                    case "暂定目标超时帧数":
                        暂定目标超时帧数 = int.Parse(参数值);
                        break;
                    case "外环PID3":
                        外环参数 = 解析PID参数(参数值);
                        break;
                    case "内环PID3":
                        内环参数 = 解析PID参数(参数值);
                        break;
                    case "辅助瞄准更新间隔":
                        辅助瞄准更新间隔 = int.Parse(参数值);
                        break;
                    case "角度容差":
                        角度容差 = double.Parse(参数值) * Math.PI / 180.0; // 转换为弧度
                        break;
                    case "武器弹速列表":
                        武器弹速列表 = 解析弹速列表(参数值);
                        break;
                }
            }
            catch (Exception)
            {
                // 参数解析失败时忽略，保持默认值
            }
        }

        #endregion

        #region 配置输出方法

        /// <summary>
        /// 生成当前参数配置的字符串
        /// </summary>
        /// <returns>参数配置字符串</returns>
        public string 生成配置字符串()
        {
            var 配置 = new System.Text.StringBuilder();
            配置.AppendLine("// 参数配置文件");
            配置.AppendLine("// 不要修改任何参数，除非你知道以下三件事：");
            配置.AppendLine("// 是什么，如何工作，可能的影响。");
            配置.AppendLine("// 目标跟踪器参数");
            配置.AppendLine($"目标历史最大长度={目标历史最大长度}");
            配置.AppendLine();
            配置.AppendLine("// 飞行AI参数");
            配置.AppendLine($"战斗块更新间隔正常={战斗块更新间隔正常}");
            配置.AppendLine($"最大速度限制={最大速度限制}");
            配置.AppendLine($"战斗块攻击模式={战斗块攻击模式}");
            配置.AppendLine("// 雷达参数");
            配置.AppendLine($"目标关联距离阈值={目标关联距离阈值}");
            配置.AppendLine($"目标确认帧数={目标确认帧数}");
            配置.AppendLine($"目标丢失超时帧数={目标丢失超时帧数}");
            配置.AppendLine($"暂定目标超时帧数={暂定目标超时帧数}");
            配置.AppendLine();
            配置.AppendLine("// 火控参数");
            配置.AppendLine($"武器弹速列表={{ {string.Join(",", 武器弹速列表)} }}");
            配置.AppendLine("// 辅助瞄准参数");
            配置.AppendLine($"外环PID3={外环参数.P系数},{外环参数.I系数},{外环参数.D系数}");
            配置.AppendLine($"内环PID3={内环参数.P系数},{内环参数.I系数},{内环参数.D系数}");
            配置.AppendLine($"辅助瞄准更新间隔={辅助瞄准更新间隔}");
            配置.AppendLine($"角度容差={角度容差 * 180 / Math.PI}"); // 转换为度
            return 配置.ToString();
        }

        #endregion
    }
}
