using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SharpDX;
using SharpDX.Mathematics;
using System.Windows.Forms;

namespace Bubbles3.Models
{
    #region BblAnimation
    /// <summary>
    /// Called whenever an animation ticks
    /// </summary>
    /// <param name="sender">the BblAnimation doing the call</param>
    /// <param name="ivp">the current animation frame</param>
    /// <param name="animationEnd">true this is the last frame of the animation</param>
    /// <returns>return true to cancel the animation, false to continue</returns>
    public delegate bool BblAnimationTickDelegate(BblAnimation sender, ImageViewingParams ivp, bool animationEnd = false);
    public class BblAnimation : IDisposable
    {
        private ImageViewingParams _start, _end, _current;
        private Timer _timer, _stillStartTimer;
        private DateTime _startTime, _endTime;
        private int _duration, _stillStartDelay;

        private bool _disposed;

        private float _rotation;
        float _deltaWidth, _deltaHeight;
        private Vector2 _translation;
        private float _progress;
        private bool _running;
        private int _frameCount;
        BblAnimationTickDelegate _tickHandler;

        public event BblAnimationTickDelegate Tick;


        public BblAnimation()
        {
            _timer = new Timer();
            _timer.Interval = 0xfffffff;
            _stillStartTimer = new Timer();
            _stillStartTimer.Interval = 0xfffffff;
            _current = new ImageViewingParams();

        }
        ~BblAnimation()
        {
            if (!_disposed) Dispose();
        }
        public void Dispose()
        {
            Reset();

            _timer.Dispose();
            _stillStartTimer.Dispose();

            _disposed = true;
        }
        //BblAnimType type, 
        public void Init(ImageViewingParams start, ImageViewingParams end,
                         BblAnimationTickDelegate tickHandler, int duration, int stillStartDelay = 0, int fps = 0)
        {

            //if (type == BblAnimType.IvpRestoration || _curType != BblAnimType.IvpRestoration)
            //{
            //    if (isRunning && !_current.isReset)
            //    {
            //        EndNow();

            //    }
            //}
            //else 
            if (isRunning && !_current.isReset)
            {
                float x = _end.center.X - _current.center.X;
                float y = _end.center.Y - _current.center.Y;

                float hw = (_end.rect.Width - _current.rect.Width) / 2;
                float hh = (_end.rect.Height - _current.rect.Height) / 2;

                start = _current;
                end.Set(end.l + x - hw, end.t + y - hh, end.r + x + hw, end.b + y + hh, end.rotation);
                Reset();
            }

            _start = start;
            _end = end;
            _duration = duration;
            _tickHandler = tickHandler;
            Tick += tickHandler;

            if (fps <= 0) fps = 100;
            _timer.Interval = (int)(1000.0f / (float)fps);
            _timer.Tick += TimerElapsed;

            _stillStartDelay = stillStartDelay;
            if (stillStartDelay > 0)
            {
                _stillStartTimer.Interval = stillStartDelay;
                _stillStartTimer.Tick += StillStartElapsed;
            }

            _rotation = _end.rotation - _start.rotation;
            if (_rotation > 180) _rotation = -(360 - _rotation);

            _deltaWidth = _end.rect.Width - _start.rect.Width;
            _deltaHeight = _end.rect.Height - _start.rect.Height;

            float xTrans = _end.center.X - _start.center.X;
            float yTrans = _end.center.Y - _start.center.Y;
            _translation = new Vector2(xTrans, yTrans);

            _frameCount = 0;

            _running = true;

            if (_stillStartDelay > 0)
            {
                Tick(this, _start);
                _stillStartTimer.Start();
            }
            else
            {
                _startTime = DateTime.Now;
                _endTime = _startTime + new TimeSpan(0, 0, 0, 0, _duration);
                _timer.Start();
            }
        }


        public bool isRunning { get { return _running; } }
        public int frameCount { get { return _frameCount; } }

        public void EndNow()
        {
            Tick(this, _end, true);
            Reset();
        }

        public void StillStartElapsed(object sender, EventArgs args)
        {
            _stillStartTimer.Stop();
            _stillStartTimer.Interval = 0xfffffff;
            _stillStartTimer.Tick -= StillStartElapsed;
            _stillStartDelay = 0;

            _startTime = DateTime.Now;
            _endTime = _startTime + new TimeSpan(0, 0, 0, 0, _duration);

            _timer.Start();
        }

        public void TimerElapsed(object sender, EventArgs args)
        {
            TimeSpan timeFromStart = DateTime.Now - _startTime;
            TimeSpan timeToEnd = _endTime - DateTime.Now;

            if (Tick == null) return;

            _progress = (float)timeFromStart.TotalMilliseconds / (float)_duration;

            if (_startTime == _endTime)
            {
                if (!_end.isReset) _progress = 1;
                else return;
            }
            if (_progress >= 1)
            {
                _progress = 1;
                _current = _end;

                _frameCount++;
                Tick(this, _current, true);
                Reset();
                return;
            }

            float r = _start.rotation + (_progress * _rotation);
            Vector2 c = new Vector2(_start.center.X + (_translation.X * _progress),
                                    _start.center.Y + (_translation.Y * _progress));
            float w = _start.rect.Width + (_progress * _deltaWidth);
            float h = _start.rect.Height + (_progress * _deltaHeight);
            _current.Set(c, w, h, r);

            _frameCount++;

            bool cancel = false;
            if (!_current.isUniDim) cancel = Tick(this, _current);

            if (cancel) Reset();

        }

        public void Reset()
        {
            _running = false;

            _timer.Stop();
            _timer.Interval = 0xfffffff;
            _timer.Tick -= TimerElapsed;

            if (_stillStartTimer.Enabled)
            {
                _stillStartTimer.Stop();
                _stillStartTimer.Interval = 0xfffffff;
                _stillStartTimer.Tick -= StillStartElapsed;
            }

            if (Tick != null && _tickHandler != null)
            {
                Tick -= _tickHandler;
                _tickHandler = null;
            }

            _start.Reset();
            _end.Reset();
            _current.Reset();

            _startTime = _endTime = DateTime.Now;
            _progress = 0.0f;
            _frameCount = 0;
            _rotation = 0.0f;
            _deltaWidth = _deltaHeight = 0;
            _translation = new Vector2();
        }

    }
    #endregion
}
