using Sandbox.ModAPI.Ingame;
using VRage.Game.ModAPI.Ingame;
using VRage.Game.GUI.TextPanel;
using VRageMath;
using System.Collections.Generic;
using Vector2 = VRageMath.Vector2;
using System;

namespace IngameScript
{
    /// <summary>
    /// HUD 系统 - 单独管理 LCD HUD 显示
    /// </summary>
    public class HUD显示系统
    {
        private IMyCockpit 参考驾驶舱;
        private List<IMyTextPanel> LCD列表 = new List<IMyTextPanel>();
        public bool 已初始化 = false;
        private const float LCD物理宽度 = 2.5f;
        private const float LCD物理高度 = 2.5f;
        public int 视线选定目标ID { get; private set; } = -1;
        private MySpriteDrawFrame 选定绘制帧;
        private double 前向最小夹角 = double.MaxValue;
        /// <summary>
        /// 初始化 HUD 系统，必须先调用
        /// </summary>
        public void 初始化(IMyBlockGroup HUD组)
        {
            List<IMyCockpit> 驾驶舱列表 = new List<IMyCockpit>();
            HUD组.GetBlocksOfType(驾驶舱列表);
            参考驾驶舱 = 驾驶舱列表[0];
            HUD组.GetBlocksOfType(LCD列表);
            已初始化 = 参考驾驶舱 != null && LCD列表 != null && LCD列表.Count > 0;
            if (已初始化)
            {
                foreach (var lcd in LCD列表)
                {
                    lcd.ContentType = ContentType.SCRIPT;
                    lcd.Script = string.Empty;
                }
            }
        }

        // 目标-屏幕-绘制位置映射类
        private class 目标绘制信息
        {
            public int 目标ID;
            public SimpleTargetInfo 目标信息;
            public IMyTextPanel LCD屏幕;
            public VRageMath.Vector2 绘制位置;
            public double 距离;
        }

