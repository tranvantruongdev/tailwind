using Tailwind.Art;
using UnityEngine;

namespace Tailwind.View
{
    /// <summary>Dusk sky, a low sun, and two layers of rooftops with lit windows that scroll slower than the course.</summary>
    public sealed class ParallaxBackground : MonoBehaviour
    {
        private const float TileWidth = 24f;

        private Camera _camera;
        private SpriteRenderer _sky;
        private SpriteRenderer _sun;
        private Transform _far;
        private Transform _near;
        private SpriteRenderer _ground;

        public static ParallaxBackground Create(Camera camera, float groundY)
        {
            var go = new GameObject("Background");
            var view = go.AddComponent<ParallaxBackground>();
            view._camera = camera;
            view._sky = ProceduralSprites.Spawn("Sky", go.transform, ProceduralSprites.SkyGradient, Color.white, -100);
            view._sun = ProceduralSprites.Spawn("Sun", go.transform, ProceduralSprites.Circle, Palette.Sun, -90);
            view._sun.transform.localScale = Vector3.one * 2.4f;
            view._far = view.BuildRooftops("Far rooftops", Palette.FarBuildings, -80, groundY, 2.2f, 5.0f, 11);
            view._near = view.BuildRooftops("Near rooftops", Palette.NearBuildings, -70, groundY, 1.2f, 3.4f, 23);
            view._ground = ProceduralSprites.Spawn("Ground", go.transform, ProceduralSprites.Square, Palette.Ground, 30);
            view._ground.transform.localScale = new Vector3(40f, groundY + 2f, 1f);
            view._ground.transform.position = new Vector3(0f, groundY - (groundY + 2f) * 0.5f, 0f);
            return view;
        }

        private Transform BuildRooftops(string label, Color color, int order, float groundY, float minHeight, float maxHeight, int seed)
        {
            var layer = new GameObject(label).transform;
            layer.SetParent(transform, false);
            var random = new System.Random(seed);
            // Two copies of one tile side by side, so the layer can wrap seamlessly.
            for (int copy = 0; copy < 2; copy++)
            {
                float x = copy * TileWidth;
                while (x < (copy + 1) * TileWidth)
                {
                    float width = 1.2f + (float)random.NextDouble() * 1.8f;
                    float height = minHeight + (float)random.NextDouble() * (maxHeight - minHeight);
                    var building = ProceduralSprites.Spawn("Roof", layer, ProceduralSprites.Square, color, order);
                    building.transform.localPosition = new Vector3(x + width * 0.5f, groundY + height * 0.5f, 0f);
                    building.transform.localScale = new Vector3(width, height, 1f);

                    int windows = random.Next(1, 4);
                    for (int w = 0; w < windows; w++)
                    {
                        if (random.NextDouble() < 0.45)
                        {
                            continue;
                        }

                        var window = ProceduralSprites.Spawn("Window", layer, ProceduralSprites.Square, Palette.Window, order + 1);
                        window.transform.localPosition = new Vector3(x + width * (0.25f + 0.5f * (float)random.NextDouble()), groundY + height * (0.3f + 0.5f * (float)random.NextDouble()), 0f);
                        window.transform.localScale = new Vector3(0.18f, 0.24f, 1f);
                    }

                    x += width + 0.15f;
                }

                random = new System.Random(seed); // identical second copy
            }

            return layer;
        }

        private void LateUpdate()
        {
            if (_camera == null)
            {
                return;
            }

            var cam = _camera.transform.position;
            float height = _camera.orthographicSize * 2f;
            float width = height * _camera.aspect;

            _sky.transform.position = new Vector3(cam.x, cam.y, 0f);
            _sky.transform.localScale = new Vector3(width * 1.1f * 256f, height * 1.1f, 1f);
            _sun.transform.position = new Vector3(cam.x + width * 0.22f, cam.y + height * 0.12f, 0f);
            _ground.transform.position = new Vector3(cam.x, _ground.transform.position.y, 0f);

            Scroll(_far, cam.x, 0.15f);
            Scroll(_near, cam.x, 0.4f);
        }

        private static void Scroll(Transform layer, float cameraX, float factor)
        {
            // Moves with the camera at (1 - factor) speed, wrapping every tile width.
            float offset = Mathf.Repeat(cameraX * factor, TileWidth);
            layer.position = new Vector3(cameraX - offset - TileWidth * 0.5f, 0f, 0f);
        }
    }
}
