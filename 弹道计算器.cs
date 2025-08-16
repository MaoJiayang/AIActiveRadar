using Sandbox.ModAPI.Ingame;
using VRage.Game.ModAPI.Ingame;
using VRageMath;
using System.Collections.Generic;
using System.Linq;
using System;

namespace IngameScript
{
    class 弹道计算器
    {
        #region 弹道计算方法

        /// <summary>
        /// 计算预测位置
        /// 其中自身位置使用飞船控制室位置,自身采用匀速模型
        /// 当前目标必须为对应的跟踪器的预测结果
        /// 弹道拦截时间单位：秒
        /// </summary>
        /// <returns>预测的拦截位置</returns>
        public static Vector3D 计算预测位置(
                                    IMyShipController 飞控,
                                    TargetTracker 目标跟踪器,
                                    SimpleTargetInfo 当前目标,
                                    out double 弹道拦截时间,
                                    Vector3D? 参考位置 = null,
                                    double 武器弹速 = 500,
                                    int 预测迭代次数 = 3,
                                    bool 弹药受重力影响 = true
                                    )
        {
            弹道拦截时间 = 0;

            // 获取舰船信息
            Vector3D 舰船位置 = 飞控.GetPosition();
            Vector3D 舰船速度 = 飞控.GetShipVelocities().LinearVelocity;
            Vector3D 重力向量 = 飞控.GetNaturalGravity();
            bool 存在重力 = 重力向量.LengthSquared() > 0.05;
            
            // 确定计算基准位置（速度始终使用飞控速度）
            Vector3D 基准位置 = 参考位置 ?? 舰船位置;
            
            // 获取当前目标更新时间差
            long 基础时间差 = 当前目标.TimeStamp - 目标跟踪器.GetLatestTimeStamp();

            // 计算线性解作为初始拦截点
            Vector3D 拦截点;
            Vector3D 相对位置 = 当前目标.Position - 基准位置;
            Vector3D 相对速度 = 当前目标.Velocity - 舰船速度;

            // 解二次方程计算拦截时间
            double a = 相对速度.LengthSquared() - Math.Pow(武器弹速, 2);

            // a≈0时表示目标速度接近武器弹速，会导致解不稳定
            if (Math.Abs(a) > 0.01)
            {
                double b = 2 * Vector3D.Dot(相对位置, 相对速度);
                double c = 相对位置.LengthSquared();
                double 判别式 = b * b - 4 * a * c;

                if (判别式 >= 0)
                {
                    // 有实数解，计算拦截时间
                    double t1 = (-b + Math.Sqrt(判别式)) / (2 * a);
                    double t2 = (-b - Math.Sqrt(判别式)) / (2 * a);

                    // 选择有效的正解（如果有）
                    double 拦截时间 = double.NaN;
                    if (t1 > 0 && t2 > 0)
                        拦截时间 = Math.Min(t1, t2);
                    else if (t1 > 0)
                        拦截时间 = t1;
                    else if (t2 > 0)
                        拦截时间 = t2;

                    if (!double.IsNaN(拦截时间) && 拦截时间 > 0)
                    {
                        // 使用线性解作为迭代起点
                        拦截点 = 当前目标.Position + 当前目标.Velocity * 拦截时间 - 舰船速度 * 拦截时间;
                    }
                    else
                    {
                        // 拦截时间无效，使用当前位置
                        拦截点 = 当前目标.Position;
                    }
                }
                else
                {
                    // 无实数解，使用当前位置
                    拦截点 = 当前目标.Position;
                }
            }
            else
            {
                // 特殊情况，使用当前位置
                拦截点 = 当前目标.Position;
            }

            // 迭代求解最佳拦截点，使用预定义的迭代次数
            for (int i = 0; i < 预测迭代次数; i++)
            {
                // 计算预判位置需要的时间
                double 距离 = Vector3D.Distance(基准位置, 拦截点);
                double 飞行时间 = 距离 / 武器弹速;
                弹道拦截时间 = 飞行时间;
                // 预测目标在未来位置
                long 预测时间ms = (long)(飞行时间 * 1000) + 基础时间差;
                var 目标信息 = 目标跟踪器.PredictFutureTargetInfo(预测时间ms, false);

                // 参考系变换：从绝对位置转换到相对位置
                // 计算舰船在飞行时间内的位移
                Vector3D 舰船位移 = 舰船速度 * 飞行时间;

                // 对自身采用匀速模型进行修正
                Vector3D 新拦截点 = 目标信息.Position - 舰船位移;

                // 检查预测收敛条件
                if (Vector3D.Distance(拦截点, 新拦截点) < 0.5)
                    break;

                拦截点 = 新拦截点;
            }
            // 简单重力补偿-忽略抛物线轨迹的时间影响
            if (存在重力 && 弹药受重力影响)
            {
                // 计算预判位置需要的时间
                double 距离 = Vector3D.Distance(基准位置, 拦截点);
                double 飞行时间 = 距离 / 武器弹速;
                // 重力导致的弹道下降量: 1/2 * g * t²
                Vector3D 重力补偿量 = 0.5 * 重力向量 * 飞行时间 * 飞行时间;
                // 向重力反方向补偿，使弹道恰好命中目标
                拦截点 -= 重力补偿量;
            }
            return 拦截点;
        }

        #endregion
    }
}