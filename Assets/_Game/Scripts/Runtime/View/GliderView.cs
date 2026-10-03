using System.Collections.Generic;
using PrimeTween;
using Tailwind.Art;
using Template.Feel;
using UnityEngine;

namespace Tailwind.View
{
    /// <summary>Mây the paper glider: tilts with vertical speed, leaves wind streaks in streams, crumples on crash.</summary>
    public sealed class GliderView : MonoBehaviour
    {
        private const int StreakPool = 16;

        private SpriteRenderer _body;
        private readonly Queue<SpriteRenderer> _streaks = new Queue<SpriteRenderer>();
        private float _tilt;
        private float _streakTimer;
        private bool _crashed;

        public static GliderView Create(Transform parent)
        {
            var go = new GameObject("Glider");
            go.transform.SetParent(parent, false);
            var view = go.AddComponent<GliderView>();
            view._body = ProceduralSprites.Spawn("Body", go.transform, ProceduralSprites.Glider, Palette.Glider, 20);
            view._body.transform.localScale = Vector3.one * 0.9f;

            for (int i = 0; i < StreakPool; i++)
            {
                var streak = ProceduralSprites.Spawn("Streak", parent, ProceduralSprites.Square, Palette.StreamStreak, 15);
                streak.gameObject.SetActive(false);
                view._streaks.Enqueue(streak);
            }

            return view;
        }

        public void Reset(Vector2 position)
        {
            _crashed = false;
            _tilt = 0f;
            transform.position = position;
            transform.rotation = Quaternion.identity;
            _body.transform.localScale = Vector3.one * 0.9f;
            _body.color = Palette.Glider;
        }

        public void Follow(Vector2 position, float velocityY, bool inStream, float dt)
        {
            if (_crashed)
            {
                return;
            }

            transform.position = position;
            float targetTilt = Mathf.Clamp(velocityY * 5f, -40f, 28f);
            _tilt = Mathf.Lerp(_tilt, targetTilt, 1f - Mathf.Exp(-12f * dt));
            transform.rotation = Quaternion.Euler(0f, 0f, _tilt);

            if (inStream)
            {
                _streakTimer -= dt;
                if (_streakTimer <= 0f)
                {
                    _streakTimer = 0.05f;
                    EmitStreak(position);
                }
            }
        }

        public void OnFlap()
        {
            JuiceFx.Punch(_body.transform, 0.18f, 0.2f);
        }

        public void OnCrash()
        {
            _crashed = true;
            // Crumple: spin, shrink and drop.
            Tween.Rotation(transform, new Vector3(0f, 0f, -200f), 0.6f, Ease.OutCubic, useUnscaledTime: true);
            Tween.Scale(_body.transform, 0.45f, 0.6f, Ease.InBack, useUnscaledTime: true);
            Tween.PositionY(transform, transform.position.y - 1.2f, 0.6f, Ease.InQuad, useUnscaledTime: true);
            Tween.Custom(_body, Palette.Glider, new Color(0.8f, 0.78f, 0.85f), 0.6f, (b, c) => b.color = c, useUnscaledTime: true);
        }

        private void EmitStreak(Vector2 position)
        {
            var streak = _streaks.Dequeue();
            _streaks.Enqueue(streak);
            var t = streak.transform;
            t.position = new Vector3(position.x - 0.45f, position.y + Random.Range(-0.12f, 0.12f), 0f);
            t.localScale = new Vector3(0.5f, 0.04f, 1f);
            streak.gameObject.SetActive(true);
            streak.color = Palette.StreamStreak;
            Tween.Custom(streak, Palette.StreamStreak, new Color(1f, 1f, 1f, 0f), 0.35f, (s, c) => s.color = c)
                .OnComplete(streak.gameObject, go => go.SetActive(false));
            Tween.PositionX(t, t.position.x - 0.6f, 0.35f);
        }
    }
}
