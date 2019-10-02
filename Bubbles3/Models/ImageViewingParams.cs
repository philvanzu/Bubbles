using System;
using System.Collections.Generic;
using SharpDX;
using System.IO;
using System.Xml.Serialization;

namespace Bubbles3.Models
{
    #region ivp struct

    /// <summary>
    /// Rectangle representing the viewport in image space.
    /// Allows for quick restauration of the last view (remember last view option).
    /// Serialized in ivp files if the option is activated.
    /// Allows also the view to remain the same when the client 
    /// is resized.
    /// </summary>
    public struct ImageViewingParams
    {
        public string filename { get; set; }
        public float rotation { get; set; }

        public float t { get; set; }
        public float l { get; set; }
        public float b { get; set; }
        public float r { get; set; }

        [XmlIgnore]
        public bool isValid { get; set; }

        [XmlIgnore]
        public bool isDirty
        {
            get;
            set;
        }

        public override bool Equals(object obj)
        {
            return (this == (ImageViewingParams)obj);
        }
        public override int GetHashCode()
        {
            return base.GetHashCode();
        }
        public static bool operator ==(ImageViewingParams lh, ImageViewingParams rh)
        {
            return (lh.l == rh.l && lh.r == rh.r && lh.t == rh.t && lh.b == rh.b && lh.rotation == rh.rotation);
        }
        public static bool operator !=(ImageViewingParams lh, ImageViewingParams rh)
        {
            return !(lh == rh);
        }
        public Vector2 tl { get { return new Vector2(l, t); } }
        public Vector2 br { get { return new Vector2(r, b); } }
        public Vector2 center { get { return new Vector2(l + ((r - l) / 2), t + ((b - t) / 2)); } }
        public RectangleF rect
        {
            get { return new RectangleF(t, l, r - l, b - t); }
        }
        public void Set(float left, float top, float right, float bottom, float rotation)
        {
            l = left; r = right; t = top; b = bottom;
            this.rotation = rotation;
        }
        public void Set(Vector2 center, float Width, float Height, float rotation)
        {
            float hw = (Width / 2);
            l = center.X - hw;
            r = center.X + hw;
            float hh = (Height / 2);
            t = center.Y - hh;
            b = center.Y + hh;

            this.rotation = rotation;
        }
        public void Reset()
        {
            l = r = t = b = rotation = 0.0f;
        }
        public bool isReset { get { return (l == 0 && r == 0 && t == 0 && b == 0 && rotation == 0); } }
        public bool isUniDim { get { return (rect.Width == 0 || rect.Height == 0); } }

        public void Harmonize(ref ImageViewingParams harmonize)
        {
            float ratio = rect.Width / rect.Height;
            float test = harmonize.rect.Width / harmonize.rect.Height;

            if (test != ratio)
            {
                float dif = harmonize.rect.Width - (ratio * harmonize.rect.Height);
                harmonize.l += dif / 2;
                harmonize.r -= dif / 2;
            }

        }

        public override string ToString()
        {
            return string.Format("ivp:{0}/l={1}/r={2}/t={3}/b={4}/rot ={5}", filename, l, r, t, b, rotation);
        }
    }
    #endregion


    public class IvpCollection
    {
        public List<ImageViewingParams> Collection { get; set; }

        public IvpCollection()
        { Collection = new List<ImageViewingParams>(); }

        public IvpCollection(List<ImageViewingParams> collection)
        { Collection = collection; }
    }

}

