using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
#endif

public sealed class PongBootstrap : MonoBehaviour
{
    // Se crea un GameObject automáticamente para no tener que editar la escena a mano.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureInstance()
    {
        if (FindObjectOfType<PongGame>() != null)
            return;

        var root = new GameObject("PongGame");
        root.AddComponent<PongGame>();
    }
}

internal sealed class PongGame : MonoBehaviour
{
    [Header("Dimensiones")]
    [SerializeField] private float targetScreenHeightUnits = 10f;
    [SerializeField] private float wallThickness = 0.35f;
    [SerializeField] private float paddleHeight = 2.75f;
    [SerializeField] private float paddleWidth = 0.35f;
    [SerializeField] private float paddleMargin = 1.25f;
    [SerializeField] private float ballSize = 0.35f;

    [Header("Juego")]
    [SerializeField] private float paddleSpeed = 9f;
    [SerializeField] private float ballBaseSpeed = 7.5f;
    [SerializeField] private float ballSpeedGain = 0.65f;
    [SerializeField] private float ballMaxSpeed = 13f;
    [SerializeField] private float relaunchDelay = 1f;
    [SerializeField] private float maxBounceAngle = 65f;

    private PaddleController leftPaddle;
    private PaddleController rightPaddle;
    private BallController ball;
    private TextMeshProUGUI leftScoreText;
    private TextMeshProUGUI rightScoreText;
    private int leftScore;
    private int rightScore;
    private PhysicsMaterial2D bounceMaterial;
    private Sprite vectorSprite;
    private float halfHeight;
    private float halfWidth;

    private void Awake()
    {
        Application.targetFrameRate = 60;
        PrepareCamera();
        bounceMaterial = CreateBounceMaterial();
        vectorSprite = CreateVectorSprite();
        BuildArena();
        UpdateScoreUI();
        StartCoroutine(DelayedLaunch(Random.value > 0.5f ? 1f : -1f));
    }

    private void PrepareCamera()
    {
        var cam = Camera.main;
        if (cam == null)
        {
            var cameraGo = new GameObject("Main Camera");
            cam = cameraGo.AddComponent<Camera>();
            cameraGo.tag = "MainCamera";
        }

        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = Color.black;
        cam.orthographic = true;
        cam.orthographicSize = targetScreenHeightUnits * 0.5f;

        halfHeight = cam.orthographicSize;
        halfWidth = halfHeight * cam.aspect;
    }

    private PhysicsMaterial2D CreateBounceMaterial()
    {
        var mat = new PhysicsMaterial2D("BouncePerfecto")
        {
            bounciness = 1f,
            friction = 0f
        };
        return mat;
    }

    private Sprite CreateVectorSprite()
    {
        var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        texture.filterMode = FilterMode.Point;
        texture.wrapMode = TextureWrapMode.Clamp;

        var colors = new Color32[4];
        for (int i = 0; i < colors.Length; i++)
            colors[i] = new Color32(255, 255, 255, 255);
        texture.SetPixels32(colors);
        texture.Apply();

        var sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
        sprite.name = "VectorQuad";
        return sprite;
    }

    private void BuildArena()
    {
        var root = transform;
        CreateCenterLine(root);
        CreateWalls(root);
        CreatePaddles(root);
        CreateBall(root);
        CreateGoals(root);
        CreateHud();
    }

    private void CreateCenterLine(Transform root)
    {
        float dashHeight = 0.6f;
        float gap = 0.35f;
        float startY = -halfHeight + dashHeight;
        int count = Mathf.CeilToInt((halfHeight * 2f) / (dashHeight + gap));
        for (int i = 0; i < count; i++)
        {
            float y = startY + i * (dashHeight + gap);
            var dash = new GameObject($"CenterDash_{i}");
            dash.transform.SetParent(root, false);
            dash.transform.localPosition = new Vector3(0f, y, 0f);
            dash.transform.localScale = new Vector3(0.15f, dashHeight, 1f);
            var sr = dash.AddComponent<SpriteRenderer>();
            sr.sprite = vectorSprite;
            sr.color = Color.white;
            sr.sortingOrder = 0;
        }
    }

    private void CreateWalls(Transform root)
    {
        float yOffset = halfHeight + wallThickness * 0.5f;
        CreateWall(root, "WallTop", new Vector2(0f, yOffset));
        CreateWall(root, "WallBottom", new Vector2(0f, -yOffset));
    }

