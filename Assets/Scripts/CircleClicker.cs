using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Circle Clicker: a tiny 2D game built around IEnumerator coroutines.
/// Setup: create an empty GameObject in a 2D scene, attach this script, press Play.
/// No prefabs, sprites, or UI objects needed; everything is created at runtime.
/// </summary>
public class CircleClicker : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] float gameDuration = 30f;
    [SerializeField] float startSpawnDelay = 1.0f;   // slow at first...
    [SerializeField] float minSpawnDelay = 0.3f;     // ...faster by the end
    [SerializeField] float circleLifetime = 2f;

    int score, misses;
    float timeLeft;
    bool playing;
    string message = "";

    Camera cam;
    Sprite circleSprite;
    readonly HashSet<GameObject> popped = new HashSet<GameObject>();

    void Start()
    {
        cam = Camera.main;
        cam.backgroundColor = new Color(0.1f, 0.1f, 0.18f);
        cam.clearFlags = CameraClearFlags.SolidColor;
        circleSprite = MakeCircleSprite();

        StartCoroutine(GameLoop());
    }

    void Update()
    {
        if (!playing || !ClickedThisFrame()) return;

        Collider2D hit = Physics2D.OverlapPoint(MouseWorldPosition());
        if (hit != null && !popped.Contains(hit.gameObject))
        {
            popped.Add(hit.gameObject);
            score++;
            StartCoroutine(PopRoutine(hit.gameObject));
        }
    }

    // ---------------------------------------------------------------
    // Coroutines (IEnumerator)
    // ---------------------------------------------------------------

    // Runs the whole game from countdown to restart, one step at a time.
    IEnumerator GameLoop()
    {
        // 3... 2... 1... GO!
        for (int i = 3; i > 0; i--)
        {
            message = i.ToString();
            yield return new WaitForSeconds(1f);
        }
        message = "GO!";
        yield return new WaitForSeconds(0.5f);
        message = "";

        score = 0;
        misses = 0;
        timeLeft = gameDuration;
        playing = true;
        StartCoroutine(SpawnRoutine());

        // Countdown timer: yield return null = "continue next frame"
        while (timeLeft > 0f)
        {
            timeLeft -= Time.deltaTime;
            yield return null;
        }

        playing = false;
        message = $"Time's up!\nScore: {score}   Missed: {misses}\n\nClick to play again";

        // Pause here until the player clicks, then reload the scene
        yield return new WaitForSeconds(0.5f);
        yield return new WaitUntil(ClickedThisFrame);
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    // Spawns circles forever (until the game ends), getting faster over time.
    IEnumerator SpawnRoutine()
    {
        while (playing)
        {
            SpawnCircle();

            float progress = 1f - (timeLeft / gameDuration);          // 0 -> 1
            float delay = Mathf.Lerp(startSpawnDelay, minSpawnDelay, progress);
            yield return new WaitForSeconds(delay);
        }
    }

    // Each circle shrinks over its lifetime. If it runs out, it counts as a miss.
    IEnumerator CircleLifeRoutine(GameObject circle, float startSize)
    {
        float t = 0f;
        while (t < circleLifetime)
        {
            if (circle == null || popped.Contains(circle)) yield break;   // already clicked

            t += Time.deltaTime;
            float size = Mathf.Lerp(startSize, startSize * 0.2f, t / circleLifetime);
            circle.transform.localScale = Vector3.one * size;
            yield return null;
        }

        if (circle != null)
        {
            misses++;
            Destroy(circle);
        }
    }

    // Quick "pop" animation: grow and fade out, then remove.
    IEnumerator PopRoutine(GameObject circle)
    {
        var sr = circle.GetComponent<SpriteRenderer>();
        circle.GetComponent<Collider2D>().enabled = false;

        Vector3 startScale = circle.transform.localScale;
        Color startColor = sr.color;
        const float duration = 0.15f;

        for (float t = 0f; t < duration; t += Time.deltaTime)
        {
            float k = t / duration;
            circle.transform.localScale = startScale * Mathf.Lerp(1f, 1.6f, k);
            sr.color = new Color(startColor.r, startColor.g, startColor.b, 1f - k);
            yield return null;
        }

        popped.Remove(circle);
        Destroy(circle);
    }

    // ---------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------

    void SpawnCircle()
    {
        float halfH = cam.orthographicSize;
        float halfW = halfH * cam.aspect;
        float startSize = Random.Range(0.8f, 1.4f);

        var pos = new Vector3(
            Random.Range(-halfW + 1f, halfW - 1f),
            Random.Range(-halfH + 1f, halfH - 1.5f),   // leave room for the score text
            0f);

        var go = new GameObject("Circle");
        go.transform.position = pos;
        go.transform.localScale = Vector3.one * startSize;

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = circleSprite;
        sr.color = Color.HSVToRGB(Random.value, 0.6f, 1f);

        go.AddComponent<CircleCollider2D>();

        StartCoroutine(CircleLifeRoutine(go, startSize));
    }

    // Builds a white circle texture in code so no art assets are needed.
    Sprite MakeCircleSprite()
    {
        const int size = 128;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        float r = size / 2f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), new Vector2(r, r));
                float alpha = Mathf.Clamp01(r - d);   // soft 1px edge
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
    }

    // Works with both the old Input Manager and the new Input System package.
    bool ClickedThisFrame()
    {
#if ENABLE_INPUT_SYSTEM
        return Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
#else
        return Input.GetMouseButtonDown(0);
#endif
    }

    Vector2 MouseWorldPosition()
    {
#if ENABLE_INPUT_SYSTEM
        Vector2 screen = Mouse.current.position.ReadValue();
#else
        Vector2 screen = Input.mousePosition;
#endif
        return cam.ScreenToWorldPoint(screen);
    }

    // Simple on-screen text using OnGUI (no Canvas setup needed).
    void OnGUI()
    {
        var hud = new GUIStyle(GUI.skin.label) { fontSize = 28, fontStyle = FontStyle.Bold };
        hud.normal.textColor = Color.white;

        if (playing)
        {
            GUI.Label(new Rect(20, 10, 400, 40), $"Score: {score}", hud);
            GUI.Label(new Rect(Screen.width - 220, 10, 200, 40), $"Time: {Mathf.CeilToInt(timeLeft)}", hud);
        }

        if (!string.IsNullOrEmpty(message))
        {
            var big = new GUIStyle(hud) { fontSize = 48, alignment = TextAnchor.MiddleCenter };
            GUI.Label(new Rect(0, 0, Screen.width, Screen.height), message, big);
        }
    }
}