        /// <summary>
        /// 根据目标位置字典绘制 HUD
        /// </summary>
        public void 绘制(Dictionary<int, SimpleTargetInfo> 目标字典,Dictionary<int, TargetTracker> 目标跟踪器字典 = null)
        {
            if (!已初始化) return;
            var 驾驶舱位置 = 参考驾驶舱.GetPosition();
            前向最小夹角 = double.MaxValue;
            视线选定目标ID = -1;

            // 0. 建立目标-屏幕-绘制位置映射表
            var 目标绘制映射表 = new List<目标绘制信息>();
            // 1. 外层循环对于每个目标，更新一遍重点目标ID
            foreach (var kv in 目标字典)
            {
                更新重点目标ID(参考驾驶舱, kv);
                // 2. 对于每个目标，内层对于每个屏幕，循环到第一个非null绘制位置的屏幕
                foreach (var lcd in LCD列表)
                {
                    if (!lcd.IsWorking) continue;
                    var lcd世界矩阵 = lcd.WorldMatrix;
                    var lcd位置 = lcd.GetPosition();
                    var surfaceSize = lcd.SurfaceSize;
                    var 屏显位置 = World到屏幕(驾驶舱位置, lcd位置, lcd世界矩阵, kv.Value.Position, surfaceSize);
                    
                    if (屏显位置.HasValue)
                    {
                        // 找到第一个能显示的屏幕，记录目标-屏幕-绘制位置对
                        var 距离 = Vector3D.Distance(驾驶舱位置, kv.Value.Position);
                        目标绘制映射表.Add(new 目标绘制信息
                        {
                            目标ID = kv.Key,
                            目标信息 = kv.Value,
                            LCD屏幕 = lcd,
                            绘制位置 = 屏显位置.Value,
                            距离 = 距离
                        });
                        break; // 一个目标只显示在一个屏幕上
                    }
                }
            }
            // 3. 按LCD分组绘制目标
            foreach (var lcd in LCD列表)
            {
                if (!lcd.IsWorking) continue;
                var surfaceSize = lcd.SurfaceSize;
                using (var frame = lcd.DrawFrame())
                {
                    // 清屏
                    frame.Add(new MySprite
                    {
                        Type = SpriteType.TEXTURE,
                        Data = "Square",
                        Position = surfaceSize * 0.5f,
                        Size = surfaceSize,
                        Color = Color.Black,
                        Alignment = TextAlignment.CENTER
                    });

                    // 绘制在当前LCD上的所有目标
                    foreach (var 绘制信息 in 目标绘制映射表)
                    {
                        if (绘制信息.LCD屏幕 == lcd)
                        {
                            if (绘制信息.目标ID == 视线选定目标ID)
                            {
                                绘制重点目标(frame, 绘制信息.绘制位置, 绘制信息.目标ID, 绘制信息.目标信息, 绘制信息.距离);
                                if (视线选定目标ID != -1 && 目标跟踪器字典 != null)
                                {
                                    TargetTracker 选定目标跟踪器 = 目标跟踪器字典[视线选定目标ID];
                                    SimpleTargetInfo 选定目标信息 = 目标字典[视线选定目标ID];
                                    double 弹道拦截时间;
                                    Vector3D 预测落点 = 弹道计算器.计算预测位置(参考驾驶舱, 选定目标跟踪器, 选定目标信息, out 弹道拦截时间);
                                    double 综合预测误差 = 选定目标跟踪器.combinationError * 弹道拦截时间;
                                    // 计算误差圆的屏幕半径
                                    // 先计算预测落点到驾驶舱的距离
                                    double 落点距离 = Vector3D.Distance(参考驾驶舱.GetPosition(), 预测落点);
                                    // 计算误差在该距离上对应的角度（弧度）
                                    double 误差角度 = 综合预测误差 / 落点距离;
                                    // 转换为屏幕像素半径（近似计算）
                                    float 屏幕半径 = (float)(误差角度 * lcd.SurfaceSize.X / (2 * Math.PI)) * LCD物理宽度;
                                    // 限制最小和最大半径
                                    屏幕半径 = Math.Max(20, Math.Min(屏幕半径, 50)); 
                                    var screenPos = World到屏幕(参考驾驶舱.GetPosition(), lcd.GetPosition(), lcd.WorldMatrix, 预测落点, lcd.SurfaceSize);
                                    if (screenPos.HasValue)
                                    {
                                        // 绘制预测落点 - 空心红色圆
                                        frame.Add(new MySprite
                                        {
                                            Type = SpriteType.TEXTURE,
                                            Data = "CircleHollow",
                                            Position = screenPos.Value,
                                            Size = new VRageMath.Vector2(屏幕半径 * 2, 屏幕半径 * 2),
                                            Color = Color.Red,
                                            Alignment = TextAlignment.CENTER
                                        });
                                        // 绘制文字，位置在方块右边，垂直居中对齐
                                        double 圆周权重 = 选定目标跟踪器.circularWeight;
                                        double 线性权重 = 选定目标跟踪器.linearWeight;
                                        frame.Add(new MySprite
                                        {
                                            Type = SpriteType.TEXT,
                                            Data = $"C: {圆周权重:F1}\nL: {线性权重:F1}",
                                            Position = screenPos.Value + new VRageMath.Vector2(-30, 20),
                                            Color = Color.Red,
                                            FontId = "White",
                                            Alignment = TextAlignment.RIGHT,
                                            RotationOrScale = 0.618f
                                        });
                                    }
                                }
                            }
                            else
                            {
                                绘制普通标记(frame, 绘制信息.绘制位置, 绘制信息.目标ID, 绘制信息.距离);
                            }
                        }
                    }
                }
            }
        }
        // 计算三维点到 LCD 屏幕的投影坐标
        private VRageMath.Vector2? World到屏幕(Vector3D 驾驶舱位置, Vector3D lcdPos, MatrixD lcdMatrix, Vector3D 目标位置, VRageMath.Vector2 surfaceSize)
        {
            Vector3D toTarget = 目标位置 - 驾驶舱位置;

            if (toTarget.LengthSquared() < 1) return null;
            Vector3D lcdNormal = lcdMatrix.Forward;
            Vector3D lcdRight = lcdMatrix.Right;
            Vector3D lcdUp = lcdMatrix.Up;

            // 平面原点向后偏移厚度一半
            Vector3D planeOrigin = lcdPos + lcdNormal * (LCD物理宽度 / 2);
            Vector3D toPlane = planeOrigin - 驾驶舱位置;
            double distPlane = Vector3D.Dot(toPlane, lcdNormal);
            double rayDist = Vector3D.Dot(toTarget, lcdNormal);
            if (System.Math.Abs(rayDist) < 1e-4) return null;
            double t = distPlane / rayDist;
            if (t <= 0) return null;
            Vector3D intersect = 驾驶舱位置 + toTarget * t;
            Vector3D local = intersect - planeOrigin;
            double lx = Vector3D.Dot(local, lcdRight);
            double ly = Vector3D.Dot(local, lcdUp);
            float x = (float)(lx / LCD物理宽度 + 0.5) * surfaceSize.X;
            float y = (float)(-ly / LCD物理高度 + 0.5) * surfaceSize.Y;
            if (x < 0 || x > surfaceSize.X || y < 0 || y > surfaceSize.Y) return null;

            return new VRageMath.Vector2(x, y);
        }
        private void 更新重点目标ID(IMyCockpit 驾驶舱, KeyValuePair<int, SimpleTargetInfo> 目标)
        {
            Vector3D toTarget = 目标.Value.Position - 驾驶舱.GetPosition();
            int 目标id = 目标.Key;
            double angle = Vector3D.Angle(toTarget, 驾驶舱.WorldMatrix.Forward);
            if (angle < 前向最小夹角)
            {
                前向最小夹角 = angle;
                视线选定目标ID = 目标id;
            }            
        }
        // 绘制单个目标标记
        private void 绘制普通标记(MySpriteDrawFrame frame, VRageMath.Vector2 pos, int id, double dist)
        {
            frame.Add(new MySprite
            {
                Type = SpriteType.TEXTURE,
                Data = "Circle",
                Position = pos,
                Size = new VRageMath.Vector2(10, 10),
                Color = Color.Red,
                Alignment = TextAlignment.CENTER
            });
            frame.Add(new MySprite
            {
                Type = SpriteType.TEXT,
                Data = $"ID:{id}\n{dist:F0}m",
                Position = pos + new VRageMath.Vector2(25, 0),
                Color = Color.Red,
                FontId = "White",
                Alignment = TextAlignment.LEFT,
                RotationOrScale = 0.8f
            });
        }
        // 绘制重点目标（离准心最近的目标）- 修正方框绘制
        private void 绘制重点目标(MySpriteDrawFrame frame, VRageMath.Vector2 pos, int id, SimpleTargetInfo target, double dist)
        {
            double 目标速度 = target.Velocity.Length();
            
            // 绘制空心正方形
            frame.Add(new MySprite
            {
                Type = SpriteType.TEXTURE,
                Data = "SquareHollow",
                Position = pos,
                Size = new VRageMath.Vector2(50, 50),
                Color = Color.Red,
                Alignment = TextAlignment.CENTER
            });
            
            // 绘制文字，位置在方块右边，垂直居中对齐
            frame.Add(new MySprite
            {
                Type = SpriteType.TEXT,
                Data = $"ID:{id}\n{dist:F1}m\n{目标速度:F1}m/s",
                Position = pos + new VRageMath.Vector2(30, -20),
                Color = Color.Red,
                FontId = "White",
                Alignment = TextAlignment.LEFT,
                RotationOrScale = 1.0f
            });
        }
    }
}
