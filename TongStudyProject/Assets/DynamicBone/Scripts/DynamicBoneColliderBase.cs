﻿using UnityEngine;

namespace DynamicBone.Scripts
{
    public class DynamicBoneColliderBase : MonoBehaviour
    {
        public enum Direction
        {
            X, Y, Z
        }

        [HideInInspector] public DynamicBoneColliderManager.ColliderInfo ColliderInfo;
        
#if UNITY_5_3_OR_NEWER
        [Tooltip("The axis of the capsule's height.")]
#endif
        public Direction m_Direction = Direction.Y;

#if UNITY_5_3_OR_NEWER
        [Tooltip("The center of the sphere or capsule, in the object's local space.")]
#endif
        public Vector3 m_Center = Vector3.zero;

        public enum Bound
        {
            Outside,
            Inside
        }

#if UNITY_5_3_OR_NEWER
        [Tooltip("Constrain bones to outside bound or inside bound.")]
#endif
        public Bound m_Bound = Bound.Outside;

        public int PrepareFrame { set; get; }

        public virtual void Start()
        {        
        }

        public virtual void Prepare()
        {
        }

        public virtual bool Collide(ref Vector3 particlePosition, float particleRadius)
        {
            return false;
        }
    }
}
