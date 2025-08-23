using System;
using System.Collections.Generic;
using VRageMath;
using Sandbox.ModAPI.Ingame;
using System.Numerics;

namespace IngameScript
{
    public partial class RelativeTargetTracker : TargetTracker
    {
        private IMyControllerCompat _referenceBlock;

        // 构造函数：传入参考方块（原点）
        public RelativeTargetTracker(IMyControllerCompat referenceBlock,int maxHistory) : base(maxHistory)
        {
            _referenceBlock = referenceBlock;
        }
        
        /// <summary>
        /// 更新目标位置，传入为绝对坐标
        /// </summary>
        public new void UpdateTarget(Vector3D position, long timeStamp, bool hasVelocityAvailable = false)
        {
            Vector3D refPos = _referenceBlock.GetPosition();
            base.UpdateTarget(position - refPos, timeStamp, hasVelocityAvailable);
        }

        public SimpleTargetInfo BackToGlobal(SimpleTargetInfo relativeTarget)
        {
            return new SimpleTargetInfo
            {
                Position = relativeTarget.Position + _referenceBlock.GetPosition(),
                Velocity = relativeTarget.Velocity + _referenceBlock.GetShipVelocities().LinearVelocity,
                TimeStamp = relativeTarget.TimeStamp
            };
        }
    }
}