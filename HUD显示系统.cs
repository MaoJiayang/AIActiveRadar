using Sandbox.ModAPI.Ingame;
using VRage.Game.ModAPI.Ingame;
using VRage.Game.GUI.TextPanel;
using VRageMath;
using System.Collections.Generic;
using Vector2 = VRageMath.Vector2;
using System;
using VRage.Game;
using System.Linq;

namespace IngameScript
{
    public class 弹道显示信息
    {
        public Vector3D 弹道预测点;
        public double 弹道拦截时间;
        public TargetTracker 选定目标跟踪器;

        public List<SimpleTargetInfo> 目标历史轨迹
        {
            get
            {
                List<SimpleTargetInfo> res = new List<SimpleTargetInfo>();
                if (选定目标跟踪器.GetHistoryCount() == 0) return res;
                for (int i = 0; i < 选定目标跟踪器.GetHistoryCount(); i++)
                {
                    var info = 选定目标跟踪器.GetTargetInfoAt(i);
                    if (info.HasValue) res.Add(info.Value);
                }
                return res;
            }
        }

        public 弹道显示信息(Vector3D 弹道预测, double 弹道拦截时间, TargetTracker 选定目标跟踪器)
        {
            this.弹道预测点 = 弹道预测;
            this.弹道拦截时间 = 弹道拦截时间;
            this.选定目标跟踪器 = 选定目标跟踪器;
        }
    }
    /// <summary>
    /// HUD 系统 - 单独管理 LCD HUD 显示
    /// </summary>
    public class HUD显示系统
    {
        private 参数管理器 参数们;
        private List<IMyTextPanel> LCD列表 = new List<IMyTextPanel>();
        public bool 已初始化 = false;
        private float LCD物理宽度 = 2.5f;
        private float LCD物理高度 = 2.5f;
        public IMyShipController 参考驾驶舱 { get; private set; }
        public int 视线选定目标ID { get; private set; } = -1;
        private double 前向最小夹角 = double.MaxValue;
        // 目标-屏幕-绘制位置映射类
        private struct 目标绘制信息
        {
            public int 目标ID;
            public SimpleTargetInfo 目标信息;
            public IMyTextPanel LCD屏幕;
            public Vector2 绘制位置;
            public double 距离;
        }
        public HUD显示系统(参数管理器 参数们)
        {
            this.参数们 = 参数们;
        }

