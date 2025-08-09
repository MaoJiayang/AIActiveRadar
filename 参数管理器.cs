using System;
using VRage.Game.ModAPI.Ingame;
using VRageMath;

namespace IngameScript
{
    /// <summary>
    /// 导弹参数管理器 - 统一管理所有超参数
    /// </summary>
    public class 参数管理器
    {
        #region 目标跟踪器参数

        /// <summary>
        /// 目标历史记录最大长度
        /// </summary>
        public int 目标历史最大长度 { get; set; } = 4;

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
        /// <summary>
        /// 战斗块目标优先级
        /// </summary>
        public OffensiveCombatTargetPriority 目标优先级 { get; set; } = OffensiveCombatTargetPriority.Largest;

        #endregion

        #region 雷达参数
        // 算法参数
        public double 目标关联距离阈值 { get; set; } = 500.0; // 米
        public double 速度关联阈值 { get; set; } = 100.0; // m/s
        public int 目标确认帧数 { get; set; } = 3; // 需要连续确认的帧数
        public int 目标丢失超时帧数 { get; set; } = 180; // 6秒超时(按30FPS计算)
        public int 暂定目标超时帧数 { get; set; } = 90; // 3秒暂定超时

        #endregion
        #region 构造函数

        /// <summary>
        /// 默认构造函数，使用默认参数
        /// </summary>
        public 参数管理器()
        {
            // 所有参数已在属性声明时设置默认值
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
                    case "速度关联阈值":
                        速度关联阈值 = double.Parse(参数值);
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
            配置.AppendLine($"速度关联阈值={速度关联阈值}");
            配置.AppendLine($"目标确认帧数={目标确认帧数}");
            配置.AppendLine($"目标丢失超时帧数={目标丢失超时帧数}");
            配置.AppendLine($"暂定目标超时帧数={暂定目标超时帧数}");
            配置.AppendLine();
            return 配置.ToString();
        }

        #endregion
    }
}