    private void CreateWall(Transform root, string name, Vector2 position)
    {
        var wall = new GameObject(name);
        wall.transform.SetParent(root, false);
        wall.transform.localPosition = position;
        wall.transform.localScale = new Vector3(halfWidth * 2f, wallThickness, 1f);

        var sr = wall.AddComponent<SpriteRenderer>();
        sr.sprite = vectorSprite;
        sr.color = Color.white;
        sr.sortingOrder = 0;

        var collider = wall.AddComponent<BoxCollider2D>();
        collider.sharedMaterial = bounceMaterial;
    }

    private void CreatePaddles(Transform root)
    {
        var leftPos = new Vector2(-halfWidth + paddleMargin, 0f);
        var rightPos = new Vector2(halfWidth - paddleMargin, 0f);
        leftPaddle = CreatePaddle(root, "PaddleLeft", leftPos, KeyCode.W, KeyCode.S);
        rightPaddle = CreatePaddle(root, "PaddleRight", rightPos, KeyCode.UpArrow, KeyCode.DownArrow);
    }

    private PaddleController CreatePaddle(Transform root, string name, Vector2 position, KeyCode up, KeyCode down)
    {
        var paddle = new GameObject(name);
        paddle.transform.SetParent(root, false);
        paddle.transform.localPosition = position;
        paddle.transform.localScale = new Vector3(paddleWidth, paddleHeight, 1f);

        var sr = paddle.AddComponent<SpriteRenderer>();
        sr.sprite = vectorSprite;
        sr.color = Color.white;
        sr.sortingOrder = 1;
        paddle.tag = "Paddle";

        var rb = paddle.AddComponent<Rigidbody2D>();
        rb.gravityScale = 0f;
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;

        var collider = paddle.AddComponent<BoxCollider2D>();
        collider.sharedMaterial = bounceMaterial;

        var controller = paddle.AddComponent<PaddleController>();
        controller.Configure(up, down, paddleSpeed, paddleHeight * 0.5f, halfHeight - wallThickness);
        return controller;
    }

    private void CreateBall(Transform root)
    {
        var go = new GameObject("Ball");
        go.transform.SetParent(root, false);
        go.transform.localPosition = Vector3.zero;
        go.transform.localScale = new Vector3(ballSize, ballSize, 1f);

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = vectorSprite;
        sr.color = Color.white;
        sr.sortingOrder = 2;

        var rb = go.AddComponent<Rigidbody2D>();
        rb.gravityScale = 0f;
        rb.mass = 1f;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        rb.freezeRotation = true;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;

        var collider = go.AddComponent<CircleCollider2D>();
        collider.radius = 0.5f;
        collider.sharedMaterial = bounceMaterial;

        ball = go.AddComponent<BallController>();
        ball.Initialize(rb, ballBaseSpeed, ballSpeedGain, ballMaxSpeed, maxBounceAngle);
    }

    private void CreateGoals(Transform root)
    {
        float goalWidth = 0.2f;
        float height = halfHeight * 2f;
        float x = halfWidth + goalWidth;

        CreateGoal(root, "GoalLeft", new Vector2(-x, 0f), height, true);
        CreateGoal(root, "GoalRight", new Vector2(x, 0f), height, false);
    }

    private void CreateGoal(Transform root, string name, Vector2 position, float height, bool rightPlayerScores)
    {
        var goal = new GameObject(name);
        goal.transform.SetParent(root, false);
        goal.transform.localPosition = position;
        goal.transform.localScale = new Vector3(0.2f, height, 1f);

        var collider = goal.AddComponent<BoxCollider2D>();
        collider.isTrigger = true;

        var scorer = goal.AddComponent<GoalZone>();
        scorer.Configure(this, rightPlayerScores);
    }

    private void CreateHud()
    {
        var canvasGo = new GameObject("HUD", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        leftScoreText = CreateScoreText(canvas.transform, new Vector2(-80f, -60f), "0");
        rightScoreText = CreateScoreText(canvas.transform, new Vector2(80f, -60f), "0");
    }

    private TextMeshProUGUI CreateScoreText(Transform parent, Vector2 anchoredPos, string text)
    {
        var go = new GameObject("ScoreText", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f);
        rect.anchoredPosition = anchoredPos;

        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.fontSize = 64;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;
        tmp.text = text;
        return tmp;
    }

    public void Score(bool rightPlayerScored)
    {
        if (rightPlayerScored)
            rightScore++;
        else
            leftScore++;

        UpdateScoreUI();
        StartCoroutine(DelayedLaunch(rightPlayerScored ? -1f : 1f));
    }

    private IEnumerator DelayedLaunch(float direction)
    {
        ball.StopImmediately();
        yield return new WaitForSeconds(relaunchDelay);
        ball.Launch(direction);
    }

    private void UpdateScoreUI()
    {
        if (leftScoreText != null) leftScoreText.text = leftScore.ToString();
        if (rightScoreText != null) rightScoreText.text = rightScore.ToString();
    }
}

internal sealed class PaddleController : MonoBehaviour
{
    private KeyCode upKey;
    private KeyCode downKey;
    private float speed;
    private float halfHeight;
    private float clampY;
    private Rigidbody2D body;

