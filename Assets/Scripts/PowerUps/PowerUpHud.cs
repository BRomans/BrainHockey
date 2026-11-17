using System.Text;
using UnityEngine;
using BrainHockey.Net;

/// <summary>
/// Shows "YOU got Big paddle!" announcements and the list of running effects with timers.
/// Assign TextMeshPro texts for a nice look; without them it falls back to OnGUI.
/// </summary>
public class PowerUpHud : MonoBehaviour
{
    public PowerUpManager powerUps;

    [Tooltip("Optional: large centred text for announcements.")]
    public TMPro.TextMeshProUGUI announcementText;
    [Tooltip("Optional: text listing the active effects.")]
    public TMPro.TextMeshProUGUI effectsText;

    public float announcementTime = 2.5f;
    public Color blueColor = new Color(0.35f, 0.6f, 1f);
    public Color redColor = new Color(1f, 0.4f, 0.4f);

    private string announcement = "";
    private Color announcementColor = Color.white;
    private float announcementUntil;
    private readonly StringBuilder sb = new StringBuilder();

    void Start()
    {
        if (powerUps == null) powerUps = PowerUpManager.Instance;
        if (powerUps != null) powerUps.PowerUpActivated += OnActivated;
    }

    void OnDestroy()
    {
        if (powerUps != null) powerUps.PowerUpActivated -= OnActivated;
    }

    private bool Networked { get { return powerUps != null && powerUps.net != null && powerUps.net.IsNetworked; } }

    private string Who(int owner)
    {
        string team = owner == 1 ? "BLUE" : "RED";
        if (!Networked) return team;
        return team + (owner == powerUps.LocalOwner ? " (you)" : "");
    }

    private Color SideColor(int owner)
    {
        return owner == 1 ? blueColor : redColor;
    }

    private void OnActivated(PowerUpType type, int owner)
    {
        announcement = Who(owner) + ": " + PowerUpManager.DisplayName(type) + "!";
        announcementColor = SideColor(owner);
        announcementUntil = Time.time + announcementTime;
    }

    void Update()
    {
        if (announcementText != null)
        {
            bool show = Time.time < announcementUntil;
            announcementText.text = show ? announcement : "";
            announcementText.color = announcementColor;
        }
        if (effectsText != null) effectsText.text = BuildEffectsText(true);
    }

    private string BuildEffectsText(bool richText)
    {
        sb.Length = 0;
        if (powerUps == null) return "";
        foreach (var e in powerUps.Effects)
        {
            string line = Who(e.owner) + ": " + PowerUpManager.DisplayName(e.type) +
                          (e.remaining < 0f ? " (until goal)" : "  " + Mathf.CeilToInt(e.remaining) + "s");
            if (richText)
            {
                string hex = ColorUtility.ToHtmlStringRGB(SideColor(e.owner));
                sb.Append("<color=#").Append(hex).Append('>').Append(line).Append("</color>\n");
            }
            else
            {
                sb.Append(line).Append('\n');
            }
        }
        return sb.ToString();
    }

    void OnGUI()
    {
        if (announcementText == null && Time.time < announcementUntil)
        {
            var big = new GUIStyle(GUI.skin.label) { fontSize = 32, alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
            big.normal.textColor = announcementColor;
            GUI.Label(new Rect(0, Screen.height * 0.15f, Screen.width, 50), announcement, big);
        }
        if (effectsText == null)
        {
            var small = new GUIStyle(GUI.skin.label) { fontSize = 16 };
            small.normal.textColor = Color.white;
            GUI.Label(new Rect(12, 12, 600, 200), BuildEffectsText(false), small);
        }
    }
}
