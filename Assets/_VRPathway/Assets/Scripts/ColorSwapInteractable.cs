using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// COLOR SWAP — เปลี่ยนสีวัตถุเมื่อถูกจับ (Feature ใหม่ #1 ที่ต่างจาก workshop)
/// การทำงาน:
/// - จับวัตถุ -> เปลี่ยนเป็นสีสุ่มสว่างๆ ทุกครั้งไม่ซ้ำกัน
/// - ปล่อยวัตถุ -> กลับเป็นสีเดิม
/// - ใช้ MaterialPropertyBlock จึงไม่เปลี่ยน material ต้นฉบับ (ถูกหลัก VR performance)
///
/// วิธีติดตั้ง (ใช้กับวัตถุอะไรก็ได้ที่จับได้):
/// 1. สร้างวัตถุ เช่น Cube / Sphere วางบนโต๊ะในฉาก
/// 2. ตรวจว่ามี Collider แล้ว
/// 3. Add Component > XR Grab Interactable
/// 4. Add Component > Color Swap Interactable (สคริปต์นี้)
/// </summary>
public class ColorSwapInteractable : MonoBehaviour
{
    [Header("ตั้งค่า")]
    [Tooltip("Renderer ของวัตถุที่จะเปลี่ยนสี (ว่าง = หาเองอัตโนมัติ)")]
    [SerializeField] Renderer targetRenderer;

    [Tooltip("สีตอนถูกจับ (ใช้เมื่อปิดโหมดสุ่มสี)")]
    [SerializeField] Color grabbedColor = Color.cyan;

    [Tooltip("สุ่มสีใหม่ทุกครั้งที่จับ")]
    [SerializeField] bool randomColorEachGrab = true;

    [Tooltip("ความสว่างของสีสุ่ม (0-1)")]
    [SerializeField] float randomColorBrightness = 0.9f;

    // ชื่อ property สีของ shader — รองรับทั้ง URP (_BaseColor) และ Built-in (_Color)
    static readonly int BaseColorID = Shader.PropertyToID("_BaseColor");
    static readonly int ColorID = Shader.PropertyToID("_Color");

    Renderer m_Renderer;
    Color m_OriginalColor;
    MaterialPropertyBlock m_Block;
    IXRSelectInteractable m_Interactable;

    void Awake()
    {
        m_Renderer = targetRenderer != null ? targetRenderer : GetComponentInChildren<Renderer>();
        m_Block = new MaterialPropertyBlock();

        if (m_Renderer != null)
            m_OriginalColor = GetColor();
    }

    void OnEnable()
    {
        m_Interactable = GetComponent<IXRSelectInteractable>();
        if (m_Interactable == null || m_Interactable as Object == null)
        {
            Debug.LogWarning($"[ColorSwapInteractable] {name} ยังไม่มี XR Grab Interactable — ใส่ก่อนถึงจะเปลี่ยนสีได้", this);
            enabled = false;
            return;
        }

        m_Interactable.selectEntered.AddListener(OnGrabbed);
        m_Interactable.selectExited.AddListener(OnReleased);
    }

    void OnDisable()
    {
        if (m_Interactable as Object != null)
        {
            m_Interactable.selectEntered.RemoveListener(OnGrabbed);
            m_Interactable.selectExited.RemoveListener(OnReleased);
        }
    }

    void OnGrabbed(SelectEnterEventArgs args)
    {
        Color c = randomColorEachGrab ? Random.ColorHSV(0f, 1f, 0.6f, 0.8f, randomColorBrightness, randomColorBrightness) : grabbedColor;
        SetColor(c);
    }

    void OnReleased(SelectExitEventArgs args)
    {
        SetColor(m_OriginalColor);
    }

    // ---------- จัดการสี ----------

    Color GetColor()
    {
        m_Renderer.GetPropertyBlock(m_Block);
        if (m_Block.HasProperty(BaseColorID))
            return m_Block.GetColor(BaseColorID);
        return m_Renderer.sharedMaterial.color;
    }

    void SetColor(Color c)
    {
        if (m_Renderer == null)
            return;

        m_Renderer.GetPropertyBlock(m_Block);
        m_Block.SetColor(BaseColorID, c);   // shader ของ URP
        m_Block.SetColor(ColorID, c);       // shader ของ Built-in
        m_Renderer.SetPropertyBlock(m_Block);
    }
}