    public float Height => halfHeight * 2f;

    public void Configure(KeyCode up, KeyCode down, float speed, float halfHeight, float clampY)
    {
        upKey = up;
        downKey = down;
        this.speed = speed;
        this.halfHeight = halfHeight;
        this.clampY = clampY - halfHeight;
        body = GetComponent<Rigidbody2D>();
    }

    private void Update()
    {
        float direction = ReadInput();
        if (Mathf.Approximately(direction, 0f))
            return;

        var position = body.position + Vector2.up * (direction * speed * Time.deltaTime);
        position.y = Mathf.Clamp(position.y, -clampY, clampY);
        body.MovePosition(position);
    }

    private float ReadInput()
    {
        float dir = 0f;

#if ENABLE_INPUT_SYSTEM
        var keyboard = Keyboard.current;
        if (keyboard != null)
        {
            dir += IsPressed(keyboard, upKey) ? 1f : 0f;
            dir -= IsPressed(keyboard, downKey) ? 1f : 0f;
        }
#endif

        if (Input.GetKey(upKey)) dir += 1f;
        if (Input.GetKey(downKey)) dir -= 1f;

        return Mathf.Clamp(dir, -1f, 1f);
    }

#if ENABLE_INPUT_SYSTEM
    private bool IsPressed(Keyboard keyboard, KeyCode keyCode)
    {
        KeyControl control = keyCode switch
        {
            KeyCode.W => keyboard.wKey,
            KeyCode.S => keyboard.sKey,
            KeyCode.UpArrow => keyboard.upArrowKey,
            KeyCode.DownArrow => keyboard.downArrowKey,
            _ => null
        };

        return control != null && control.isPressed;
    }
#endif
}

internal sealed class BallController : MonoBehaviour
{
    private Rigidbody2D body;
    private float baseSpeed;
    private float speedGain;
    private float maxSpeed;
    private float currentSpeed;
    private float maxBounceAngle;

    public void Initialize(Rigidbody2D rigidbody, float baseSpeed, float speedGain, float maxSpeed, float maxBounceAngle)
    {
        body = rigidbody;
        this.baseSpeed = baseSpeed;
        this.speedGain = speedGain;
        this.maxSpeed = maxSpeed;
        this.maxBounceAngle = maxBounceAngle;
        currentSpeed = baseSpeed;
    }

    public void Launch(float direction)
    {
        currentSpeed = baseSpeed;
        float randomAngle = Random.Range(-20f, 20f) * Mathf.Deg2Rad;
        var dir = new Vector2(Mathf.Sign(direction) * Mathf.Cos(randomAngle), Mathf.Sin(randomAngle)).normalized;
        body.position = Vector2.zero;
        body.velocity = dir * currentSpeed;
    }

    public void StopImmediately()
    {
        body.velocity = Vector2.zero;
        body.position = Vector2.zero;
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.collider.TryGetComponent<PaddleController>(out var paddle))
        {
            float offset = transform.position.y - paddle.transform.position.y;
            float normalized = Mathf.Clamp(offset / (paddle.Height * 0.5f), -1f, 1f);
            float angle = normalized * maxBounceAngle * Mathf.Deg2Rad;
            float direction = transform.position.x < 0f ? 1f : -1f;

            currentSpeed = Mathf.Min(currentSpeed + speedGain, maxSpeed);
            Vector2 newDir = new Vector2(Mathf.Cos(angle) * direction, Mathf.Sin(angle)).normalized;
            body.velocity = newDir * currentSpeed;
        }
        else
        {
            // Normalizar velocidad para evitar que se pierda energía en colisiones contra las paredes.
            body.velocity = body.velocity.normalized * currentSpeed;
        }
    }
}

internal sealed class GoalZone : MonoBehaviour
{
    private PongGame game;
    private bool rightPlayerScores;

    public void Configure(PongGame game, bool rightPlayerScores)
    {
        this.game = game;
        this.rightPlayerScores = rightPlayerScores;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.TryGetComponent<BallController>(out _))
            return;

        game.Score(rightPlayerScores);
    }
}
