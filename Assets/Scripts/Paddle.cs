using UnityEngine;

public class Paddle : MonoBehaviour
{
    /// <summary>BCI class index of the power-up target in the middle of the field.</summary>
    public const int PowerUpTargetIndex = 4;

    public float speed = 10f;

    public Transform anchorTop;
    public Transform anchorMiddle;
    public Transform anchorBottom;

    public bool discreteMovements = false;
    public float discreteSpeed = 200f;

    public float topBound = 3.8f;
    public float bottomBound = -3.8f;

    public int currentAnchor = 2;

    [Header("Power-ups")]
    [Tooltip("1 = left paddle ('Player'), 2 = right paddle ('Player2'). 0 = detect from position.")]
    public int side = 0;
    [Tooltip("Transform stretched by the paddle-size power-ups. Empty = this transform. " +
             "Use a child holding the sprite + collider if your stimuli are children of the paddle.")]
    public Transform sizeTarget;

    [Header("Team colour")]
    [Tooltip("Tint the SpriteRenderer on sizeTarget: left = blue, right = red.")]
    public bool applyTeamColor = true;
    public Color leftColor = new Color(0.2f, 0.5f, 1f);
    public Color rightColor = new Color(1f, 0.25f, 0.25f);

    public Color TeamColor { get { return side == 2 ? rightColor : leftColor; } }

    public float SizeMultiplier { get; private set; }
    private float baseScaleY = 1f;

    void Awake()
    {
        if (sizeTarget == null) sizeTarget = transform;
        baseScaleY = sizeTarget.localScale.y;
        SizeMultiplier = 1f;
        if (side == 0) side = transform.position.x < 0f ? 1 : 2;

        if (applyTeamColor)
        {
            var sr = sizeTarget.GetComponent<SpriteRenderer>();
            if (sr != null) sr.color = TeamColor;
        }
    }

    // Update is called once per frame
    void Update()
    {
        Vector2 pos = transform.position;

        if (pos.y > topBound)
            pos.y = topBound;
        if (pos.y < bottomBound)
            pos.y = bottomBound;

        transform.position = pos;
    }

    /// <summary>
    /// Entry point for the BCI classifier: 1 = top, 2 = middle, 3 = bottom, 4 = power-up target.
    /// Call this instead of MoveToAnchor from your TaskController / serial handler.
    /// </summary>
    public void SelectTarget(int target)
    {
        if (target == PowerUpTargetIndex)
        {
            if (PowerUpManager.Instance != null) PowerUpManager.Instance.RequestClaim(side);
            return;
        }
        MoveToAnchor(target);
    }

    public void MoveToAnchor(int anchor)
    {
        anchor = anchor == 0? 1 : anchor;
        anchor = anchor == 4? 3 : anchor;

        if(anchor == 1)
        {
            currentAnchor = 1;
            transform.position = anchorTop.position;
        }
        else if(anchor == 2)
        {
            currentAnchor = 2;
            transform.position = anchorMiddle.position;
        }
        else if(anchor == 3)
        {
            currentAnchor = 3;
            transform.position = anchorBottom.position;
        }
    }

    public void MoveUp(float paddleSpeed)
    {
        if(discreteMovements)
        {
            MoveToAnchor(currentAnchor - 1);

        } else
        {
            transform.Translate(Vector3.up * paddleSpeed * Time.deltaTime);
        }
    }

    public void MoveDown(float paddleSpeed)
    {
        if(discreteMovements)
        {
            MoveToAnchor(currentAnchor + 1);
        } else
        {
            transform.Translate(Vector3.down * paddleSpeed * Time.deltaTime);
        }
    }

    /// <summary>Used by the network code to place the remote player's paddle.</summary>
    public void SetNetworkY(float y)
    {
        Vector3 p = transform.position;
        p.y = y;
        transform.position = p;
    }

    /// <summary>Stretches the paddle vertically (1 = normal size).</summary>
    public void SetSizeMultiplier(float multiplier)
    {
        if (sizeTarget == null) return;
        if (Mathf.Approximately(multiplier, SizeMultiplier)) return;
        SizeMultiplier = multiplier;
        Vector3 s = sizeTarget.localScale;
        s.y = baseScaleY * multiplier;
        sizeTarget.localScale = s;
    }
}
