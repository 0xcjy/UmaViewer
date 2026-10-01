using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace Gallop.RenderPipeline
{
    public struct RenderTextureHandle
    {
        private const int InvalidNameId = -2;

        private int _id;

        public int Width;

        public int Height;

        private RenderTargetIdentifier _rtId;

        public int NameId
        {
            get
            {
                return _id;
            }
            set
            {
                _id = value;
            }
        }

        public RenderTargetIdentifier RtId
        {
            get
            {
                return _rtId;
            }
        }

        public bool IsValidNameId()
        {
            return _id != InvalidNameId;
        }

        public bool Equals(RenderTextureHandle other)
        {
            // 两边都是通过 nameId 创建的临时 RT。
            if (_id != InvalidNameId && other._id != InvalidNameId)
            {
                return _id == other._id;
            }

            // 任意一边是外部 RenderTargetIdentifier,则统一比较实际的 RenderTargetIdentifier
            return _rtId == other._rtId;
        }

        public override bool Equals(object obj)
        {
            if (!(obj is RenderTextureHandle))
            {
                return false;
            }

            return Equals((RenderTextureHandle)obj);
        }

        public override int GetHashCode()
        {
            return _id;
        }

        public void GetTemporaryRT(CommandBuffer cmd)
        {
            cmd.GetTemporaryRT( _id, Width, Height, 0);
        }

        public void GetTemporaryRT(CommandBuffer cmd, FilterMode filter)
        {
            cmd.GetTemporaryRT( _id, Width, Height, 0, filter);
        }

        public void ReleaseTemporaryRT(CommandBuffer cmd)
        {
            cmd.ReleaseTemporaryRT(_id);
        }

        public static bool operator ==(RenderTextureHandle c1, RenderTextureHandle c2)
        {
            if (c1._id != InvalidNameId && c2._id != InvalidNameId)
            {
                return c1._id == c2._id;
            }

            return c1._rtId == c2._rtId;
        }

        public static bool operator !=( RenderTextureHandle c1, RenderTextureHandle c2)
        {
            if (c1._id != InvalidNameId &&
                c2._id != InvalidNameId)
            {
                return c1._id != c2._id;
            }

            return c1._rtId != c2._rtId;
        }

        public static RenderTextureHandle Make(RenderTargetIdentifier identifer)
        {
            RenderTextureHandle result = default;

            result._id = InvalidNameId;
            result.Width = 0;
            result.Height = 0;
            result._rtId = identifer;

            return result;
        }

        public static RenderTextureHandle Make(RenderTexture renderTexture)
        {
            RenderTextureHandle result = default;

            result._id = InvalidNameId;
            result.Width = renderTexture.width;
            result.Height = renderTexture.height;
            result._rtId = renderTexture;

            return result;
        }

        public static RenderTextureHandle Make(RenderTargetIdentifier identifer,int width,int height)
        {
            RenderTextureHandle result = default;

            result._id = InvalidNameId;
            result.Width = width;
            result.Height = height;
            result._rtId = identifer;

            return result;
        }

        public static RenderTextureHandle Make(int nameId,int width,int height)
        {
            RenderTextureHandle result = default;

            result._id = nameId;
            result.Width = width;
            result.Height = height;
            result._rtId = nameId;

            return result;
        }
    }
}