        /// 初始化 HUD 系统，必须先调用
        /// </summary>
        public void 初始化(IMyBlockGroup HUD组)
        {
            List<IMyShipController> 驾驶舱列表 = new List<IMyShipController>();
            HUD组.GetBlocksOfType(驾驶舱列表, block => block.CustomName.Contains(参数们.参考驾驶舱标签));
            if (驾驶舱列表.Count > 0) 参考驾驶舱 = 驾驶舱列表[0];

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
            if (参考驾驶舱.CubeGrid.GridSizeEnum == MyCubeSize.Small)
            {
                LCD物理宽度 = 0.5f;
                LCD物理高度 = 0.5f;
            }
            else if (参考驾驶舱.CubeGrid.GridSizeEnum == MyCubeSize.Large)
            {
                LCD物理宽度 = 2.5f;
                LCD物理高度 = 2.5f;
            }
        }
        public void 更新视线选定目标ID(Dictionary<int, SimpleTargetInfo> 目标字典, Vector3D? 选定弹道预测 = null)
        {
            int 上次选定目标ID = 视线选定目标ID;
            视线选定目标ID = -1;
            前向最小夹角 = double.MaxValue;
            if (目标字典.Count == 0) return;
            foreach (var 目标 in 目标字典)
            {
                Vector3D toTarget = 目标.Value.Position - 参考驾驶舱.GetPosition();
                int 目标id = 目标.Key;
                double angle = Vector3D.Angle(toTarget, 参考驾驶舱.WorldMatrix.Forward);
                if (angle < 前向最小夹角)
                {
                    前向最小夹角 = angle;
                    视线选定目标ID = 目标id;
                }
            }
            if (选定弹道预测.HasValue && 选定弹道预测.Value != Vector3D.Zero && 目标字典.ContainsKey(上次选定目标ID)) // 不让新目标抢走预瞄点选择
            {
                Vector3D toAim = 选定弹道预测.Value - 参考驾驶舱.GetPosition();
                double angle = Vector3D.Angle(toAim, 参考驾驶舱.WorldMatrix.Forward);
                if (angle < 前向最小夹角 * 2) // 允许预瞄点选择抢走目标,容差两倍
                {
                    前向最小夹角 = angle;
                    视线选定目标ID = 上次选定目标ID;
                }
            }
        }
        /// <summary>
        /// 根据目标位置字典绘制 HUD
        /// </summary>
        public void 绘制(Dictionary<int, SimpleTargetInfo> 目标字典, 弹道显示信息 弹道信息 = null)
        {
            if (!已初始化) return;
            var 驾驶舱位置 = 参考驾驶舱.GetPosition();

            // 0. 建立目标-屏幕-绘制位置映射表
            List<目标绘制信息> 目标绘制映射表 = new List<目标绘制信息>();
            // 1. 外层循环对于每个目标，更新一遍重点目标ID
            foreach (var kv in 目标字典)
            {
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
                                绘制选定目标(frame, 绘制信息.绘制位置, 绘制信息.目标ID, 绘制信息.目标信息, 绘制信息.距离);
                                if (视线选定目标ID != -1 && 弹道信息 != null)
                                {
                                    Vector2? screenPos = World到屏幕(参考驾驶舱.GetPosition(), lcd.GetPosition(), lcd.WorldMatrix, 弹道信息.弹道预测点, lcd.SurfaceSize);
                                    if (screenPos.HasValue)
                                    {
                                        绘制预瞄点(frame, lcd, screenPos.Value, 弹道信息.弹道预测点, 弹道信息.选定目标跟踪器, 弹道信息.弹道拦截时间);
                                    }
                                    List<Vector2> 屏幕轨迹点列表 = new List<Vector2>();
                                    foreach (SimpleTargetInfo 轨迹点 in 弹道信息.目标历史轨迹)
                                    {
                                        Vector2? 屏幕位置 = World到屏幕(参考驾驶舱.GetPosition(), lcd.GetPosition(), lcd.WorldMatrix, 轨迹点.Position, lcd.SurfaceSize);
                                        if (屏幕位置.HasValue) 屏幕轨迹点列表.Add(屏幕位置.Value);
                                    }
                                    绘制轨迹点(frame, lcd, 屏幕轨迹点列表);
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
            if (参考驾驶舱.CubeGrid.GridSizeEnum == MyCubeSize.Small) y -= 512;
            if (x < 0 || x > surfaceSize.X || y < 0 || y > surfaceSize.Y) return null;

            return new VRageMath.Vector2(x, y);
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
        private void 绘制选定目标(MySpriteDrawFrame frame, VRageMath.Vector2 pos, int id, SimpleTargetInfo target, double dist)
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
            frame.Add(new MySprite
            {
                Type = SpriteType.TEXTURE,
                Data = "Cross",
                Position = pos,
                Size = new VRageMath.Vector2(6, 6),
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
        /// <summary>
        /// 绘制预瞄点
        /// </summary>
        private void 绘制预瞄点(MySpriteDrawFrame frame, IMyTextPanel lcd, VRageMath.Vector2 screenPos, Vector3D predictedPoint, TargetTracker tracker, double interceptTime)
        {
            // 计算综合预测误差和误差圆半径
            double totalError = tracker.combinationError * interceptTime;
            double pointDist = Vector3D.Distance(参考驾驶舱.GetPosition(), predictedPoint);
            if (pointDist < 1e-6) return;
            double errorAngle = totalError / pointDist;
            var surfaceSize = lcd.SurfaceSize;
            // 近似转换为屏幕像素半径
            float radius = (float)(errorAngle * surfaceSize.X / (2 * Math.PI) * LCD物理宽度);
            // 限制半径范围
            radius = Math.Max(12, Math.Min(radius, 50));
            // 绘制空心圆
            frame.Add(new MySprite
            {
                Type = SpriteType.TEXTURE,
                Data = "CircleHollow",
                Position = screenPos,
                Size = new VRageMath.Vector2(radius * 2, radius * 2),
                Color = Color.Red,
                Alignment = TextAlignment.CENTER
            });
            frame.Add(new MySprite
            {
                Type = SpriteType.TEXTURE,
                Data = "CircleHollow",
                Position = screenPos,
                Size = new VRageMath.Vector2(radius * 2 + 2, radius * 2 + 2),
                Color = Color.Red,
                Alignment = TextAlignment.CENTER
            });
            // 绘制权重信息
            double cw = tracker.circularWeight;
            double lw = tracker.linearWeight;
            frame.Add(new MySprite
            {
                Type = SpriteType.TEXT,
                Data = $"Cir: {cw:F2}\nLin: {lw:F2}\nErr: {totalError:F0}m",
                Position = new VRageMath.Vector2(75, 10),
                Color = Color.Green,
                FontId = "White",
                Alignment = TextAlignment.RIGHT,
                RotationOrScale = 0.618f
            });
        }
        private void 绘制轨迹点(MySpriteDrawFrame frame, IMyTextPanel lcd, List<Vector2> 屏幕轨迹点列表)
        {
            for (int i = 0; i < 屏幕轨迹点列表.Count; i++)
            {
                float 轨迹点大小 = Math.Min(Math.Max((屏幕轨迹点列表.Count - i) * 1.5f, 2f), 8f);
                Vector2 point = 屏幕轨迹点列表[i];
                frame.Add(new MySprite
                {
                    Type = SpriteType.TEXTURE,
                    Data = "Circle",
                    Position = point,
                    Size = new VRageMath.Vector2(轨迹点大小, 轨迹点大小),
                    Color = Color.Green,
                    Alignment = TextAlignment.CENTER
                });
            }
        }
    }
}
